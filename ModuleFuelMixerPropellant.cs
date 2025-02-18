using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static FuelMixer.PropellantCombinationConfig;

namespace FuelMixer
{
    public class ModuleFuelMixerPropellant : PartModule, IPartMassModifier, IPartCostModifier
    {
        [KSPField(isPersistant = true)]
        public string moduleID;

        [KSPField(isPersistant = true)]
        public string resourceName;

        [KSPField(isPersistant = true)]
        public string resourceNamePrevious = null;

        [KSPField(isPersistant = true)]
        public string resourceNameOriginal = null;

        string GetResourceName(bool useOriginal)
        {
            if (useOriginal) return resourceNameOriginal;
            return resourceName;
        }

        [KSPField(isPersistant = true)]
        public float ratio = 0;

        [KSPField(isPersistant = true)]
        public bool drawStackGauge = false;

        [KSPField(isPersistant = true)]
        public bool ignoreForIsp = false;

        private PropellantConfigBase _originalPropellantConfig = null;
        private PropellantConfigBase OriginalPropellantConfig
        {
            get
            {
                if (_originalPropellantConfig is null)
                {
                    _originalPropellantConfig = GetPropellantConfig(true);
                }
                return _originalPropellantConfig;
            }
        }

        private PropellantConfigBase _currentPropellantConfig = null;
        private PropellantConfigBase CurrentPropellantConfig
        {
            get
            {
                if (_currentPropellantConfig is null)
                {
                    _currentPropellantConfig = GetPropellantConfig(false);
                }
                return _currentPropellantConfig;
            }
        }

        // Tank
        [KSPField(isPersistant = true)]
        public string removeResource = null;

        [KSPField(isPersistant = true)]
        public float addedVolume = 0;

        [KSPField(isPersistant = true)]
        public float MassOriginal = 0;
        public float MassCurrent = 0;
        public float GetModuleMass(float baseMass, ModifierStagingSituation situation) => MassCurrent;
        public ModifierChangeWhen GetModuleMassChangeWhen() => ModifierChangeWhen.FIXED;

        [KSPField(isPersistant = true)]
        public float CostOriginal;
        public float CostCurrent;
        public float GetModuleCost(float baseCost, ModifierStagingSituation situation) => CostCurrent;
        public ModifierChangeWhen GetModuleCostChangeWhen() => ModifierChangeWhen.FIXED;

        // Engine
        [KSPField(isPersistant = true)]
        public string engineID = "";

        [KSPField(isPersistant = true)]
        public float EngineMaxThrustOriginal = 0;

        [KSPField(isPersistant = true)]
        public float EngineIspVacuumOriginal = 0;

        [KSPField(isPersistant = true)]
        public float EngineIspSeaLevelOriginal = 0;

        // RCS
        [KSPField(isPersistant = true)]
        public float RCSThrusterPowerOriginal = 0;

        [KSPField(isPersistant = true)]
        public float RCSIspVacuumOriginal = 0;

        [KSPField(isPersistant = true)]
        public float RCSIspSeaLevelOriginal = 0;

        private void AbsorbMassAndCost(string resource)
        {
            if (resource is null) return;

            if (!part.Resources.Contains(resource)) return;

            var resourceAmount = (float)part.Resources.Get(resource).maxAmount;
            var resourceDefinition = PartResourceLibrary.Instance.GetDefinition(resource);

            if (resourceAmount > 0 && resourceDefinition.density > 0)
            {
                var unitsPerVolume = GetUnitsPerVolume(resource);
                var density = resourceDefinition.density * (unitsPerVolume / resourceDefinition.volume);
                var volume = resourceAmount / unitsPerVolume;
                MassOriginal += GetTankMass(volume, density);
                CostOriginal += resourceAmount * resourceDefinition.unitCost;
            }
        }

        private void RemoveResource(string resource)
        {
            if (resource is null) return;

            part.Resources.Remove(resource);
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);

            bool massAndCostSet = MassOriginal != 0;

            foreach (var resource in new []{ removeResource, resourceNameOriginal, resourceNamePrevious })
            {
                if (!massAndCostSet) AbsorbMassAndCost(resource);
                RemoveResource(resource);
            }
            resourceNamePrevious = resourceName;

            _originalPropellantConfig = null;
            _currentPropellantConfig = null;

            ApplyPropellantCombinationToResources();

            var engineModule = GetEngineModule();
            if (!(engineModule is null))
            {
                if (EngineMaxThrustOriginal == 0 && engineModule.atmosphereCurve.Curve.keys.Length > 0)
                {
                    EngineMaxThrustOriginal = engineModule.maxThrust;
                    int thing = engineModule.atmosphereCurve.Curve.keys.Length;
                    EngineIspVacuumOriginal = engineModule.atmosphereCurve.Curve.keys[0].value;
                    if (!engineModule.useVelCurve)
                    {
                        EngineIspSeaLevelOriginal = engineModule.atmosphereCurve.Curve.keys[1].value;
                    }
                }

                ApplyPropellantCombinationToEngineModule();
            }

            var rcsModule = GetRCSModule();
            if (!(rcsModule is null))
            {
                if (RCSThrusterPowerOriginal == 0)
                {
                    RCSThrusterPowerOriginal = rcsModule.thrusterPower;
                    RCSIspVacuumOriginal = rcsModule.atmosphereCurve.Curve.keys[0].value;
                    RCSIspSeaLevelOriginal = rcsModule.atmosphereCurve.Curve.keys[1].value;
                }

                ApplyPropellantCombinationToRCS();
            }
        }

        private ModuleEngines GetEngineModule()
        {
            var allEngineModules = part.FindModulesImplementing<ModuleEngines>();
            if (engineID != "")
            {
                foreach (var engineModule in allEngineModules)
                {
                    if (engineModule.engineID == engineID)
                    {
                        return engineModule;
                    }
                }
            }
            else if (allEngineModules.Count > 0)
            {
                return allEngineModules.FirstOrDefault();
            }
            return null;
        }

        private ModuleRCS GetRCSModule()
        {
            var rcsModules = part.FindModulesImplementing<ModuleRCS>();
            if (rcsModules.Count > 0) return rcsModules.FirstOrDefault();
            return null;
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

        private PropellantConfigBase GetPropellantConfig(bool useOriginalResourceNames)
        {
            var propellantModules = GetConnectedPropellantModules(true, useOriginalResourceNames);
            
            var propellantConfigNodes = GameDatabase.Instance.GetConfigNodes("FuelMixerPropellantConfig");
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
            ConfigNode[] propellantCombinationConfigNodes = GameDatabase.Instance.GetConfigNodes("FuelMixerPropellantCombinationConfig");
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

        private List<ModuleFuelMixerPropellant> GetConnectedPropellantModules(bool requireGoodResource, bool useOriginalResourceNames)
        {
            var propellantModules = new List<ModuleFuelMixerPropellant>();
            foreach (var module in part.GetComponents<ModuleFuelMixerPropellant>())
            {
                if (module.engineID != engineID) continue;
                var resource = module.GetResourceName(useOriginalResourceNames);
                if (requireGoodResource && (resource is null || PartResourceLibrary.Instance.GetDefinition(resource) is null)) continue;
                propellantModules.Add(module);
            }
            return propellantModules;
        }

        private float GetUnitsPerVolume(string resourceName)
        {
            // Unit volume = 5 litres, as per B9PartSwitch
            if (resourceName == "LiquidFuel" || resourceName == "Oxidizer") return 1f;
            if (resourceName == "MonoPropellant") return 1.5f;
            return 5f;
        }

        private float GetTankMass(float volume, float resourceDensity)
        {
            return volume * Mathf.Round(2500000 * Mathf.Pow(resourceDensity, 2 / 3f)) / 40000000;
        }

        private void ApplyPropellantCombinationToResources()
        {
            if (OriginalPropellantConfig is null || CurrentPropellantConfig is null) return;

            var totalVolume = 0f;
            foreach (var propellantModule in GetConnectedPropellantModules(false, true))
            {
                totalVolume += propellantModule.addedVolume;
            }
            if (totalVolume == 0) return;

            var totalRatio = 0f;
            foreach (var propellant in CurrentPropellantConfig.Propellants) totalRatio += propellant.ratio;

            MassCurrent = -MassOriginal;
            CostCurrent = -CostOriginal;
            foreach (var propellant in CurrentPropellantConfig.Propellants)
            {
                var resourceDefinition = PartResourceLibrary.Instance.GetDefinition(propellant.name);
                var unitsPerVolume = GetUnitsPerVolume(propellant.name);
                var volume = totalVolume * propellant.ratio / totalRatio;
                var maxAmount = volume * unitsPerVolume;
                var density = resourceDefinition.density * (unitsPerVolume / resourceDefinition.volume);
                if (propellant.name == resourceName)
                {
                    MassCurrent += GetTankMass(volume, density);
                    CostCurrent += maxAmount * resourceDefinition.unitCost;
                }

                var resourceNode = new ConfigNode();
                resourceNode.name = "RESOURCE";
                resourceNode.AddValue("name", propellant.name);
                resourceNode.AddValue("amount", maxAmount);
                resourceNode.AddValue("maxAmount", maxAmount);
                part.SetResource(resourceNode);
            }
        }

        struct EngineData
        {
            public float maxThrust;
            public float maxFuelFlow;
            public Keyframe[] ispKeys;
        }

        EngineData GetEngineData(float maxThrustOriginal, float ispVacuumOriginal, float ispSeaLevelOriginal, float g, bool useVelCurve)
        {
            var thrustMultiplier = CurrentPropellantConfig.ThrustMultiplier / OriginalPropellantConfig.ThrustMultiplier;
            thrustMultiplier = Mathf.Round(thrustMultiplier * 100) / 100;
            var thrustChange = Mathf.Round(maxThrustOriginal * (thrustMultiplier - 1) / 0.1f) * 0.1f;
            if (Mathf.Abs(thrustChange) > 5) thrustChange = Mathf.Round(thrustChange);
            if (Mathf.Abs(thrustChange) > 20) thrustChange = Mathf.Round(thrustChange / 5) * 5;
            var maxThrust = maxThrustOriginal + thrustChange;

            var ispVacuumMultiplier = CurrentPropellantConfig.IspMultiplier / OriginalPropellantConfig.IspMultiplier;
            ispVacuumMultiplier = Mathf.Round(ispVacuumMultiplier * 100) / 100;
            var ispVacuumChange = Mathf.Round(ispVacuumOriginal * (ispVacuumMultiplier - 1));
            if (Mathf.Abs(ispVacuumChange) > 10) ispVacuumChange = Mathf.Round(ispVacuumChange / 5) * 5;
            var ispVacuum = ispVacuumOriginal + ispVacuumChange;
            var ispKeys = new List<Keyframe> { new Keyframe(0, ispVacuum) };

            if (!useVelCurve)
            {
                //var ispSeaLevelMultiplier = 1 + (ispVacuumMultiplier - 1) * (2.7f * ispVacuumOriginal / ispSeaLevelOriginal - 1.7f);
                var ispSeaLevelMultiplier = Mathf.Pow(ispVacuumMultiplier, 1 / thrustMultiplier);
                if (ispSeaLevelMultiplier < 0) ispSeaLevelMultiplier = 0;
                ispSeaLevelMultiplier = Mathf.Round(ispSeaLevelMultiplier * 100) / 100;
                var ispSeaLevelChange = Mathf.Round(ispSeaLevelOriginal * (ispSeaLevelMultiplier - 1));
                if (Mathf.Abs(ispSeaLevelChange) > 10) ispSeaLevelChange = Mathf.Round(ispSeaLevelChange / 5) * 5;
                var ispSeaLevel = ispSeaLevelOriginal + ispSeaLevelChange;
                ispKeys.Add(new Keyframe(1, ispSeaLevel));
                ispKeys.Add(new Keyframe(12, 0.001f));
            }

            EngineData engineData;
            engineData.maxThrust = maxThrust;
            engineData.maxFuelFlow = maxThrust / (g * ispVacuum);
            engineData.ispKeys = ispKeys.ToArray();

            return engineData;
        }

        private void ApplyPropellantCombinationToEngineModule()
        {
            if (OriginalPropellantConfig is null || CurrentPropellantConfig is null) return;

            var engineModule = GetEngineModule();
            if (engineModule is null) return;

            var engineData = GetEngineData(EngineMaxThrustOriginal, EngineIspVacuumOriginal, EngineIspSeaLevelOriginal, engineModule.g, engineModule.useVelCurve);

            engineModule.maxThrust = engineData.maxThrust;
            engineModule.maxFuelFlow = engineData.maxFuelFlow;
            engineModule.atmosphereCurve.Curve.keys = engineData.ispKeys;
            engineModule.propellants = CurrentPropellantConfig.Propellants;
            engineModule.SetupPropellant();
        }

        private void ApplyPropellantCombinationToRCS()
        {
            if (OriginalPropellantConfig is null || CurrentPropellantConfig is null) return;

            var rcsModule = GetRCSModule();
            if (rcsModule is null) return;

            var engineData = GetEngineData(RCSThrusterPowerOriginal, RCSIspSeaLevelOriginal, RCSIspVacuumOriginal, (float)rcsModule.G, false);

            rcsModule.thrusterPower = engineData.maxThrust;
            rcsModule.maxFuelFlow = engineData.maxFuelFlow;
            rcsModule.atmosphereCurve.Curve.keys = engineData.ispKeys;
            rcsModule.propellants = CurrentPropellantConfig.Propellants;
        }
    }
}