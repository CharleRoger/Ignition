using System.Collections.Generic;

namespace Ignition
{
    public class ModuleIgnitionPropellant : PartModule
    {
        [KSPField(isPersistant = true)]
        public string moduleID;

        [KSPField(isPersistant = true)]
        public string resourceName;

        [KSPField(isPersistant = true)]
        public string resourceNamePrevious = null;

        [KSPField(isPersistant = true)]
        public string resourceNameOriginal = null;

        public string GetResourceName(bool useOriginal)
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

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);

            if (resourceName is null) return;

            if (resourceNameOriginal is null) resourceNameOriginal = resourceName;
            if (resourceNamePrevious is null) resourceNamePrevious = resourceName;

            var connectedControllerModules = new List<ModuleIgnitionController>();
            foreach (var controllerModule in part.FindModulesImplementing<ModuleIgnitionController>())
            {
                if (controllerModule.IsConnectedToPropellantModule(moduleID)) connectedControllerModules.Add(controllerModule);
                if (controllerModule is ModuleIgnitionTankController)
                {
                    if (resourceName != resourceNameOriginal) part.Resources.Remove(resourceNameOriginal);
                    if (resourceName != resourceNamePrevious) part.Resources.Remove(resourceNamePrevious);
                }
            }

            resourceNamePrevious = resourceName;

            foreach (var controllerModule in connectedControllerModules)
            {
                controllerModule.UpdatePropellantConfigs();
                controllerModule.ApplyPropellantConfig();
            }
        }
    }
}