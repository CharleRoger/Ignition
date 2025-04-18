using System.Linq;

namespace Ignition
{
    class ModuleIgnitionRCSController : ModuleIgnitionThrusterController
    {
        private ModuleRCS _moduleRCS;
        private ModuleRCS ModuleRCS
        {
            get
            {
                if (_moduleRCS is null)
                {
                    var rcsModules = part.FindModulesImplementing<ModuleRCS>();
                    if (rcsModules.Count > 0) _moduleRCS = rcsModules.FirstOrDefault();
                }
                return _moduleRCS;
            }
        }

        protected override bool ModuleIsNull()
        {
            return ModuleRCS is null;
        }

        protected override string GetGroupName()
        {
            return "RCS";
        }

        protected override void SetupOriginalData()
        {
            if (ModuleIsNull()) return;
            if (ModuleRCS.atmosphereCurve.Curve.keys.Length == 0) return;
            if (MaxThrustOriginal != -1) return;

            MaxThrustOriginal = ModuleRCS.thrusterPower;
            IspVacuumOriginal = ModuleRCS.atmosphereCurve.Curve.keys[0].value;
            IspSeaLevelOriginal = ModuleRCS.atmosphereCurve.Curve.keys[1].value;
        }

        protected override void ApplyPropellantCombinationToModule()
        {
            if (ModuleIsNull()) return;

            ModuleRCS.thrusterPower = MaxThrustCurrent;
            ModuleRCS.maxFuelFlow = MaxFuelFlowCurrent;
            ModuleRCS.atmosphereCurve.Curve.keys = GetIspKeys();
            ModuleRCS.propellants = PropellantConfigCurrent.Propellants;
        }

        public override void RecompilePartInfo()
        {
            if (part.partInfo is null || part.partInfo.moduleInfos is null) return;

            var rcsModules = part.FindModulesImplementing<ModuleRCS>();
            var rcsIndex = 0;
            for (int i = 0; i < part.partInfo.moduleInfos.Count; i++)
            {
                if (part.partInfo.moduleInfos[i].moduleName != "RCS" || part.partInfo.moduleInfos[i].moduleName != "RCSFX") continue;
                part.partInfo.moduleInfos[i].info = rcsModules[rcsIndex].GetInfo();
                rcsIndex++;
            }
        }

        protected override float GetG()
        {
            if (ModuleIsNull()) return 9.80665f;

            return (float)ModuleRCS.G;
        }

        protected override bool UseVelCurve()
        {
            return false;
        }
    }
}
