using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FuelMixer
{
    public class ModuleIgnitor : PartModule
    {
        private bool _isEngineMouseOver;

        [KSPField(isPersistant = true)]
        public string engineID = "";

        bool MultiModeEngine = false;
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

            // Resources required for ignitor
            IgnitorResources.Clear();
            if (IgnitorResourcesString != "")
            {
                foreach (var requiredResourceString in IgnitorResourcesString.Split(';')) IgnitorResources.Add(IgnitorResource.FromString(requiredResourceString));
            }

            // Propellant configs for ignition potential computation
            var propellantConfigNodes = GameDatabase.Instance.GetConfigNodes("FuelMixerPropellantConfig");
            PropellantConfigs.Clear();
            foreach (var propellantConfigNode in propellantConfigNodes)
            {
                var propellantConfig = new PropellantConfig(propellantConfigNode);
                PropellantConfigs[propellantConfig.ResourceName] = propellantConfig;
            }
        }

        public void OnMouseEnter()
        {
            if (HighLogic.LoadedSceneIsEditor) _isEngineMouseOver = true;
        }

        public void OnMouseExit()
        {
            if (HighLogic.LoadedSceneIsEditor) _isEngineMouseOver = false;
        }

        private float GetEngineMassRate()
        {
            float isp = EngineModule.atmosphereCurve.Curve.keys[0].value;
            if (!EngineModule.useVelCurve) isp = EngineModule.atmosphereCurve.Curve.keys[1].value;
            return 1000 * EngineModule.maxThrust / (EngineModule.g * isp);
        }

        void OnGUI()
        {
            if (_isEngineMouseOver == false) return;

            string resourceRequired = "No resource requirement for ignition.";

            if (IgnitorResources.Count > 0)
            {
                resourceRequired = "Ignition requires: ";
                for (int i = 0; i < IgnitorResources.Count; ++i)
                {
                    IgnitorResource resource = IgnitorResources[i];
                    resourceRequired += resource.Name + "(" + resource.GetAmount(GetEngineMassRate()).ToString("F1") + ")";
                    if (i != IgnitorResources.Count - 1) resourceRequired += ", ";
                    else resourceRequired += ".";
                }
            }

            Vector2 screenCoords = Camera.main.WorldToScreenPoint(part.transform.position);
            Rect ignitorInfoRect = new Rect(screenCoords.x - 100.0f, Screen.height - screenCoords.y - 10, 200.0f, 20.0f);
        }

        private void FixedUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight || EngineModule is null || !EngineModule.allowShutdown) return;

            var oldIgnited = _ignited;
            DecideNewState();

            bool attemptIgnition = !oldIgnited && _ignited;

            if (OtherEngineModeActive())
            {
                _ignited = true;
            }
            else if (attemptIgnition)
            {
                AttemptIgnition();
            }

            if (attemptIgnition && !_ignited && EngineModule.EngineIgnited)
            {
                if (EngineModule is ModuleEnginesFX engineModuleFX) engineModuleFX.part.Effects.Event(engineModuleFX.flameoutEffectName, engineModuleFX.transform.hierarchyCount);
                else
                {
                    EngineModule.BurstFlameoutGroups();
                    EngineModule.SetRunningGroupsActive(false);
                }
                
                foreach (BaseEvent baseEvent in EngineModule.Events)
                {
                    if (baseEvent.name.IndexOf("shutdown", StringComparison.CurrentCultureIgnoreCase) >= 0)
                    {
                        baseEvent.Invoke();
                    }
                }
                EngineModule.SetRunningGroupsActive(false);
            }
        }

        private void DecideNewState()
        {
            bool flameout = EngineModule is ModuleEnginesFX engineModuleFX ? engineModuleFX.getFlameoutState : EngineModule.flameout;
            if ((EngineModule.requestedThrottle <= 0.0f && !MultiModeEngine) || flameout || (EngineModule.EngineIgnited == false && EngineModule.allowShutdown))
            {
                _ignited = false;
                return;
            }

            if (!_ignited)
            {
                //When changing from not-ignited to ignited, we must ensure that the throttle is non-zero or locked (SRBs)
                if (vessel.ctrlState.mainThrottle > 0.0f || EngineModule.throttleLocked) _ignited = true;
                else _ignited = false;
            }
        }

        string CurrentActiveEngineID()
        {
            foreach (var engineModule in part.FindModulesImplementing<ModuleEngines>())
            {
                if (engineModule.EngineIgnited == true) return engineModule.engineID;
            }
            return null;
        }

        float oldEngineThrottle = 0;
        bool OtherEngineModeActive()
        {
            foreach (var engineModule in part.FindModulesImplementing<ModuleEngines>())
            {
                if (engineModule.engineID == engineID) continue;

                bool deprived = engineModule.CheckDeprived(.01, out string propName);
                bool flameout = EngineModule is ModuleEnginesFX engineModuleFX ? engineModuleFX.getFlameoutState : EngineModule.flameout;
                if (engineModule.EngineIgnited == true && !flameout && !deprived)
                {
                    oldEngineThrottle = engineModule.requestedThrottle;
                    return true;
                }
            }
            return false;
        }

        private bool UseIgnitor(IgnitorResource ignitorResource, ref float addedIgnitionPotential, ref Dictionary<int, float> resourcesToDrain)
        {
            double resourceAmount = 0f;
            int resourceId = PartResourceLibrary.Instance.GetDefinition(ignitorResource.Name).id;
            part?.GetConnectedResourceTotals(resourceId, out resourceAmount, out double resourceMaxAmount);

            var requiredResourceAmount = ignitorResource.GetAmount(GetEngineMassRate());
            if (resourceAmount < requiredResourceAmount) return false;

            addedIgnitionPotential += ignitorResource.AddedIgnitionPotential;
            resourcesToDrain[resourceId] = requiredResourceAmount;
            return true;
        }

        private void AttemptIgnition()
        {
            if (MultiModeEngine && OtherEngineModeActive() && CurrentActiveEngineID() != engineID) return;

            // Use required ignitors
            var addedIgnitionPotential = 0f;
            Dictionary<int, float> resourcesToDrain = new Dictionary<int, float>();
            foreach (var ignitorResource in IgnitorResources)
            {
                if (!ignitorResource.AlwaysRequired) continue;

                bool success = UseIgnitor(ignitorResource, ref addedIgnitionPotential, ref resourcesToDrain);
                if (!success)
                {
                    // Required ignitor not satisfied
                    ScreenMessages.PostScreenMessage("Not enough " + ignitorResource.Name + " for ignitor", 3f, ScreenMessageStyle.UPPER_CENTER);
                    _ignited = false;
                    return;
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

                    bool success = UseIgnitor(ignitorResource, ref ignitionPotential, ref resourcesToDrain);
                    if (!success) missingResources.Add(ignitorResource.Name);

                    if (ignitionPotential > ignitionThreshold) break;
                }
            }
            if (ignitionPotential < ignitionThreshold)
            {
                // Ignition failed
                if (missingResources.Count > 0) ScreenMessages.PostScreenMessage("Not enough " + missingResources.First() + " for ignitor", 3f, ScreenMessageStyle.UPPER_CENTER);
                else ScreenMessages.PostScreenMessage("Failed to achieve ignition", 3f, ScreenMessageStyle.UPPER_CENTER);
                _ignited = false;
                return;
            }

            // Ignition achieved
            foreach (var resourceToDrain in resourcesToDrain)
            {
                part?.RequestResource(resourceToDrain.Key, resourceToDrain.Value, ResourceFlowMode.STAGE_PRIORITY_FLOW);
            }
        }
    }
}
