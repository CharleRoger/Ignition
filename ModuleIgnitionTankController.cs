using System;
using System.Collections.Generic;

namespace Ignition
{
    class ModuleIgnitionTankController : ModuleIgnitionController, IPartMassModifier, IPartCostModifier
    {
        [KSPField(isPersistant = true)]
        public double volume = 0;
        private double VolumeScaled => GetScale(VolumeScaleExponent) * volume;
        private double VolumeResolution => 1e-5 * VolumeScaled;

        [KSPField(isPersistant = true)]
        public double addedMass = 0;

        [KSPField(isPersistant = true)]
        public double currentAddedMass = 0;
        public float GetModuleMass(float baseMass, ModifierStagingSituation situation) => (float)currentAddedMass;
        public ModifierChangeWhen GetModuleMassChangeWhen() => ModifierChangeWhen.CONSTANTLY;

        [KSPField(isPersistant = true)]
        public double addedCost = 0;

        [KSPField(isPersistant = true)]
        public double currentAddedCost = 0;
        public float GetModuleCost(float baseCost, ModifierStagingSituation situation) => (float)currentAddedCost;
        public ModifierChangeWhen GetModuleCostChangeWhen() => ModifierChangeWhen.CONSTANTLY;

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
                if (resource.maxAmount < VolumeResolution) resourcesToRemove.Add(resource.resourceName);
            }
            foreach (var resourceName in resourcesToRemove) part.RemoveResource(resourceName);
        }

        private void AddOrRemoveResource(string resourceName, double volumeFraction, double tankDensity, bool addNotRemove)
        {
            var resourceDefinition = PartResourceLibrary.Instance.GetDefinition(resourceName);

            var addedVolume = addNotRemove ? volume : -volume;
            addedVolume *= volumeFraction * GetScale(VolumeScaleExponent);

            var addedAmount = addedVolume / PropellantConfigUtils.GetUnitVolume(resourceName);

            if (addNotRemove)
            {
                // Set unscaled values because TweakScale will handle them afterwards
                currentAddedMass += addedVolume * tankDensity / GetScale(MassScaleExponent);
                currentAddedCost += addedAmount * resourceDefinition.unitCost / GetScale(CostScaleExponent);
            }

            var totalAmount = addedAmount;
            if (part.Resources.Contains(resourceName)) totalAmount += part.Resources.Get(resourceName).maxAmount;
            if (totalAmount < 0) totalAmount = 0;

            var resourceNode = new ConfigNode();
            resourceNode.name = "RESOURCE";
            resourceNode.AddValue("name", resourceName);
            resourceNode.AddValue("amount", totalAmount);
            resourceNode.AddValue("maxAmount", totalAmount);
            part.SetResource(resourceNode);
        }

        private void AddOrRemoveConfiguredPropellant(bool addNotRemove)
        {
            currentAddedMass = addedMass;
            currentAddedCost = addedCost;

            if (PropellantConfigCurrent is null || PropellantConfigCurrent.Propellants.Count == 0)
            {
                currentAddedMass = 0;
                return;
            }

            var totalRatio = 0.0;
            foreach (var propellant in PropellantConfigCurrent.Propellants) totalRatio += propellant.ratio;
            foreach (var propellant in PropellantConfigCurrent.Propellants) AddOrRemoveResource(propellant.name, propellant.ratio / totalRatio, PropellantConfigCurrent.TankDensity, addNotRemove);
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