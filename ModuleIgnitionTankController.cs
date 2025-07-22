using System.Collections.Generic;
using UnityEngine;

namespace Ignition
{
    class ModuleIgnitionTankController : ModuleIgnitionController, IPartMassModifier, IPartCostModifier
    {
        [KSPField(isPersistant = true)]
        public float volume = 0;
        private float VolumeScaled => GetScale(VolumeScaleExponent) * volume;

        [KSPField(isPersistant = true)]
        public float addedMass = 0;
        public float AddedMassScaled => GetScale(MassScaleExponent) * addedMass;

        [KSPField(isPersistant = true)]
        public float currentAddedMass = 0;
        public float GetModuleMass(float baseMass, ModifierStagingSituation situation) => currentAddedMass;
        public ModifierChangeWhen GetModuleMassChangeWhen() => ModifierChangeWhen.FIXED;

        [KSPField(isPersistant = true)]
        public float addedCost = 0;
        public float AddedCostScaled => GetScale(CostScaleExponent) * addedCost;

        [KSPField(isPersistant = true)]
        public float currentAddedCost = 0;
        public float GetModuleCost(float baseCost, ModifierStagingSituation situation) => currentAddedCost;
        public ModifierChangeWhen GetModuleCostChangeWhen() => ModifierChangeWhen.FIXED;

        public override void OnStart(StartState state)
        {
            UpdatePropellantConfigs();
            RemoveZeroResources();
        }

        private void RemoveZeroResources()
        {
            var resourcesToRemove = new List<string>();
            foreach (var resource in part.Resources)
            {
                if (resource.maxAmount <= 1e-3) resourcesToRemove.Add(resource.resourceName);
            }
            foreach (var resourceName in resourcesToRemove) part.RemoveResource(resourceName);
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

        private void AddResource(string resourceName, float addedVolume)
        {
            var resourceDefinition = PartResourceLibrary.Instance.GetDefinition(resourceName);
            var unitVolume = GetUnitVolume(resourceName);
            var density = resourceDefinition.density / unitVolume;

            var addedAmount = double.MaxValue;
            var addedMaxAmount = addedVolume / unitVolume;

            var totalAmount = addedAmount;
            var totalMaxAmount = addedMaxAmount;

            if (part.Resources.Contains(resourceName))
            {
                var resource = part.Resources.Get(resourceName);
                var previousAmount = (float)resource.amount;
                var previousMaxAmount = (float)resource.maxAmount;

                if (previousAmount < previousMaxAmount) totalAmount = previousAmount;
                totalMaxAmount += previousMaxAmount;
            }

            currentAddedMass += GetTankMass(addedVolume, density);
            currentAddedCost += addedMaxAmount * resourceDefinition.unitCost;

            if (totalAmount < 0) totalAmount = 0;
            if (totalMaxAmount < 0) totalMaxAmount = 0;
            if (totalAmount > totalMaxAmount) totalAmount = totalMaxAmount;

            var resourceNode = new ConfigNode();
            resourceNode.name = "RESOURCE";
            resourceNode.AddValue("name", resourceName);
            resourceNode.AddValue("amount", totalAmount);
            resourceNode.AddValue("maxAmount", totalMaxAmount);
            part.SetResource(resourceNode);
        }

        private void AddOrRemoveConfiguredPropellant(bool addNotRemove)
        {
            currentAddedMass = AddedMassScaled;
            currentAddedCost = AddedCostScaled;
            var addedVolume = addNotRemove ? VolumeScaled : -VolumeScaled;

            if (PropellantConfigCurrent is null || PropellantConfigCurrent.Propellants.Count == 0)
            {
                currentAddedMass += GetTankMass(addedVolume, 0.001f);
                return;
            }

            var totalRatio = 0f;
            foreach (var propellant in PropellantConfigCurrent.Propellants) totalRatio += propellant.ratio;
            foreach (var propellant in PropellantConfigCurrent.Propellants) AddResource(propellant.name, addedVolume * propellant.ratio / totalRatio);
        }

        public override void UnapplyPropellantConfig()
        {
            AddOrRemoveConfiguredPropellant(false);
        }

        public override void ApplyPropellantConfig()
        {
            AddOrRemoveConfiguredPropellant(true);
            RemoveZeroResources();
        }

        public override bool ShouldUpdateAndApply()
        {
            return HighLogic.LoadedSceneIsEditor;
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