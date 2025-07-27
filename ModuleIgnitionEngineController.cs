using System;
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
        public int FixedIgnitors = 0;

        [KSPField(isPersistant = true, guiName = "<b>Fixed ignitors</b>")]
        [UI_Label(scene = UI_Scene.All)]
        private int FixedIgnitorsRemaining = -1;

        [KSPField(isPersistant = true)]
        public string IgnitionResourcesString = null;
        private List<IgnitionResource> IgnitionResources = new List<IgnitionResource>();

        [KSPField(guiName = "<b>Ignition resources</b>")]
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

            if (FixedIgnitorsRemaining == -1) FixedIgnitorsRemaining = FixedIgnitors;

            IgnitionResources.Clear();
            IgnitionResourcesDisplayString = "";
            if (!string.IsNullOrEmpty(IgnitionResourcesString))
            {
                foreach (var requiredResourceString in IgnitionResourcesString.Split(';'))
                {
                    var ignitionResource = IgnitionResource.FromString(requiredResourceString);
                    ignitionResource.Amount *= GetScale(EngineThrustScaleExponent);
                    ignitionResource.ScaledAmount *= GetScale(EngineThrustScaleExponent);
                    if (ignitionResource.Amount == 0 && ignitionResource.ScaledAmount > 0)
                    {
                        var unroundedAmount = ignitionResource.ScaledAmount * GetEngineMassRate();
                        var powerOfTen = Math.Pow(10, Math.Floor(Math.Log10(unroundedAmount)));
                        ignitionResource.Amount = powerOfTen * Math.Round(unroundedAmount / powerOfTen);
                        IgnitionResourcesDisplayString += "\n  " + ignitionResource.Amount + " " + ignitionResource.ResourceName;
                        if (ignitionResource.AlwaysRequired) IgnitionResourcesDisplayString += " (always consumed)";
                        else IgnitionResourcesDisplayString += " (consumed if necessary)";
                    }
                    IgnitionResources.Add(ignitionResource);
                }
            }

            if (PropellantNodeResourceNames is null && !(ModuleEngines.propellants is null))
            {
                PropellantNodeResourceNames = "";
                for (int i = 0; i < ModuleEngines.propellants.Count; i++)
                {
                    PropellantNodeResourceNames += ModuleEngines.propellants[i].name;
                    if (i != ModuleEngines.propellants.Count - 1) PropellantNodeResourceNames += ";";
                }
            }

            if (ModuleEngines.atmosphereCurve.Curve.keys.Length == 0) return;
            if (MaxThrustOriginal == 0) MaxThrustOriginal = ModuleEngines.maxThrust;
            if (IspVacuumOriginal == 0) IspVacuumOriginal = GetKeyframeValue(ModuleEngines.atmosphereCurve.Curve.keys, 0);
            if (IspSeaLevelOriginal == 0) IspSeaLevelOriginal = GetKeyframeValue(ModuleEngines.atmosphereCurve.Curve.keys, 1);
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

            Fields["FixedIgnitorsRemaining"].guiActiveEditor = FixedIgnitors > 0;
            Fields["FixedIgnitorsRemaining"].guiActive = FixedIgnitors > 0;
        }

        protected override void ApplyPropellantCombinationToModule()
        {
            if (ModuleIsNull()) return;

            ModuleEngines.maxThrust = (float)MaxThrustCurrent;
            ModuleEngines.maxFuelFlow = (float)MaxFuelFlowCurrent;
            ModuleEngines.atmosphereCurve.Curve.keys = GetIspKeys();
            ModuleEngines.propellants = GetAllCurrentPropellants(ModuleEngines.propellants);
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

        protected override double GetG()
        {
            if (ModuleIsNull()) return 9.80665;

            return ModuleEngines.g;
        }

        protected override bool UseIspSeaLevel()
        {
            if (ModuleIsNull()) return false;

            return !ModuleEngines.useVelCurve && IspSeaLevelOriginal != 0;
        }

        private double GetEngineMassRate()
        {
            if (ModuleIsNull()) return 0;

            double isp = ModuleEngines.atmosphereCurve.Curve.keys[0].value;
            if (!ModuleEngines.useVelCurve && ModuleEngines.atmosphereCurve.Curve.keys.Length > 1) isp = ModuleEngines.atmosphereCurve.Curve.keys[1].value;
            return 1000 * ModuleEngines.maxThrust / (ModuleEngines.g * isp);
        }

        private void FixedUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight || ModuleEngines is null || PropellantConfigCurrent is null) return;

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
            if ((ModuleEngines.requestedThrottle <= 0.0) || EngineFlameout() || (ModuleEngines.EngineIgnited == false && ModuleEngines.allowShutdown)) return false;

            if (!ModuleEngines.EngineIgnited) return vessel.ctrlState.mainThrottle > 0.0 || ModuleEngines.throttleLocked;

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

        private bool UseIgnitionResource(IgnitionResource ignitionResource, ref double addedIgnitionPotential, ref Dictionary<int, double> resourcesToDrain)
        {
            double resourceAmount = 0.0;
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

            bool ignitionAchieved = false;

            // Always use any fixed ignitors
            if (FixedIgnitorsRemaining > 0)
            {
                ignitionAchieved = true;
                FixedIgnitorsRemaining--;
            }

            // Always use any required ignition resources
            var addedIgnitionPotential = 0.0;
            Dictionary<int, double> resourcesToDrain = new Dictionary<int, double>();
            var missingResources = new List<string>();
            foreach (var ignitionResource in IgnitionResources)
            {
                if (!ignitionResource.AlwaysRequired) continue;

                bool success = UseIgnitionResource(ignitionResource, ref addedIgnitionPotential, ref resourcesToDrain);
                if (!success)
                {
                    // Required ignition resource not satisfied
                    ignitionAchieved = false;
                    missingResources.Add(ignitionResource.ResourceName);
                    break;
                }
            }

            // If required ignition resources are satisfied, compute ignition potential and try other ignition resources if ignition is not yet achieved
            if (missingResources.Count == 0)
            {
                // Compute ignition potential of propellant combination
                var ignitionThreshold = 0.999; // A bit less than 1 to allow for precision issues
                var ignitionPotential = 1.0;
                if (!ignitionAchieved)
                {
                    var propellantConfigNodes = GameDatabase.Instance.GetConfigNodes("IgnitionPropellantConfig");
                    var propellantConfigs = new Dictionary<string, PropellantConfig>();
                    foreach (var propellantConfigNode in propellantConfigNodes)
                    {
                        var propellantConfig = new PropellantConfig(propellantConfigNode);
                        propellantConfigs[propellantConfig.ResourceName] = propellantConfig;
                    }
                    var totalRatio = 0.0;
                    foreach (var propellant in ModuleEngines.propellants) totalRatio += propellant.ratio;
                    foreach (var propellant in ModuleEngines.propellants)
                    {
                        if (!propellantConfigs.ContainsKey(propellant.name)) continue;
                        ignitionPotential *= Math.Pow(propellantConfigs[propellant.name].IgnitionPotential, propellant.ratio / totalRatio);
                    }
                    ignitionPotential += addedIgnitionPotential;
                    ignitionAchieved = ignitionPotential > ignitionThreshold;
                }

                // Use ignition resources
                if (!ignitionAchieved)
                {
                    foreach (var ignitionResource in IgnitionResources)
                    {
                        if (ignitionResource.AlwaysRequired) continue;

                        bool success = UseIgnitionResource(ignitionResource, ref ignitionPotential, ref resourcesToDrain);
                        if (!success) missingResources.Add(ignitionResource.ResourceName);

                        if (ignitionPotential > ignitionThreshold)
                        {
                            ignitionAchieved = true;
                            break;
                        }
                    }
                }
            }

            if (ignitionAchieved)
            {
                message = "Ignition!";
                foreach (var resourceToDrain in resourcesToDrain)
                {
                    part?.RequestResource(resourceToDrain.Key, resourceToDrain.Value, ResourceFlowMode.STAGE_PRIORITY_FLOW);
                }
            }
            else if (FixedIgnitors > 0 && IgnitionResources.Count == 0) message = "Ignition failed — No ignitors remaining";
            else if (missingResources.Count > 0) message = "Ignition failed — Not enough " + missingResources.First();
            else message = "Ignition failed — Ignitor was insufficient";

            return ignitionAchieved;
        }

        protected override double GetScaledMaxThrustOriginal()
        {
            return GetScale(EngineThrustScaleExponent) * MaxThrustOriginal;
        }
    }
}