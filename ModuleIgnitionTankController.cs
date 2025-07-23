using System;
using System.Collections.Generic;

namespace Ignition
{
    class ModuleIgnitionTankController : ModuleIgnitionController, IPartMassModifier, IPartCostModifier
    {
        [KSPField(isPersistant = true)]
        public double volume = 0;
        private double VolumeScaled => GetScale(VolumeScaleExponent) * volume;

        [KSPField(isPersistant = true)]
        public double addedMass = 0;
        public double AddedMassScaled => GetScale(MassScaleExponent) * addedMass;

        [KSPField(isPersistant = true)]
        public double currentAddedMass = 0;
        public float GetModuleMass(float baseMass, ModifierStagingSituation situation) => (float)currentAddedMass;
        public ModifierChangeWhen GetModuleMassChangeWhen() => ModifierChangeWhen.FIXED;

        [KSPField(isPersistant = true)]
        public double addedCost = 0;
        public double AddedCostScaled => GetScale(CostScaleExponent) * addedCost;

        [KSPField(isPersistant = true)]
        public double currentAddedCost = 0;
        public float GetModuleCost(float baseCost, ModifierStagingSituation situation) => (float)currentAddedCost;
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
                if (resource.maxAmount < 1e-5 * VolumeScaled) resourcesToRemove.Add(resource.resourceName);
            }
            foreach (var resourceName in resourcesToRemove) part.RemoveResource(resourceName);
        }

        private double GetUnitVolume(string resourceName)
        {
            if (resourceName == "LiquidFuel") return 5;
            if (resourceName == "Oxidizer") return 5;
            if (resourceName == "MonoPropellant") return 4;
            return 1;
        }

        private double GetTankMass(double volume, double resourceDensity)
        {
            return volume * Math.Round(2500000 * Math.Pow(resourceDensity, 2 / 3.0)) / 200000000;
        }

        private void AddOrRemoveResource(string resourceName, double volumeFraction, bool addNotRemove)
        {
            var resourceDefinition = PartResourceLibrary.Instance.GetDefinition(resourceName);
            var unitVolume = GetUnitVolume(resourceName);
            var density = resourceDefinition.density / unitVolume;

            var addedVolume = addNotRemove ? volume : -volume;
            addedVolume *= volumeFraction;
            var addedVolumeScaled = addedVolume * GetScale(VolumeScaleExponent);

            var addedAmount = double.MaxValue;
            var addedMaxAmount = addedVolumeScaled / unitVolume;

            var totalAmount = addedAmount;
            var totalMaxAmount = addedMaxAmount;

            if (part.Resources.Contains(resourceName))
            {
                var resource = part.Resources.Get(resourceName);
                var previousAmount = resource.amount;
                var previousMaxAmount = resource.maxAmount;

                if (previousAmount < previousMaxAmount) totalAmount = previousAmount;
                totalMaxAmount += previousMaxAmount;
            }

            if (addNotRemove)
            {
                currentAddedMass += GetTankMass(addedVolume, density);
                //currentAddedCost += addedMaxAmount * resourceDefinition.unitCost / GetScale(CostScaleExponent);
            }

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

        public override void ScaleMassAndCost()
        {
            currentAddedMass *= GetScale(MassScaleExponent);
            currentAddedCost *= GetScale(CostScaleExponent);
        }

        private void AddOrRemoveConfiguredPropellant(bool addNotRemove)
        {
            if (PropellantConfigCurrent is null || PropellantConfigCurrent.Propellants.Count == 0)
            {
                currentAddedMass = 0;
                return;
            }

            currentAddedMass = addedMass;
            //currentAddedCost = addedCost;

            var totalRatio = 0.0;
            foreach (var propellant in PropellantConfigCurrent.Propellants) totalRatio += propellant.ratio;
            foreach (var propellant in PropellantConfigCurrent.Propellants) AddOrRemoveResource(propellant.name, propellant.ratio / totalRatio, addNotRemove);
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