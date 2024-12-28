using System.Collections.Generic;
using System.Linq;
using UnityEngine;

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

        private PropellantConfigBase _originalPropellantConfig = null;
        private PropellantConfigBase OriginalPropellantConfig
        {
            get
            {
                if (_originalPropellantConfig is null)
                {
                    var propellantNames = new HashSet<string>();
                    foreach (var propellantModule in GetConnectedPropellantModules())
                    {
                        propellantNames.Add(propellantModule.resourceNameOriginal);
                    }
                    _originalPropellantConfig = GetPropellantConfig(propellantNames);
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
                    var propellantNames = new HashSet<string>();
                    foreach (var propellantModule in GetConnectedPropellantModules())
                    {
                        propellantNames.Add(propellantModule.resourceName);
                    }
                    _currentPropellantConfig = GetPropellantConfig(propellantNames);
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

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);

            if (!(removeResource is null) && part.Resources.Contains(removeResource))
            {
                var resourceAmount = (float)part.Resources.Get(removeResource).maxAmount;
                if (MassOriginal == 0)
                {
                    var resourceDefinition = PartResourceLibrary.Instance.GetDefinition(removeResource);
                    if (resourceDefinition.density > 0)
                    {
                        var unitsPerVolume = GetUnitsPerVolume(removeResource);
                        var density = resourceDefinition.density * (unitsPerVolume / resourceDefinition.volume);
                        var volume = resourceAmount / unitsPerVolume;
                        MassOriginal = GetTankMass(volume, density);
                        CostOriginal = resourceAmount * resourceDefinition.unitCost;
                    }
                }

                part.Resources.Remove(removeResource);
            }

            if (resourceNameOriginal is null)
            {
                resourceNameOriginal = resourceName;
                resourceNamePrevious = resourceName;
            }

            if (!(resourceNameOriginal is null)) part.RemoveResource(resourceNameOriginal);
            if (!(resourceNamePrevious is null)) part.RemoveResource(resourceNamePrevious);
            resourceNamePrevious = resourceName;

            _originalPropellantConfig = null;
            _currentPropellantConfig = null;

            ApplyPropellantCombinationToResources();

            var engineModule = GetEngineModule();
            if (!(engineModule is null) && !engineModule.useVelCurve)
            {
                if (EngineMaxThrustOriginal == 0)
                {
                    EngineMaxThrustOriginal = engineModule.maxThrust;
                    EngineIspVacuumOriginal = engineModule.atmosphereCurve.Curve.keys[0].value;
                    EngineIspSeaLevelOriginal = engineModule.atmosphereCurve.Curve.keys[1].value;
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

        private PropellantConfigBase GetPropellantConfig(HashSet<string> propellantNames)
        {
            ConfigNode[] propellantConfigNodes = GameDatabase.Instance.GetConfigNodes("FuelMixerPropellantConfig");

            // If there is only one propellant, return a simple config
            if (propellantNames.Count == 1)
            {
                foreach (var propellantConfigNode in propellantConfigNodes)
                {
                    var propellantConfig = new PropellantConfig(propellantConfigNode);
                    if (propellantConfig.ResourceName == propellantNames.First()) return propellantConfig;
                }
                return null;
            }            

            // If there are multiple propellants, try to find a pre-configured propellant combination
            ConfigNode[] propellantCombinationConfigNodes = GameDatabase.Instance.GetConfigNodes("FuelMixerPropellantCombinationConfig");
            foreach (var propellantCombinationConfigNode in propellantCombinationConfigNodes)
            {
                var propellantCombinationConfig = new PropellantCombinationConfig(propellantCombinationConfigNode);
                if (propellantCombinationConfig.Propellants.Count != propellantNames.Count) continue;

                var configPropellantNames = new HashSet<string>();
                foreach (var propellant in propellantCombinationConfig.Propellants) configPropellantNames.Add(propellant.name);

                if (configPropellantNames.SetEquals(propellantNames)) return propellantCombinationConfig;
            }

            // Otherwise generate a new combination, which we can only do with a pair of fuel and oxidizer
            if (propellantNames.Count != 2) return null;

            PropellantConfig fuelConfig = null;
            PropellantConfig oxidizerConfig = null;
            foreach (var propellantConfigNode in propellantConfigNodes)
            {
                var propellantConfig = new PropellantConfig(propellantConfigNode);
                if (propellantNames.Contains(propellantConfig.ResourceName))
                {
                    if (propellantConfig.PropellantType == "fuel") fuelConfig = propellantConfig;
                    else if (propellantConfig.PropellantType == "oxidizer") oxidizerConfig = propellantConfig;
                    if (!(fuelConfig is null) && !(oxidizerConfig is null)) break;
                }
            }
            if (fuelConfig is null || oxidizerConfig is null) return null;
            
            return new PropellantCombinationConfig(fuelConfig, oxidizerConfig);
        }

        private List<ModuleFuelMixerPropellant> GetConnectedPropellantModules()
        {
            var propellantModules = new List<ModuleFuelMixerPropellant>();
            foreach (var module in part.GetComponents<ModuleFuelMixerPropellant>())
            {
                if (module.engineID == engineID) propellantModules.Add(module);
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
            foreach (var propellantModule in GetConnectedPropellantModules())
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

        EngineData GetEngineData(float maxThrustOriginal, float ispVacuumOriginal, float ispSeaLevelOriginal, float g)
        {
            EngineData engineData;

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

            var ispSeaLevelMultiplier = 1 + (ispVacuumMultiplier - 1) * (2.7f * ispVacuumOriginal / ispSeaLevelOriginal - 1.7f);
            ispSeaLevelMultiplier = Mathf.Round(ispSeaLevelMultiplier * 100) / 100;
            var ispSeaLevelChange = Mathf.Round(ispSeaLevelOriginal * (ispSeaLevelMultiplier - 1));
            if (Mathf.Abs(ispSeaLevelChange) > 10) ispSeaLevelChange = Mathf.Round(ispSeaLevelChange / 5) * 5;
            var ispSeaLevel = ispSeaLevelOriginal + ispSeaLevelChange;

            engineData.maxThrust = maxThrust;
            engineData.maxFuelFlow = maxThrust / (g * ispVacuum);
            engineData.ispKeys = new Keyframe[3]
            {
                new Keyframe(0, ispVacuum), new Keyframe(1, ispSeaLevel), new Keyframe(12, 0.001f)
            };

            return engineData;
        }

        private void ApplyPropellantCombinationToEngineModule()
        {
            if (OriginalPropellantConfig is null || CurrentPropellantConfig is null) return;

            var engineModule = GetEngineModule();
            if (engineModule is null) return;

            var engineData = GetEngineData(EngineMaxThrustOriginal, EngineIspVacuumOriginal, EngineIspSeaLevelOriginal, engineModule.g);

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

            var engineData = GetEngineData(RCSThrusterPowerOriginal, RCSIspSeaLevelOriginal, RCSIspVacuumOriginal, (float)rcsModule.G);

            rcsModule.thrusterPower = engineData.maxThrust;
            rcsModule.maxFuelFlow = engineData.maxFuelFlow;
            rcsModule.atmosphereCurve.Curve.keys = engineData.ispKeys;
            rcsModule.propellants = CurrentPropellantConfig.Propellants;
        }
    }
}