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

            if (PropellantNodeResourceNames is null && !(ModuleRCS.propellants is null))
            {
                PropellantNodeResourceNames = "";
                for (int i = 0; i < ModuleRCS.propellants.Count; i++)
                {
                    PropellantNodeResourceNames += ModuleRCS.propellants[i];
                    if (i != ModuleRCS.propellants.Count - 1) PropellantNodeResourceNames += ";";
                }
            }

            if (ModuleRCS.atmosphereCurve.Curve.keys.Length == 0) return;
            if (MaxThrustOriginal == -1) MaxThrustOriginal = ModuleRCS.thrusterPower;
            if (IspVacuumOriginal == -1) IspVacuumOriginal = GetKeyframeValue(ModuleRCS.atmosphereCurve.Curve.keys, 0);
            if (IspSeaLevelOriginal == -1) IspSeaLevelOriginal = GetKeyframeValue(ModuleRCS.atmosphereCurve.Curve.keys, 1);
        }

        protected override void ApplyPropellantCombinationToModule()
        {
            if (ModuleIsNull()) return;

            ModuleRCS.thrusterPower = (float)MaxThrustCurrent;
            ModuleRCS.maxFuelFlow = MaxFuelFlowCurrent;
            ModuleRCS.atmosphereCurve.Curve.keys = GetIspKeys();
            ModuleRCS.propellants = GetAllCurrentPropellants(ModuleRCS.propellants);
        }

        protected override void RecompilePartInfo()
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

        protected override double GetG()
        {
            if (ModuleIsNull()) return 9.80665;

            return (double)ModuleRCS.G;
        }

        protected override bool UseIspSeaLevel()
        {
            return true;
        }

        protected override double GetScaledMaxThrustOriginal()
        {
            return GetScale(RCSThrustScaleExponent) * MaxThrustOriginal;
        }
    }
}
