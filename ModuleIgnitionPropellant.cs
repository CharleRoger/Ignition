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
        public string resourceNameOriginal = null;

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

            foreach (var controllerModule in part.FindModulesImplementing<ModuleIgnitionController>())
            {
                if (controllerModule.IsConnectedToPropellantModule(moduleID)) controllerModule.UpdateAndApply(false);
            }
        }
    }

    public class ModuleIgnitionPropellantWrapper
    {
        private ModuleIgnitionPropellant Module;
        private bool UseOriginalResourceNames;
        public string moduleID => Module.moduleID;
        public float ratio => Module.ratio;
        public bool drawStackGauge => Module.drawStackGauge;
        public bool ignoreForIsp => Module.ignoreForIsp;

        public ModuleIgnitionPropellantWrapper(ModuleIgnitionPropellant module, bool useOriginalResourceNames)
        {
            Module = module;
            UseOriginalResourceNames = useOriginalResourceNames;
        }

        public string GetResourceName()
        {
            if (UseOriginalResourceNames) return Module.resourceNameOriginal;
            return Module.resourceName;
        }
    }
}