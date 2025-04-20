using UnityEngine;

namespace Ignition
{
    class ModuleIgnitionTankController : ModuleIgnitionController, IPartMassModifier, IPartCostModifier
    {
        [KSPField(isPersistant = true)]
        public float volume = 0;

        [KSPField(isPersistant = true)]
        public float addedMass = 0;

        [KSPField(isPersistant = true)]
        public float currentAddedMass = 0;
        public float GetModuleMass(float baseMass, ModifierStagingSituation situation) => currentAddedMass;
        public ModifierChangeWhen GetModuleMassChangeWhen() => ModifierChangeWhen.FIXED;

        [KSPField(isPersistant = true)]
        public float addedCost = 0;

        [KSPField(isPersistant = true)]
        public float currentAddedCost;
        public float GetModuleCost(float baseCost, ModifierStagingSituation situation) => currentAddedCost;
        public ModifierChangeWhen GetModuleCostChangeWhen() => ModifierChangeWhen.FIXED;

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
        }

        public override void OnStart(StartState state)
        {
            ApplyPropellantConfig();
        }

        private float GetUnitVolume(string resourceName)
        {
            if (resourceName == "LiquidFuel") return 5f;
            if (resourceName == "Oxidizer") return 5f;
            if (resourceName == "MonoPropellant") return 4f;
            return 1f;
        }

        private float GetTankMass(float volume, float resourceDensity)
        {
            return volume * Mathf.Round(2500000 * Mathf.Pow(resourceDensity, 2 / 3f)) / 200000000;
        }

        private void AddResource(Propellant propellant, float volume)
        {
            var resourceDefinition = PartResourceLibrary.Instance.GetDefinition(propellant.name);
            var unitVolume = GetUnitVolume(propellant.name);
            var density = resourceDefinition.density / unitVolume;

            var maxAmount = volume / unitVolume;
            var amount = float.MaxValue;
            if (part.Resources.Contains(propellant.name))
            {
                var previousMaxAmount = part.Resources.Get(propellant.name).maxAmount;
                var previousAmount = part.Resources.Get(propellant.name).amount;
                if (previousAmount < previousMaxAmount) amount = (float)part.Resources.Get(propellant.name).amount;
            }
            if (amount > maxAmount) amount = maxAmount;

            currentAddedMass = addedMass + GetTankMass(volume, density);
            currentAddedCost = addedCost + amount * resourceDefinition.unitCost;

            var resourceNode = new ConfigNode();
            resourceNode.name = "RESOURCE";
            resourceNode.AddValue("name", propellant.name);
            resourceNode.AddValue("amount", amount);
            resourceNode.AddValue("maxAmount", maxAmount);
            part.SetResource(resourceNode);
        }

        public override void ApplyPropellantConfig()
        {
            if (PropellantConfigCurrent is null) return;

            if (PropellantConfigCurrent.Propellants.Count == 0) return;

            var totalRatio = 0f;
            foreach (var propellant in PropellantConfigCurrent.Propellants) totalRatio += propellant.ratio;
            foreach (var propellant in PropellantConfigCurrent.Propellants) AddResource(propellant, volume * propellant.ratio / totalRatio);
        }

        public override string GetInfo()
        {
            return "<b>Volume: </b>" + KSPUtil.LocalizeNumber(volume, "F1") + " L";
        }

        public override string GetModuleDisplayName()
        {
            return "Propellant tank";
        }
    }
}