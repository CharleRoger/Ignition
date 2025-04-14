using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FuelMixer
{
    public class ModuleIgnitor : PartModule
    {
        private bool _isEngineMouseOver;

        // In case we have multiple engines...
        [KSPField(isPersistant = false)]
        public int EngineIndex = 0;

        bool MultiModeEngine = false;

        // List of all engines. So we can pick the one we are corresponding to.
        private List<EngineWrapper> _engines = new List<EngineWrapper>();
        private EngineWrapper _engine = null;

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
            _engines.Clear();

            if (part is null || part.Modules is null) return;

            var engineModules = part.FindModulesImplementing<ModuleEngines>();

            foreach (PartModule module in part.Modules)
            {
                if (module.moduleName == "ModuleEnginesFX") //find partmodule engine on the part
                {
                    _engines.Add(new EngineWrapper(module as ModuleEnginesFX));
                }
                if (module.moduleName == "ModuleEngines") //find partmodule engine on the part
                {
                    _engines.Add(new EngineWrapper(module as ModuleEngines));
                }
                if (module.moduleName == "MultiModeEngine") MultiModeEngine = true;
            }

            if (EngineIndex > _engines.Count - 1) return;

            _engine = _engines[EngineIndex];

            // Resources required for ignitor
            IgnitorResources.Clear();
            if (IgnitorResourcesString != "")
            {
                var requiredResourceStrings = IgnitorResourcesString.Split(';');
                foreach (var requiredResourceString in requiredResourceStrings) IgnitorResources.Add(IgnitorResource.FromString(requiredResourceString));
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

        int CurrentActiveMode()
        {
            int cnt = 0;
            foreach (PartModule pm in part.Modules) //change from part to partmodules
            {
                if (pm.moduleName == "ModuleEngines") //find partmodule engine on the part
                {
                    ModuleEngines em = pm as ModuleEngines;
                    cnt++;
                    if (em.EngineIgnited)
                        break;
                }
                if (pm.moduleName == "ModuleEnginesFX") //find partmodule engine on the part
                {
                    cnt++;
                    ModuleEnginesFX emfx = pm as ModuleEnginesFX;
                    if (emfx.EngineIgnited == true)
                        break;
                }
            }
            return cnt - 1;
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
                    resourceRequired += resource.Name + "(" + resource.GetAmount(_engine.MassRate).ToString("F1") + ")";
                    if (i != IgnitorResources.Count - 1) resourceRequired += ", ";
                    else resourceRequired += ".";
                }
            }

            Vector2 screenCoords = Camera.main.WorldToScreenPoint(part.transform.position);
            Rect ignitorInfoRect = new Rect(screenCoords.x - 100.0f, Screen.height - screenCoords.y - 10, 200.0f, 20.0f);
        }

        private void FixedUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight || _engine == null || !_engine.allowShutdown) return;

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

            if (attemptIgnition && !_ignited && _engine.EngineIgnited)
            {
                _engine.BurstFlameoutGroups();
                _engine.SetRunningGroupsActive(false);
                foreach (BaseEvent baseEvent in _engine.Events)
                {
                    if (baseEvent.name.IndexOf("shutdown", StringComparison.CurrentCultureIgnoreCase) >= 0)
                    {
                        baseEvent.Invoke();
                    }
                }
                _engine.SetRunningGroupsActive(false);
            }
        }

        private void DecideNewState()
        {
            if ((_engine.requestedThrottle <= 0.0f && !MultiModeEngine) || _engine.flameout || (_engine.EngineIgnited == false && _engine.allowShutdown))
            {
                _ignited = false;
                return;
            }

            if (!_ignited)
            {
                //When changing from not-ignited to ignited, we must ensure that the throttle is non-zero or locked (SRBs)
                if (vessel.ctrlState.mainThrottle > 0.0f || _engine.throttleLocked) _ignited = true;
                else _ignited = false;
            }
        }

        float oldEngineThrottle = 0;
        bool OtherEngineModeActive()
        {
            string s;

            // Check to see if any other engine mode is on
            int cnt = 0;
            foreach (PartModule pm in part.Modules) //change from part to partmodules
            {
                if (pm.moduleName == "ModuleEngines") //find partmodule engine on the part
                {
                    if (cnt == EngineIndex)
                        continue;
                    cnt++;
                    ModuleEngines em = pm as ModuleEngines;

                    bool deprived = em.CheckDeprived(.01, out s);
                    if (em.EngineIgnited == true && !em.flameout && !deprived)
                    {
                        oldEngineThrottle = em.requestedThrottle;
                        return true;
                    }
                }
                if (pm.moduleName == "ModuleEnginesFX") //find partmodule engine on the part
                {
                    if (cnt == EngineIndex)
                        continue;
                    cnt++;
                    ModuleEnginesFX emfx = pm as ModuleEnginesFX;

                    bool deprived = emfx.CheckDeprived(.01, out s);
                    if (emfx.EngineIgnited == true && !emfx.flameout && !deprived)
                    {
                        oldEngineThrottle = emfx.requestedThrottle;
                        return true;
                    }
                }
            }
            return false;
        }

        private bool UseIgnitor(IgnitorResource ignitorResource, ref float addedIgnitionPotential, ref Dictionary<int, float> resourcesToDrain)
        {
            double resourceAmount = 0f;
            int resourceId = PartResourceLibrary.Instance.GetDefinition(ignitorResource.Name).id;
            part?.GetConnectedResourceTotals(resourceId, out resourceAmount, out double resourceMaxAmount);

            var requiredResourceAmount = ignitorResource.GetAmount(_engine.MassRate);
            if (resourceAmount < requiredResourceAmount) return false;

            addedIgnitionPotential += ignitorResource.AddedIgnitionPotential;
            resourcesToDrain[resourceId] = requiredResourceAmount;
            return true;
        }

        private void AttemptIgnition()
        {
            var ignitionThreshold = 0.999; // A bit less than 1 to allow for precision issues

            var totalRatio = 0f;
            foreach (var propellant in _engine.propellants) totalRatio += propellant.ratio;

            if (!MultiModeEngine || (MultiModeEngine && !OtherEngineModeActive()) || CurrentActiveMode() == EngineIndex)
            {
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
                var ignitionPotential = 1f;
                foreach (var propellant in _engine.propellants)
                {
                    if (!PropellantConfigs.ContainsKey(propellant.name)) continue;
                    ignitionPotential *= Mathf.Pow(PropellantConfigs[propellant.name].IgnitionPotential, propellant.ratio / totalRatio);
                }
                ignitionPotential += addedIgnitionPotential;

                // Use other ignitors if ignition is not yet achieved
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
}
