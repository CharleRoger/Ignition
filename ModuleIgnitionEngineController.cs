using System.Linq;

namespace Ignition
{
    class ModuleIgnitionEngineController : ModuleIgnitionThrusterController
    {
        [KSPField(isPersistant = true)]
        public string engineID = "";

        private ModuleEngines _moduleEngine;
        private ModuleEngines ModuleEngines
        {
            get
            {
                if (_moduleEngine is null)
                {
                    var allEngineModules = part.FindModulesImplementing<ModuleEngines>();
                    if (engineID != "")
                    {
                        foreach (var engineModule in allEngineModules)
                        {
                            if (engineModule.engineID == engineID)
                            {
                                _moduleEngine = engineModule;
                                break;
                            }
                        }
                    }
                    else if (allEngineModules.Count > 0) _moduleEngine = allEngineModules.FirstOrDefault();
                }
                return _moduleEngine;
            }
        }

        protected override bool ModuleIsNull()
        {
            return ModuleEngines is null;
        }

        protected override string GetGroupName()
        {
            return engineID != "" ? engineID : "Engine";
        }

        protected override void InitialiseStats()
        {
            if (ModuleIsNull()) return;
            if (ModuleEngines.atmosphereCurve.Curve.keys.Length == 0) return;
            if (MaxThrustOriginal != -1) return;

            MaxThrustOriginal = ModuleEngines.maxThrust;
            IspVacuumOriginal = ModuleEngines.atmosphereCurve.Curve.keys[0].value;
            if (!ModuleEngines.useVelCurve) IspSeaLevelOriginal = ModuleEngines.atmosphereCurve.Curve.keys[1].value;
        }

        protected override void ApplyPropellantCombinationToModule()
        {
            if (ModuleIsNull()) return;
            if (PropellantConfigCurrent is null) return;
            if (PropellantConfigCurrent.Propellants.Count == 0) return;
            if (MaxThrustCurrent == -1) return;

            ModuleEngines.maxThrust = MaxThrustCurrent;
            ModuleEngines.maxFuelFlow = MaxFuelFlowCurrent;
            ModuleEngines.atmosphereCurve.Curve.keys = GetIspKeys();
            ModuleEngines.propellants = PropellantConfigCurrent.Propellants;
            ModuleEngines.SetupPropellant();
        }

        protected override float GetG()
        {
            if (ModuleIsNull()) return 9.80665f;

            return ModuleEngines.g;
        }

        protected override bool UseVelCurve()
        {
            if (ModuleIsNull()) return false;

            return ModuleEngines.useVelCurve;
        }
    }
}