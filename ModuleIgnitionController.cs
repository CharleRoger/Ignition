using System;
using System.Collections.Generic;
using static Ignition.PropellantConfigUtils;

namespace Ignition
{
    abstract class ModuleIgnitionController : PartModule
    {
        [KSPField(isPersistant = true)]
        public string moduleID;

        [KSPField(isPersistant = true)]
        public string PropellantModuleIDs = "";

        protected PropellantConfigBase PropellantConfigOriginal = null;
        protected PropellantConfigBase PropellantConfigCurrent = null;

        protected double ScaleFactorPrevious = 1;
        public double ScaleFactor = 1;
        private Dictionary<string, double> ScaleExponents = new Dictionary<string, double>();

        protected const string MassScaleExponent = "mass";
        protected const string CostScaleExponent = "cost";
        protected const string VolumeScaleExponent = "tank";
        protected const string EngineThrustScaleExponent = "engine";
        protected const string RCSThrustScaleExponent = "rcs";

        public virtual void UnapplyPropellantConfig() {}

        public abstract void ApplyPropellantConfig();

        public virtual void SetupData() {}

        public ModuleIgnitionController()
        {
            foreach (var node in GameDatabase.Instance.GetConfigNodes("TWEAKSCALEEXPONENTS"))
            {
                if (node.HasValue("name"))
                {
                    switch (node.GetValue("name"))
                    {
                        case "TweakScale":
                            if (node.HasValue("MassScale")) ScaleExponents[MassScaleExponent] = double.Parse(node.GetValue("MassScale"));
                            if (node.HasValue("DryCost")) ScaleExponents[CostScaleExponent] = double.Parse(node.GetValue("DryCost"));
                            break;
                        case "Part":
                            if (node.HasNode("Resources") && node.GetNode("Resources").HasValue("!amount")) ScaleExponents[VolumeScaleExponent] = double.Parse(node.GetNode("Resources").GetValue("!amount"));
                            break;
                        case "ModuleEngines":
                            if (node.HasValue("maxFuelFlow")) ScaleExponents[EngineThrustScaleExponent] = double.Parse(node.GetValue("maxFuelFlow"));
                            break;
                        case "ModuleRCS":
                            if (node.HasValue("thrusterPower")) ScaleExponents[RCSThrustScaleExponent] = double.Parse(node.GetValue("thrusterPower"));
                            break;
                        default:
                            break;
                    }
                }
            }
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);

            if (PropellantModuleIDs == "")
            {
                var propellantIDs = node.GetValues("propellantModuleID");
                for (int i = 0; i < propellantIDs.Length; i++)
                {
                    PropellantModuleIDs += propellantIDs[i];
                    if (i != propellantIDs.Length - 1) PropellantModuleIDs += ";";
                }
            }

            UpdateAndApply(false);
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);

            UpdateAndApply(true);
        }

        protected void UpdateScaleFactor()
        {
            foreach (var module in part.Modules)
            {
                if (module.ClassName == "TweakScale" && module.Fields.GetValue<bool>("moduleIsEnabled"))
                {
                    ScaleFactor = module.Fields.GetValue<float>("currentScaleFactor");
                    break;
                }
            }
        }

        public void UpdatePropellantConfigs()
        {
            PropellantConfigOriginal = GetPropellantConfig(GetConnectedPropellantModules(true, true));
            PropellantConfigCurrent = GetPropellantConfig(GetConnectedPropellantModules(true, false));
        }

        public virtual bool ShouldUpdateAndApply()
        {
            return true;
        }

        public void UpdateAndApply(bool initialSetup)
        {
            if (!ShouldUpdateAndApply()) return;

            UpdateScaleFactor();
            UnapplyPropellantConfig();
            UpdatePropellantConfigs();
            if (initialSetup) SetupData();
            ApplyPropellantConfig();
        }

        public bool ShouldCheckForUpdateScaleFactor()
        {
            return HighLogic.LoadedSceneIsEditor;
        }

        public void Update()
        {
            if (!ShouldCheckForUpdateScaleFactor()) return;

            UpdateScaleFactor();
            if (ScaleFactor != ScaleFactorPrevious)
            {
                // Only really need to update the stats for display purposes here since TweakScale will have already scaled the volume/thrust,
                // but just to be safe, reapply the config
                UpdateAndApply(false);
                ScaleFactorPrevious = ScaleFactor;
            }
        }

        public double GetScale(string key)
        {
            return ScaleExponents.ContainsKey(key) ? Math.Pow(ScaleFactor, ScaleExponents[key]) : 0;
        }

        public bool IsConnectedToPropellantModule(string moduleID)
        {
            return PropellantModuleIDs.Contains(moduleID);
        }

        protected List<ModuleIgnitionPropellantWrapper> GetConnectedPropellantModules(bool requireGoodResource, bool useOriginalResourceNames)
        {
            var propellantModules = new List<ModuleIgnitionPropellantWrapper>();
            foreach (var module in part.GetComponents<ModuleIgnitionPropellant>())
            {
                var propellantModule = new ModuleIgnitionPropellantWrapper(module, useOriginalResourceNames);
                if (!IsConnectedToPropellantModule(propellantModule.moduleID)) continue;
                var resource = propellantModule.GetResourceName();
                if (requireGoodResource && (resource is null || PartResourceLibrary.Instance.GetDefinition(resource) is null)) continue;
                propellantModules.Add(propellantModule);
            }
            return propellantModules;
        }
    }
}
