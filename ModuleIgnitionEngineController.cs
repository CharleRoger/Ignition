using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Ignition
{
    class ModuleIgnitionEngineController : ModuleIgnitionThrusterController
    {
        [KSPField(isPersistant = true)]
        public string engineID = "";

        private ModuleEngines _moduleEngine;
        private ModuleEngines ModuleEngines
        {
            get
            {
                if (_moduleEngine is null)
                {
                    var allModuleEnginess = part.FindModulesImplementing<ModuleEngines>();
                    if (engineID != "")
                    {
                        foreach (var ModuleEngines in allModuleEnginess)
                        {
                            if (ModuleEngines.engineID == engineID)
                            {
                                _moduleEngine = ModuleEngines;
                                break;
                            }
                        }
                    }
                    else if (allModuleEnginess.Count > 0) _moduleEngine = allModuleEnginess.FirstOrDefault();
                }
                return _moduleEngine;
            }
        }

        private bool IsMultiModeEngine = false;

        private bool _ignited = false;

        [KSPField(isPersistant = true)]
        public string IgnitionResourcesString = null;
        private List<IgnitionResource> IgnitionResources = new List<IgnitionResource>();

        [KSPField(guiName = "<b>Ignitor</b>")]
        [UI_Label(scene = UI_Scene.All)]
        public string IgnitionResourcesDisplayString = "";

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);

            if (!(IgnitionResourcesString is null)) return;

            IgnitionResourcesString = "";
            var ignitionResourceNodes = node.GetNodes("IGNITION_RESOURCE");
            for (int i = 0; i < ignitionResourceNodes.Length; i++)
            {
                if (!ignitionResourceNodes[i].HasValue("name")) continue;
                if (!ignitionResourceNodes[i].HasValue("Amount") && !ignitionResourceNodes[i].HasValue("ScaledAmount")) continue;

                IgnitionResource newIgnitionResource = new IgnitionResource();
                newIgnitionResource.Load(ignitionResourceNodes[i]);
                IgnitionResourcesString += newIgnitionResource.ToString();
                if (i != ignitionResourceNodes.Length - 1) IgnitionResourcesString += ';';
            }
        }

        protected override bool ModuleIsNull()
        {
            return ModuleEngines is null;
        }

        protected override string GetGroupName()
        {
            return IsMultiModeEngine ? "Engine mode \"" + engineID + "\"" : "Engine";
        }

        protected override void SetupOriginalData()
        {
            if (ModuleIsNull()) return;

            if (engineID == "") engineID = ModuleEngines.engineID;
            IsMultiModeEngine = part.HasModuleImplementing<MultiModeEngine>();

            IgnitionResources.Clear();
            IgnitionResourcesDisplayString = "";
            if (!string.IsNullOrEmpty(IgnitionResourcesString))
            {
                foreach (var requiredResourceString in IgnitionResourcesString.Split(';'))
                {
                    var ignitionResource = IgnitionResource.FromString(requiredResourceString);
                    if (ignitionResource.Amount == 0 && ignitionResource.ScaledAmount > 0)
                    {
                        var unroundedAmount = ignitionResource.ScaledAmount * GetEngineMassRate();
                        var powerOfTen = Mathf.Pow(10, Mathf.Floor(Mathf.Log10(unroundedAmount)));
                        ignitionResource.Amount = powerOfTen * Mathf.Round(unroundedAmount / powerOfTen);
                        IgnitionResourcesDisplayString += "\n  " + ignitionResource.Amount + " " + ignitionResource.ResourceName;
                        if (ignitionResource.AlwaysRequired) IgnitionResourcesDisplayString += " (always consumed)";
                        else IgnitionResourcesDisplayString += " (consumed if necessary)";
                    }
                    IgnitionResources.Add(ignitionResource);
                }
            }

            if (ModuleEngines.atmosphereCurve.Curve.keys.Length == 0) return;
            if (MaxThrustOriginal != -1) return;

            MaxThrustOriginal = ModuleEngines.maxThrust;
            IspVacuumOriginal = ModuleEngines.atmosphereCurve.Curve.keys[0].value;
            if (!ModuleEngines.useVelCurve) IspSeaLevelOriginal = ModuleEngines.atmosphereCurve.Curve.keys[1].value;
        }

        protected override void SetupInfoStrings()
        {
            base.SetupInfoStrings();

            var groupName = GetGroupName();
            Fields["IgnitionResourcesDisplayString"].group.name = groupName;
            Fields["IgnitionResourcesDisplayString"].group.displayName = groupName;

            var isActive = !ModuleIsNull() && IgnitionResources.Count != 0;
            Fields["IgnitionResourcesDisplayString"].guiActiveEditor = isActive;
            Fields["IgnitionResourcesDisplayString"].guiActive = isActive;
        }

        protected override void ApplyPropellantCombinationToModule()
        {
            if (ModuleIsNull()) return;

            ModuleEngines.maxThrust = MaxThrustCurrent;
            ModuleEngines.maxFuelFlow = MaxFuelFlowCurrent;
            ModuleEngines.atmosphereCurve.Curve.keys = GetIspKeys();
            ModuleEngines.propellants = PropellantConfigCurrent.Propellants;
            ModuleEngines.SetupPropellant();
        }

        protected override void RecompilePartInfo()
        {
            if (part.partInfo is null || part.partInfo.moduleInfos is null) return;

            var engineModules = part.FindModulesImplementing<ModuleEngines>();
            var engineIndex = 0;
            for (int i = 0; i < part.partInfo.moduleInfos.Count; i++)
            {
                if (part.partInfo.moduleInfos[i].moduleName != "Engine") continue;
                part.partInfo.moduleInfos[i].info = engineModules[engineIndex].GetInfo();
                engineIndex++;
            }
        }

        protected override float GetG()
        {
            if (ModuleIsNull()) return 9.80665f;

            return ModuleEngines.g;
        }

        protected override bool UseVelCurve()
        {
            if (ModuleIsNull()) return false;

            return ModuleEngines.useVelCurve;
        }

        private float GetEngineMassRate()
        {
            if (ModuleIsNull()) return 0;

            float isp = ModuleEngines.atmosphereCurve.Curve.keys[0].value;
            if (!ModuleEngines.useVelCurve) isp = ModuleEngines.atmosphereCurve.Curve.keys[1].value;
            return 1000 * ModuleEngines.maxThrust / (ModuleEngines.g * isp);
        }

        private void FixedUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight || ModuleEngines is null) return;

            // A single propellant cannot "ignite" in the strict sense, so the engine should light without ignition simulation
            if (PropellantConfigCurrent.Propellants.Count == 1) return;

            bool shouldBeIgnited = ShouldBeIgnited() || OtherEngineModeActive();

            if (!shouldBeIgnited) _ignited = false;
            else if (!_ignited)
            {
                string message = "";
                _ignited = AttemptIgnition(ref message);
                if (message != "") ScreenMessages.PostScreenMessage(message, 3f, ScreenMessageStyle.UPPER_CENTER);

                if (ModuleEngines.EngineIgnited)
                {
                    if (_ignited)
                    {
                        if (ModuleEngines is ModuleEnginesFX ModuleEnginesFX) ModuleEnginesFX.part.Effects.Event(ModuleEnginesFX.engageEffectName, ModuleEnginesFX.transform.hierarchyCount);
                        else ModuleEngines.PlayEngageFX();
                    }
                    else if (ModuleEngines.allowShutdown)
                    {
                        if (ModuleEngines is ModuleEnginesFX ModuleEnginesFX) ModuleEnginesFX.part.Effects.Event(ModuleEnginesFX.flameoutEffectName, ModuleEnginesFX.transform.hierarchyCount);
                        else ModuleEngines.BurstFlameoutGroups();
                        ModuleEngines.SetRunningGroupsActive(false);
                        ModuleEngines.Shutdown();
                    }
                }
            }
        }

        private bool EngineFlameout()
        {
            return ModuleEngines is ModuleEnginesFX ModuleEnginesFX ? ModuleEnginesFX.getFlameoutState : ModuleEngines.flameout;
        }

        private bool ShouldBeIgnited()
        {
            if ((ModuleEngines.requestedThrottle <= 0.0f) || EngineFlameout() || (ModuleEngines.EngineIgnited == false && ModuleEngines.allowShutdown)) return false;

            if (!ModuleEngines.EngineIgnited) return vessel.ctrlState.mainThrottle > 0.0f || ModuleEngines.throttleLocked;

            return ModuleEngines.EngineIgnited;
        }

        bool OtherEngineModeActive()
        {
            foreach (var ModuleEngines in part.FindModulesImplementing<ModuleEngines>())
            {
                if (ModuleEngines.engineID == engineID) continue;

                bool flameout = EngineFlameout();
                bool deprived = ModuleEngines.CheckDeprived(0.01, out string propName);
                if (ModuleEngines.EngineIgnited == true && !flameout && !deprived)
                {
                    return true;
                }
            }
            return false;
        }

        private bool UseIgnitionResource(IgnitionResource ignitionResource, ref float addedIgnitionPotential, ref Dictionary<int, float> resourcesToDrain)
        {
            double resourceAmount = 0f;
            int resourceId = PartResourceLibrary.Instance.GetDefinition(ignitionResource.ResourceName).id;
            part?.GetConnectedResourceTotals(resourceId, out resourceAmount, out double resourceMaxAmount);

            var requiredResourceAmount = ignitionResource.Amount;
            if (resourceAmount < requiredResourceAmount) return false;

            addedIgnitionPotential += ignitionResource.AddedIgnitionPotential;
            resourcesToDrain[resourceId] = requiredResourceAmount;
            return true;
        }

        private bool AttemptIgnition(ref string message)
        {
            if (IsMultiModeEngine && OtherEngineModeActive()) return true;

            // Use required ignition resources
            var addedIgnitionPotential = 0f;
            Dictionary<int, float> resourcesToDrain = new Dictionary<int, float>();
            foreach (var ignitionResource in IgnitionResources)
            {
                if (!ignitionResource.AlwaysRequired) continue;

                bool success = UseIgnitionResource(ignitionResource, ref addedIgnitionPotential, ref resourcesToDrain);
                if (!success)
                {
                    // Required ignition resource not satisfied
                    message = "Ignition failed — Not enough " + ignitionResource.ResourceName;
                    return false;
                }
            }

            var propellantConfigNodes = GameDatabase.Instance.GetConfigNodes("IgnitionPropellantConfig");
            var propellantConfigs = new Dictionary<string, PropellantConfig>();
            foreach (var propellantConfigNode in propellantConfigNodes)
            {
                var propellantConfig = new PropellantConfig(propellantConfigNode);
                propellantConfigs[propellantConfig.ResourceName] = propellantConfig;
            }

            // Compute ignition potential of propellant combination
            var totalRatio = 0f;
            foreach (var propellant in ModuleEngines.propellants) totalRatio += propellant.ratio;
            var ignitionPotential = 1f;
            foreach (var propellant in ModuleEngines.propellants)
            {
                if (!propellantConfigs.ContainsKey(propellant.name)) continue;
                ignitionPotential *= Mathf.Pow(propellantConfigs[propellant.name].IgnitionPotential, propellant.ratio / totalRatio);
            }
            ignitionPotential += addedIgnitionPotential;

            // Use other ignition resources if ignition is not yet achieved
            var ignitionThreshold = 0.999; // A bit less than 1 to allow for precision issues
            var missingResources = new List<string>();
            if (ignitionPotential < ignitionThreshold)
            {
                foreach (var ignitionResource in IgnitionResources)
                {
                    if (ignitionResource.AlwaysRequired) continue;

                    bool success = UseIgnitionResource(ignitionResource, ref ignitionPotential, ref resourcesToDrain);
                    if (!success) missingResources.Add(ignitionResource.ResourceName);

                    if (ignitionPotential > ignitionThreshold) break;
                }
            }
            if (ignitionPotential < ignitionThreshold)
            {
                // Ignition failed
                if (missingResources.Count > 0) message = "Ignition failed — Not enough " + missingResources.First();
                else message = "Ignition failed — Ignitor was insufficient";
                return false;
            }

            // Ignition achieved
            message = "Ignition!";
            foreach (var resourceToDrain in resourcesToDrain)
            {
                part?.RequestResource(resourceToDrain.Key, resourceToDrain.Value, ResourceFlowMode.STAGE_PRIORITY_FLOW);
            }
            return true;
        }
    }
}