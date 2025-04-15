using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Ignition
{
    public class ModuleIgnitor : PartModule
    {
        [KSPField(isPersistant = true)]
        public string engineID = "";

        bool IsMultiModeEngine = false;
        private ModuleEngines EngineModule = null;

        private bool _ignited = false;

        [KSPField(isPersistant = true)]
        public string IgnitorResourcesString = null;
        public List<IgnitorResource> IgnitorResources = new List<IgnitorResource>();
        public Dictionary<string, PropellantConfig> PropellantConfigs = new Dictionary<string, PropellantConfig>();

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);

            if (!(IgnitorResourcesString is null)) return;

            IgnitorResourcesString = "";
            var ignitorResourceNodes = node.GetNodes("IGNITOR_RESOURCE");
            for (int i = 0; i < ignitorResourceNodes.Length; i++)
            {
                if (ignitorResourceNodes[i].HasValue("name") == false) continue;
                if (ignitorResourceNodes[i].HasValue("Amount") == false && ignitorResourceNodes[i].HasValue("ScaledAmount") == false) continue;

                IgnitorResource newIgnitorResource = new IgnitorResource();
                newIgnitorResource.Load(ignitorResourceNodes[i]);
                IgnitorResourcesString += newIgnitorResource.ToString();
                if (i != ignitorResourceNodes.Length - 1) IgnitorResourcesString += ';';
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

            // Resources required for ignitor
            IgnitorResources.Clear();
            if (IgnitorResourcesString != "")
            {
                foreach (var requiredResourceString in IgnitorResourcesString.Split(';'))
                {
                    var ignitorResource = IgnitorResource.FromString(requiredResourceString);
                    if (ignitorResource.Amount == 0) ignitorResource.Amount = ignitorResource.ScaledAmount * GetEngineMassRate();
                    IgnitorResources.Add(ignitorResource);
                }
            }

            // Propellant configs for ignition potential computation
            var propellantConfigNodes = GameDatabase.Instance.GetConfigNodes("IgnitionPropellantConfig");
            PropellantConfigs.Clear();
            foreach (var propellantConfigNode in propellantConfigNodes)
            {
                var propellantConfig = new PropellantConfig(propellantConfigNode);
                PropellantConfigs[propellantConfig.ResourceName] = propellantConfig;
            }
        }

        private float GetEngineMassRate()
        {
            float isp = EngineModule.atmosphereCurve.Curve.keys[0].value;
            if (!EngineModule.useVelCurve) isp = EngineModule.atmosphereCurve.Curve.keys[1].value;
            return 1000 * EngineModule.maxThrust / (EngineModule.g * isp);
        }

        private void FixedUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight || EngineModule is null || !EngineModule.allowShutdown) return;

            bool shouldBeIgnited = ShouldBeIgnited() || OtherEngineModeActive();

            if (!shouldBeIgnited) _ignited = false;
            else if (!_ignited && shouldBeIgnited)
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

        private bool ShouldBeIgnited()
        {
            bool flameout = EngineModule is ModuleEnginesFX engineModuleFX ? engineModuleFX.getFlameoutState : EngineModule.flameout;
            if ((EngineModule.requestedThrottle <= 0.0f && !IsMultiModeEngine) || flameout || (EngineModule.EngineIgnited == false && EngineModule.allowShutdown)) return false;

            if (!EngineModule.EngineIgnited) return vessel.ctrlState.mainThrottle > 0.0f || EngineModule.throttleLocked;

            return EngineModule.EngineIgnited;
        }

        string CurrentActiveEngineID()
        {
            foreach (var engineModule in part.FindModulesImplementing<ModuleEngines>())
            {
                if (engineModule.EngineIgnited == true) return engineModule.engineID;
            }
            return null;
        }

        bool OtherEngineModeActive()
        {
            foreach (var engineModule in part.FindModulesImplementing<ModuleEngines>())
            {
                if (engineModule.engineID == engineID) continue;

                bool deprived = engineModule.CheckDeprived(.01, out string propName);
                bool flameout = EngineModule is ModuleEnginesFX engineModuleFX ? engineModuleFX.getFlameoutState : EngineModule.flameout;
                if (engineModule.EngineIgnited == true && !flameout && !deprived)
                {
                    return true;
                }
            }
            return false;
        }

        private bool UseIgnitorResource(IgnitorResource ignitorResource, ref float addedIgnitionPotential, ref Dictionary<int, float> resourcesToDrain)
        {
            double resourceAmount = 0f;
            int resourceId = PartResourceLibrary.Instance.GetDefinition(ignitorResource.ResourceName).id;
            part?.GetConnectedResourceTotals(resourceId, out resourceAmount, out double resourceMaxAmount);

            var requiredResourceAmount = ignitorResource.Amount;
            if (resourceAmount < requiredResourceAmount) return false;

            addedIgnitionPotential += ignitorResource.AddedIgnitionPotential;
            resourcesToDrain[resourceId] = requiredResourceAmount;
            return true;
        }

        private bool AttemptIgnition(ref string message)
        {
            if (IsMultiModeEngine && OtherEngineModeActive() && CurrentActiveEngineID() != engineID) return true;

            // Use required ignitors
            var addedIgnitionPotential = 0f;
            Dictionary<int, float> resourcesToDrain = new Dictionary<int, float>();
            foreach (var ignitorResource in IgnitorResources)
            {
                if (!ignitorResource.AlwaysRequired) continue;

                bool success = UseIgnitorResource(ignitorResource, ref addedIgnitionPotential, ref resourcesToDrain);
                if (!success)
                {
                    // Required ignitor not satisfied
                    message = "Ignition failed — Not enough " + ignitorResource.ResourceName;
                    return false;
                }
            }

            // Compute ignition potential of propellant combination
            var totalRatio = 0f;
            foreach (var propellant in EngineModule.propellants) totalRatio += propellant.ratio;
            var ignitionPotential = 1f;
            foreach (var propellant in EngineModule.propellants)
            {
                if (!PropellantConfigs.ContainsKey(propellant.name)) continue;
                ignitionPotential *= Mathf.Pow(PropellantConfigs[propellant.name].IgnitionPotential, propellant.ratio / totalRatio);
            }
            ignitionPotential += addedIgnitionPotential;

            // Use other ignitors if ignition is not yet achieved
            var ignitionThreshold = 0.999; // A bit less than 1 to allow for precision issues
            var missingResources = new List<string>();
            if (ignitionPotential < ignitionThreshold)
            {
                foreach (var ignitorResource in IgnitorResources)
                {
                    if (ignitorResource.AlwaysRequired) continue;

                    bool success = UseIgnitorResource(ignitorResource, ref ignitionPotential, ref resourcesToDrain);
                    if (!success) missingResources.Add(ignitorResource.ResourceName);

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
