using System;
using System.Collections.Generic;
using System.Linq;

namespace Ignition
{
    public static class PropellantConfigUtils
    {
        public static Propellant CreatePropellant(string resourceName, double ratio, bool drawStackGauge = false, bool ignoreForIsp = false)
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

        public static PropellantConfig GetPropellantConfig(string resourceName)
        {
            var allPropellantConfigNodes = GameDatabase.Instance.GetConfigNodes("IgnitionPropellantConfig");
            var allPropellantConfigs = new Dictionary<string, PropellantConfig>();
            foreach (var propellantConfigNode in allPropellantConfigNodes)
            {
                if (propellantConfigNode.HasValue("name") && propellantConfigNode.GetValue("name") == resourceName)
                {
                    return new PropellantConfig(propellantConfigNode);
                }
            }
            return null;
        }

        public static Dictionary<string, PropellantConfig> GetAllPropellantConfigs()
        {
            var allPropellantConfigNodes = GameDatabase.Instance.GetConfigNodes("IgnitionPropellantConfig");
            var allPropellantConfigs = new Dictionary<string, PropellantConfig>();
            foreach (var propellantConfigNode in allPropellantConfigNodes)
            {
                if (propellantConfigNode.HasValue("name"))
                {
                    allPropellantConfigs[propellantConfigNode.GetValue("name")] = new PropellantConfig(propellantConfigNode);
                }
            }
            return allPropellantConfigs;
        }

        public static List<PropellantCombinationConfig> GetAllPropellantCombinationConfigs()
        {
            var propellantCombinationConfigNodes = GameDatabase.Instance.GetConfigNodes("IgnitionPropellantCombinationConfig");
            var allPropellantCombinationConfigs = new List<PropellantCombinationConfig>();
            foreach (var propellantCombinationConfigNode in propellantCombinationConfigNodes)
            {
                allPropellantCombinationConfigs.Add(new PropellantCombinationConfig(propellantCombinationConfigNode));
            }
            return allPropellantCombinationConfigs;
        }

        public static PropellantConfigBase GetOrCreatePropellantConfig(List<ModuleIgnitionPropellantWrapper> propellantModules)
        {
            if (propellantModules.Count == 0) return null;

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
                    var propellant = CreatePropellant(propellantModule.GetResourceName(), propellantModule.ratio, propellantModule.drawStackGauge, propellantModule.ignoreForIsp);
                    propellants.Add(propellant);
                }
                return new PropellantCombinationConfig(propellants);
            }

            // If propellants have unspecified ratios, try to find a pre-configured propellant combination
            var propellantNames = new List<string>();
            foreach (var propellantModule in propellantModules)
            {
                if (!propellantModule.ignoreForIsp) propellantNames.Add(propellantModule.GetResourceName());
            }
            return GetOrCreatePropellantConfig(propellantNames);
        }

        public static PropellantConfigBase GetOrCreatePropellantConfig(List<string> propellantNames)
        {
            if (propellantNames.Count == 0) return null;

            // Try to find a config for the given combination
            var allPropellantCombinationConfigs = GetAllPropellantCombinationConfigs();
            foreach (var propellantCombinationConfig in allPropellantCombinationConfigs)
            {
                if (propellantCombinationConfig.Propellants.Count != propellantNames.Count) continue;

                var configPropellantNames = new HashSet<string>();
                foreach (var propellant in propellantCombinationConfig.Propellants) configPropellantNames.Add(propellant.name);

                if (configPropellantNames.SetEquals(propellantNames)) return propellantCombinationConfig;
            }
            
            // Gather individual configs for each propellant
            var allPropellantConfigs = GetAllPropellantConfigs();
            var propellantConfigs = new List<PropellantConfig>();
            foreach (var propellantName in propellantNames)
            {
                foreach (var propellantConfig in allPropellantConfigs.Values)
                {
                    if (propellantConfig.Propellants.Count != 1) continue; // Should only be single propellants here, but just in case
                    if (propellantConfig.ResourceName == propellantName) propellantConfigs.Add(propellantConfig);
                    if (propellantConfigs.Count == propellantNames.Count) break;
                }
            }
            if (propellantConfigs.Count < propellantNames.Count) return null;
            if (propellantConfigs.Count > 2) return null;

            // If there is only one propellant, return a simple config
            if (propellantNames.Count == 1) return propellantConfigs.First();

            // Otherwise generate a new combination, which we can only do with a pair of fuel and oxidizer
            PropellantConfig fuelConfig = null;
            PropellantConfig oxidizerConfig = null;
            var success = GetFuelAndOxidizer(propellantConfigs, ref fuelConfig, ref oxidizerConfig);
            if (success) return new PropellantCombinationConfig(fuelConfig, oxidizerConfig);

            return null;
        }

        public static bool GetFuelAndOxidizer(List<PropellantConfig> propellantConfigs, ref PropellantConfig fuelConfig, ref PropellantConfig oxidizerConfig)
        {
            if (propellantConfigs.Count != 2) return false;

            fuelConfig = null;
            oxidizerConfig = null;

            foreach (var propellantConfig in propellantConfigs)
            {
                if (propellantConfig.IsOxidizer) oxidizerConfig = propellantConfig;
                else fuelConfig = propellantConfig;
            }

            return !(fuelConfig is null) && !(oxidizerConfig is null);
        }

        public static double GetUnitVolume(string resourceName)
        {
            if (resourceName == "LiquidFuel") return 5;
            if (resourceName == "Oxidizer") return 5;
            if (resourceName == "MonoPropellant") return 4;
            return 1;
        }

        public static double ComputeTankDensity(string resourceName)
        {
            var resourceDefinition = PartResourceLibrary.Instance.GetDefinition(resourceName);
            var unitVolume = GetUnitVolume(resourceName);
            var density = resourceDefinition.density / unitVolume;
            return Math.Round(2500000 * Math.Pow(density, 2 / 3.0)) / 200000000;
        }

        public static string GetPropellantRatiosString(List<Propellant> propellants, List<string> configuredPropellantNames = null)
        {
            if (propellants.Count == 0) return "";
            if (propellants.Count == 1 && !(propellants[0] is null) && !(propellants[0].resourceDef is null)) return propellants[0].resourceDef.displayName;

            var totalRatio = 0.0;
            foreach (var propellant in propellants) totalRatio += propellant.ratio;

            if (configuredPropellantNames is null)
            {
                configuredPropellantNames = new List<string>();
                foreach (var propellant in propellants) configuredPropellantNames.Add(propellant.name);
            }

            var multiplier = 1;
            var totalConfiguredRatio = 0.0;
            foreach (var propellant in propellants)
            {
                if (configuredPropellantNames.Contains(propellant.name)) totalConfiguredRatio += propellant.ratio;
            }

            for (int d = 1; d < 21; d++)
            {
                bool found = true;
                foreach (var propellant in propellants)
                {
                    if (!configuredPropellantNames.Contains(propellant.name)) continue;
                    if (((propellant.ratio / totalConfiguredRatio * d + 1e-5) % 1) < 2e-5) continue;

                    found = false;
                    break;
                }
                if (found)
                {
                    multiplier = d;
                    break;
                }
            }

            var propellantsString = "";
            for (int i = 0; i < propellants.Count; i++)
            {
                var propellant = propellants[i];
                if (propellant is null) continue;
                var ratio = Math.Round(1e4 * multiplier * propellant.ratio / totalRatio) / 1e4;
                if (multiplier > 1 && configuredPropellantNames.Contains(propellant.name)) ratio = Math.Round(ratio);
                if (!(propellant.resourceDef is null)) propellantsString += ratio + " " + propellant.resourceDef.displayName;
                if (i < propellants.Count - 1) propellantsString += " : ";
            }

            return propellantsString;
        }
    }
}