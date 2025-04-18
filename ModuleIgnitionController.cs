using System.Collections.Generic;
using System.Linq;

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

        public abstract void ApplyPropellantConfig();
        public abstract void RecompilePartInfo();

        public virtual void SetupData() {}

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
        
        public override void OnAwake()
        {
            base.OnAwake();

            UpdateAndApply(true);
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);

            UpdateAndApply(true);
        }

        private void UpdateAndApply(bool initialSetup)
        {
            UpdatePropellantConfigs();
            if (initialSetup) SetupData();
            ApplyPropellantConfig();
            RecompilePartInfo();
        }

        public void UpdatePropellantConfigs()
        {
            PropellantConfigOriginal = GetPropellantConfig(true);
            PropellantConfigCurrent = GetPropellantConfig(false);
        }

        private Propellant GetPropellant(string resourceName, float ratio, bool drawStackGauge = false, bool ignoreForIsp = false)
        {
            var node = new ConfigNode();
            node.name = "PROPELLANT";
            node.AddValue("name", resourceName);
            node.AddValue("ratio", ratio);
            node.AddValue("drawStackGauge", drawStackGauge);
            node.AddValue("ignoreForIsp", ignoreForIsp);
            var propellant = new Propellant();
            propellant.Load(node);
            propellant.displayName = resourceName;
            return propellant;
        }

        protected PropellantConfigBase GetPropellantConfig(bool useOriginalResourceNames)
        {
            var propellantModules = GetConnectedPropellantModules(true, useOriginalResourceNames);

            var propellantConfigNodes = GameDatabase.Instance.GetConfigNodes("IgnitionPropellantConfig");
            var propellantConfigs = new Dictionary<string, PropellantConfig>();
            foreach (var propellantConfigNode in propellantConfigNodes)
            {
                var propellantConfig = new PropellantConfig(propellantConfigNode);
                propellantConfigs[propellantConfig.ResourceName] = propellantConfig;
            }
            foreach (var propellantModule in propellantModules)
            {
                // Dummy config for undefined propellants
                var propellantName = propellantModule.GetResourceName(useOriginalResourceNames);
                if (!propellantConfigs.ContainsKey(propellantName)) propellantConfigs[propellantName] = new PropellantConfig(propellantName);
            }

            // If propellants have specified ratios, construct the combination
            bool ratiosSpecified = true;
            foreach (var propellantModule in propellantModules)
            {
                if (propellantModule.ratio == 0)
                {
                    ratiosSpecified = false;
                    break;
                }
            }
            if (ratiosSpecified)
            {
                var propellants = new List<Propellant>();
                foreach (var propellantModule in propellantModules)
                {
                    var propellant = GetPropellant(propellantModule.GetResourceName(useOriginalResourceNames), propellantModule.ratio, propellantModule.drawStackGauge, propellantModule.ignoreForIsp);
                    propellants.Add(propellant);
                }
                return new PropellantCombinationConfig(propellants);
            }

            // Need to account for ratio, drawStackGauge and ignoreForIsp here

            // If propellants have unspecified ratios, try to find a pre-configured propellant combination
            ConfigNode[] propellantCombinationConfigNodes = GameDatabase.Instance.GetConfigNodes("IgnitionPropellantCombinationConfig");
            var propellantNames = new List<string>();
            foreach (var propellantModule in propellantModules) propellantNames.Add(propellantModule.GetResourceName(useOriginalResourceNames));
            foreach (var propellantCombinationConfigNode in propellantCombinationConfigNodes)
            {
                var propellantCombinationConfig = new PropellantCombinationConfig(propellantCombinationConfigNode);
                if (propellantCombinationConfig.Propellants.Count != propellantNames.Count) continue;

                var configPropellantNames = new HashSet<string>();
                foreach (var propellant in propellantCombinationConfig.Propellants) configPropellantNames.Add(propellant.name);

                if (configPropellantNames.SetEquals(propellantNames)) return propellantCombinationConfig;
            }

            // If there is only one propellant, return a simple config
            if (propellantModules.Count == 1)
            {
                var propellantName = propellantModules.First().GetResourceName(useOriginalResourceNames);
                if (!(propellantName is null) && propellantConfigs.ContainsKey(propellantName)) return propellantConfigs[propellantName];
                return null;
            }

            // Otherwise generate a new combination, which we can only do with a pair of fuel and oxidizer
            if (propellantModules.Count != 2) return null;
            // Both the fuel and oxidizer must be used in engine data computation
            if (propellantModules[0].ignoreForIsp || propellantModules[1].ignoreForIsp) return null;
            // If both have ratios set, propellants should have been created directly above
            // If either one has a ratio set, just ignore it (e.g. the fuel in a jet-rocket multimode engine)
            if (propellantModules[0].ratio != 0 && propellantModules[1].ratio != 0) return null;

            PropellantConfig fuelConfig = null;
            PropellantConfig oxidizerConfig = null;

            for (int i = 0; i < 2; i++)
            {
                var propellantModuleResourceName = propellantModules[i].GetResourceName(useOriginalResourceNames);
                foreach (var propellantConfig in propellantConfigs.Values)
                {
                    if (propellantConfig.Propellants.Count != 1) continue; // Should only be single propellants here, but just in case

                    if (propellantConfig.Propellants.First().name == propellantModuleResourceName)
                    {
                        if (propellantConfig.IsOxidizer) oxidizerConfig = propellantConfig;
                        else fuelConfig = propellantConfig;
                        break;
                    }
                }
            }
            if (fuelConfig is null || oxidizerConfig is null) return null;

            return new PropellantCombinationConfig(fuelConfig, oxidizerConfig);
        }

        public bool IsConnectedToPropellantModule(string moduleID)
        {
            return PropellantModuleIDs.Contains(moduleID);
        }

        protected List<ModuleIgnitionPropellant> GetConnectedPropellantModules(bool requireGoodResource, bool useOriginalResourceNames)
        {
            var propellantModules = new List<ModuleIgnitionPropellant>();
            foreach (var propellantModule in part.GetComponents<ModuleIgnitionPropellant>())
            {
                if (!IsConnectedToPropellantModule(propellantModule.moduleID)) continue;
                var resource = propellantModule.GetResourceName(useOriginalResourceNames);
                if (requireGoodResource && (resource is null || PartResourceLibrary.Instance.GetDefinition(resource) is null)) continue;
                propellantModules.Add(propellantModule);
            }
            return propellantModules;
        }
    }
}
