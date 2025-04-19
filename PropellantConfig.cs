using System.Collections.Generic;
using System.Linq;
using static Ignition.PropellantConfigUtils;

namespace Ignition
{
    public class PropellantConfig : PropellantConfigBase
    {
        public string ResourceName { get; private set; } = "";
        public bool IsOxidizer { get; private set; } = false;
        public int MixtureConstant { get; private set; } = 0;

        public PropellantConfig(ConfigNode node) : base(node)
        {
            if (node.HasValue("name")) ResourceName = node.GetValue("name");
            if (node.HasValue("IsOxidizer")) IsOxidizer = bool.Parse(node.GetValue("IsOxidizer"));
            if (node.HasValue("MixtureConstant")) MixtureConstant = int.Parse(node.GetValue("MixtureConstant"));
            Propellants.Add(GetPropellant(1, true));
        }

        public PropellantConfig(string resourceName)
        {
            ResourceName = resourceName;
        }

        public Propellant GetPropellant(float ratio, bool drawStackGauge = false, bool ignoreForIsp = false)
        {
            return PropellantConfigUtils.GetPropellant(ResourceName, ratio, drawStackGauge, ignoreForIsp);
        }
    }

    public static class PropellantConfigUtils
    {
        public static Propellant GetPropellant(string resourceName, float ratio, bool drawStackGauge = false, bool ignoreForIsp = false)
        {
            var node = new ConfigNode();
            node.name = "PROPELLANT";
            node.AddValue("name", resourceName);
            node.AddValue("ratio", ratio);
            var propellant = new Propellant();
            propellant.Load(node);
            propellant.displayName = resourceName;
            propellant.drawStackGauge = drawStackGauge;
            propellant.ignoreForIsp = ignoreForIsp;
            return propellant;
        }

        public static PropellantConfigBase GetPropellantConfig(List<ModuleIgnitionPropellantWrapper> propellantModules)
        {
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
                var propellantName = propellantModule.GetResourceName();
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
                    var propellant = GetPropellant(propellantModule.GetResourceName(), propellantModule.ratio, propellantModule.drawStackGauge, propellantModule.ignoreForIsp);
                    propellants.Add(propellant);
                }
                return new PropellantCombinationConfig(propellants);
            }

            // Need to account for ratio, drawStackGauge and ignoreForIsp here

            // If propellants have unspecified ratios, try to find a pre-configured propellant combination
            ConfigNode[] propellantCombinationConfigNodes = GameDatabase.Instance.GetConfigNodes("IgnitionPropellantCombinationConfig");
            var propellantNames = new List<string>();
            foreach (var propellantModule in propellantModules) propellantNames.Add(propellantModule.GetResourceName());
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
                var propellantName = propellantModules.First().GetResourceName();
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
                var propellantModuleResourceName = propellantModules[i].GetResourceName();
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
    }
}