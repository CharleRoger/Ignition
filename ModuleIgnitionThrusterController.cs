using System.Collections.Generic;
using UnityEngine;

namespace Ignition
{
    abstract class ModuleIgnitionThrusterController : ModuleIgnitionController
    {
        [KSPField(isPersistant = true)]
        public float MaxThrustOriginal = -1;
        protected float MaxThrustCurrent = -1;

        [KSPField(isPersistant = true)]
        public float IspVacuumOriginal = -1;
        protected float IspVacuumCurrent = -1;

        [KSPField(isPersistant = true)]
        public float IspSeaLevelOriginal = -1;
        protected float IspSeaLevelCurrent = -1;

        protected float MaxFuelFlowCurrent => MaxThrustCurrent / (GetG() * IspVacuumCurrent);

        [KSPField(guiName = "<b>Thrust</b>")]
        [UI_Label(scene = UI_Scene.All)]
        public string ThrustString = "";

        [KSPField(guiName = "<b>Isp</b>")]
        [UI_Label(scene = UI_Scene.All)]
        public string IspString = "";

        protected struct ThrusterData
        {
            public float maxThrust;
            public float maxFuelFlow;
            public Keyframe[] ispKeys;
        }

        protected abstract bool ModuleIsNull();
        protected abstract string GetGroupName();
        protected abstract void InitialiseStats();
        protected abstract void ApplyPropellantCombinationToModule();
        protected abstract float GetG();
        protected abstract bool UseVelCurve();

        private void InitialiseInfoStrings()
        {
            bool isActive = !ModuleIsNull();
            string groupName = GetGroupName();

            if (groupName == "") groupName = "Thruster";

            Fields["ThrustString"].guiActiveEditor = isActive;
            Fields["IspString"].guiActiveEditor = isActive;

            Fields["ThrustString"].guiActive = isActive;
            Fields["IspString"].guiActive = isActive;

            if (!isActive) return;

            Fields["ThrustString"].group.name = groupName;
            Fields["IspString"].group.name = groupName;

            Fields["ThrustString"].group.displayName = groupName;
            Fields["IspString"].group.displayName = groupName;
        }

        protected string GetValueString(string unit, float vacuumOriginal, float vacuumCurrent, float seaLevelCurrent = -1)
        {
            var str = vacuumCurrent.ToString("0.0") + unit;

            if (seaLevelCurrent != -1) str = seaLevelCurrent.ToString("0.0") + unit + " — " + str;

            if (vacuumCurrent > vacuumOriginal) str += " (<color=#44FF44>+" + Mathf.Round(100 * (vacuumCurrent / vacuumOriginal - 1)) + "</color>%)";
            else if (vacuumCurrent < vacuumOriginal) str += " (<color=#FF8888>-" + Mathf.Round(100 * (1 - vacuumCurrent / vacuumOriginal)) + "</color>%)";

            return str;
        }

        public override void Initialise()
        {
            InitialiseStats();
            InitialiseInfoStrings();
        }

        private void ComputeNewStats()
        {
            if (PropellantConfigOriginal is null || PropellantConfigCurrent is null) return;
            if (PropellantConfigOriginal.Propellants.Count == 0 || PropellantConfigCurrent.Propellants.Count == 0) return;

            var thrustMultiplier = PropellantConfigCurrent.ThrustMultiplier / PropellantConfigOriginal.ThrustMultiplier;
            thrustMultiplier = Mathf.Round(thrustMultiplier * 100) / 100;
            var thrustChange = Mathf.Round(MaxThrustOriginal * (thrustMultiplier - 1) / 0.1f) * 0.1f;
            if (Mathf.Abs(thrustChange) > 5) thrustChange = Mathf.Round(thrustChange);
            if (Mathf.Abs(thrustChange) > 20) thrustChange = Mathf.Round(thrustChange / 5) * 5;
            MaxThrustCurrent = MaxThrustOriginal + thrustChange;

            var ispVacuumMultiplier = PropellantConfigCurrent.IspMultiplier / PropellantConfigOriginal.IspMultiplier;
            ispVacuumMultiplier = Mathf.Round(ispVacuumMultiplier * 100) / 100;
            var ispVacuumChange = Mathf.Round(IspVacuumOriginal * (ispVacuumMultiplier - 1));
            if (Mathf.Abs(ispVacuumChange) > 10) ispVacuumChange = Mathf.Round(ispVacuumChange / 5) * 5;
            IspVacuumCurrent = IspVacuumOriginal + ispVacuumChange;

            if (!UseVelCurve())
            {
                var ispSeaLevelMultiplier = Mathf.Pow(ispVacuumMultiplier, 1 / thrustMultiplier);
                if (ispSeaLevelMultiplier < 0) ispSeaLevelMultiplier = 0;
                ispSeaLevelMultiplier = Mathf.Round(ispSeaLevelMultiplier * 100) / 100;
                var ispSeaLevelChange = Mathf.Round(IspSeaLevelOriginal * (ispSeaLevelMultiplier - 1));
                if (Mathf.Abs(ispSeaLevelChange) > 10) ispSeaLevelChange = Mathf.Round(ispSeaLevelChange / 5) * 5;
                IspSeaLevelCurrent = IspSeaLevelOriginal + ispSeaLevelChange;
            }
        }

        public override void ApplyPropellantConfig()
        {
            ComputeNewStats();
            ApplyPropellantCombinationToModule();
            SetInfoStrings();
        }

        protected Keyframe[] GetIspKeys()
        {
            var ispKeys = new List<Keyframe> { new Keyframe(0, IspVacuumCurrent) };
            if (!UseVelCurve())
            {
                ispKeys.Add(new Keyframe(1, IspSeaLevelCurrent));
                ispKeys.Add(new Keyframe(12, 0.001f));
            }

            return ispKeys.ToArray();
        }

        protected void SetInfoStrings()
        {
            if (ModuleIsNull()) return;

            if (UseVelCurve())
            {
                ThrustString = GetValueString("kN", MaxThrustOriginal, MaxThrustCurrent);
                IspString = GetValueString("s", IspVacuumOriginal, IspVacuumCurrent);
            }
            else
            {
                ThrustString = GetValueString("kN", MaxThrustOriginal, MaxThrustCurrent, MaxThrustCurrent * IspSeaLevelCurrent / IspVacuumCurrent);
                IspString = GetValueString("s", IspVacuumOriginal, IspVacuumCurrent, IspSeaLevelCurrent);
            }
        }
    }
}
