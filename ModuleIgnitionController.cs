using System.Collections.Generic;
using static Ignition.PropellantConfigUtils;

namespace Ignition
{
    abstract class ModuleIgnitionController : PartModule
    {
        [KSPField(isPersistant = true)]
        public string moduleID;

        [KSPField(isPersistant = true)]
        public string PropellantModuleIDs = "";

        protected PropellantConfigBase PropellantConfigOriginal = null;
        protected PropellantConfigBase PropellantConfigCurrent = null;

        public virtual void UnapplyPropellantConfig() {}

        public abstract void ApplyPropellantConfig();

        public abstract void UpdateAndApply(bool initialSetup);

        public virtual void SetupData() {}

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);

            if (PropellantModuleIDs == "")
            {
                var propellantIDs = node.GetValues("propellantModuleID");
                for (int i = 0; i < propellantIDs.Length; i++)
                {
                    PropellantModuleIDs += propellantIDs[i];
                    if (i != propellantIDs.Length - 1) PropellantModuleIDs += ";";
                }
            }

            UpdateAndApply(false);
        }
        
        public override void OnStart(StartState state)
        {
            base.OnStart(state);

            UpdateAndApply(true);
        }

        public void UpdatePropellantConfigs()
        {
            PropellantConfigOriginal = GetPropellantConfig(GetConnectedPropellantModules(true, true));
            PropellantConfigCurrent = GetPropellantConfig(GetConnectedPropellantModules(true, false));
        }

        public bool IsConnectedToPropellantModule(string moduleID)
        {
            return PropellantModuleIDs.Contains(moduleID);
        }

        protected List<ModuleIgnitionPropellantWrapper> GetConnectedPropellantModules(bool requireGoodResource, bool useOriginalResourceNames)
        {
            var propellantModules = new List<ModuleIgnitionPropellantWrapper>();
            foreach (var module in part.GetComponents<ModuleIgnitionPropellant>())
            {
                var propellantModule = new ModuleIgnitionPropellantWrapper(module, useOriginalResourceNames);
                if (!IsConnectedToPropellantModule(propellantModule.moduleID)) continue;
                var resource = propellantModule.GetResourceName();
                if (requireGoodResource && (resource is null || PartResourceLibrary.Instance.GetDefinition(resource) is null)) continue;
                propellantModules.Add(propellantModule);
            }
            return propellantModules;
        }
    }
}
