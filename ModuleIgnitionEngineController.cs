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
                    var allEngineModules = part.FindModulesImplementing<ModuleEngines>();
                    if (engineID != "")
                    {
                        foreach (var engineModule in allEngineModules)
                        {
                            if (engineModule.engineID == engineID)
                            {
                                _moduleEngine = engineModule;
                                break;
                            }
                        }
                    }
                    else if (allEngineModules.Count > 0) _moduleEngine = allEngineModules.FirstOrDefault();
                }
                return _moduleEngine;
            }
        }

        private bool IsMultiModeEngine = false;
        private ModuleEngines EngineModule = null;

        private bool _ignited = false;

        [KSPField(isPersistant = true)]
        public string IgnitionResourcesString = null;
        private List<IgnitionResource> IgnitionResources = new List<IgnitionResource>();

        [KSPField(guiName = "<b>Ignitor</b>")]
        [UI_Label(scene = UI_Scene.All)]
        public string IgnitionResourceDisplayString = "";

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);

            if (!(IgnitionResourcesString is null)) return;

            IgnitionResourcesString = "";
            var ignitionResourceNodes = node.GetNodes("IGNITION_RESOURCE");
            for (int i = 0; i < ignitionResourceNodes.Length; i++)
            {
                if (ignitionResourceNodes[i].HasValue("name") == false) continue;
                if (ignitionResourceNodes[i].HasValue("Amount") == false && ignitionResourceNodes[i].HasValue("ScaledAmount") == false) continue;

                IgnitionResource newIgnitionResource = new IgnitionResource();
                newIgnitionResource.Load(ignitionResourceNodes[i]);
                IgnitionResourcesString += newIgnitionResource.ToString();
                if (i != ignitionResourceNodes.Length - 1) IgnitionResourcesString += ';';
            }
        }

        public void Start()
        {
            if (part is null || part.Modules is null) return;

            // Engine modules
            var engineModules = part.FindModulesImplementing<ModuleEngines>();
            if (engineModules.Count == 1) EngineModule = engineModules.First();
            else
            {
                foreach (var engineModule in engineModules)
                {
                    if (engineModule.engineID == engineID)
                    {
                        EngineModule = engineModule;
                        break;
                    }
                }
            }
            if (EngineModule is null) return;
            if (engineID == "") engineID = EngineModule.engineID;
            IsMultiModeEngine = part.FindModulesImplementing<MultiModeEngine>().Count > 0;

            // Resources used for ignition
            IgnitionResources.Clear();
            IgnitionResourceDisplayString = "";
            if (IgnitionResourcesString != "")
            {
                foreach (var requiredResourceString in IgnitionResourcesString.Split(';'))
                {
                    var ignitionResource = IgnitionResource.FromString(requiredResourceString);
                    if (ignitionResource.Amount == 0 && ignitionResource.ScaledAmount > 0)
                    {
                        var unroundedAmount = ignitionResource.ScaledAmount * GetEngineMassRate();
                        var powerOfTen = Mathf.Pow(10, Mathf.Floor(Mathf.Log10(unroundedAmount)));
                        ignitionResource.Amount = powerOfTen * Mathf.Round(unroundedAmount / powerOfTen);
                        IgnitionResourceDisplayString += "\n  " + ignitionResource.Amount + " " + ignitionResource.ResourceName;
                        if (ignitionResource.AlwaysRequired) IgnitionResourceDisplayString += " (always consumed)";
                        else IgnitionResourceDisplayString += " (consumed if necessary)";
                    }
                    IgnitionResources.Add(ignitionResource);
                }
            }

            var groupName = engineID == "" ? "Engine" : engineID;
            Fields["IgnitionDisplayString"].group.name = groupName;
            Fields["IgnitionDisplayString"].group.displayName = groupName;
        }

        private Dictionary<string, PropellantConfig> GetPropellantConfigs()
        {
            var propellantConfigNodes = GameDatabase.Instance.GetConfigNodes("IgnitionPropellantConfig");
            var propellantConfigs = new Dictionary<string, PropellantConfig>();
            foreach (var propellantConfigNode in propellantConfigNodes)
            {
                var propellantConfig = new PropellantConfig(propellantConfigNode);
                propellantConfigs[propellantConfig.ResourceName] = propellantConfig;
            }
            return propellantConfigs;
        }

        protected override bool ModuleIsNull()
        {
            return ModuleEngines is null;
        }

        protected override string GetGroupName()
        {
            return engineID != "" ? engineID : "Engine";
        }

        protected override void InitialiseStats()
        {
            if (ModuleIsNull()) return;
            if (ModuleEngines.atmosphereCurve.Curve.keys.Length == 0) return;
            if (MaxThrustOriginal != -1) return;

            MaxThrustOriginal = ModuleEngines.maxThrust;
            IspVacuumOriginal = ModuleEngines.atmosphereCurve.Curve.keys[0].value;
            if (!ModuleEngines.useVelCurve) IspSeaLevelOriginal = ModuleEngines.atmosphereCurve.Curve.keys[1].value;
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

            float isp = EngineModule.atmosphereCurve.Curve.keys[0].value;
            if (!EngineModule.useVelCurve) isp = EngineModule.atmosphereCurve.Curve.keys[1].value;
            return 1000 * EngineModule.maxThrust / (EngineModule.g * isp);
        }

        private void FixedUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight || EngineModule is null || !EngineModule.allowShutdown) return;

            bool shouldBeIgnited = ShouldBeIgnited() || OtherEngineModeActive();

            if (!shouldBeIgnited) _ignited = false;
            else if (!_ignited)
            {
                string message = "";
                _ignited = AttemptIgnition(ref message);
                if (message != "") ScreenMessages.PostScreenMessage(message, 3f, ScreenMessageStyle.UPPER_CENTER);

                if (EngineModule.EngineIgnited)
                {
                    if (_ignited)
                    {
                        if (EngineModule is ModuleEnginesFX engineModuleFX) engineModuleFX.part.Effects.Event(engineModuleFX.engageEffectName, engineModuleFX.transform.hierarchyCount);
                        else EngineModule.PlayEngageFX();
                    }
                    else
                    {
                        if (EngineModule is ModuleEnginesFX engineModuleFX) engineModuleFX.part.Effects.Event(engineModuleFX.flameoutEffectName, engineModuleFX.transform.hierarchyCount);
                        else EngineModule.BurstFlameoutGroups();
                        EngineModule.SetRunningGroupsActive(false);
                        EngineModule.Shutdown();
                    }
                }
            }
        }

        private bool EngineFlameout()
        {
            return EngineModule is ModuleEnginesFX engineModuleFX ? engineModuleFX.getFlameoutState : EngineModule.flameout;
        }

        private bool ShouldBeIgnited()
        {
            if ((EngineModule.requestedThrottle <= 0.0f) || EngineFlameout() || (EngineModule.EngineIgnited == false && EngineModule.allowShutdown)) return false;

            if (!EngineModule.EngineIgnited) return vessel.ctrlState.mainThrottle > 0.0f || EngineModule.throttleLocked;

            return EngineModule.EngineIgnited;
        }

        bool OtherEngineModeActive()
        {
            foreach (var engineModule in part.FindModulesImplementing<ModuleEngines>())
            {
                if (engineModule.engineID == engineID) continue;

                bool flameout = EngineFlameout();
                bool deprived = engineModule.CheckDeprived(0.01, out string propName);
                if (engineModule.EngineIgnited == true && !flameout && !deprived)
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

            // Compute ignition potential of propellant combination
            var totalRatio = 0f;
            foreach (var propellant in EngineModule.propellants) totalRatio += propellant.ratio;
            var ignitionPotential = 1f;
            var propellantConfigs = GetPropellantConfigs();
            foreach (var propellant in EngineModule.propellants)
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