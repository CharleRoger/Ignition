using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Ignition
{
    abstract class ModuleIgnitionThrusterController : ModuleIgnitionController
    {
        [KSPField(isPersistant = true)]
        public double MaxThrustOriginal = -1;
        protected double MaxThrustCurrent = -1;

        [KSPField(isPersistant = true)]
        public double IspVacuumOriginal = -1;
        protected double IspVacuumCurrent = -1;

        [KSPField(isPersistant = true)]
        public double IspSeaLevelOriginal = -1;
        protected double IspSeaLevelCurrent = -1;

        protected double MaxFuelFlowCurrent => MaxThrustCurrent / (GetG() * IspVacuumCurrent);

        [KSPField(isPersistant = true)]
        public string PropellantNodeResourceNames = null;

        [KSPField(guiName = "<b>Thrust</b>")]
        [UI_Label(scene = UI_Scene.All)]
        public string ThrustString = "";

        [KSPField(guiName = "<b>Isp</b>")]
        [UI_Label(scene = UI_Scene.All)]
        public string IspString = "";

        protected abstract bool ModuleIsNull();
        protected abstract string GetGroupName();
        protected abstract void SetupOriginalData();
        protected abstract void ApplyPropellantCombinationToModule();
        protected abstract void RecompilePartInfo();
        protected abstract double GetG();
        protected abstract bool UseIspSeaLevel();
        protected abstract double GetScaledMaxThrustOriginal();

        protected virtual void SetupInfoStrings()
        {
            bool isActive = !ModuleIsNull();
            Fields["ThrustString"].guiActiveEditor = isActive;
            Fields["IspString"].guiActiveEditor = isActive;
            Fields["ThrustString"].guiActive = isActive;
            Fields["IspString"].guiActive = isActive;

            if (!isActive) return;

            string groupName = GetGroupName();
            Fields["ThrustString"].group.name = groupName;
            Fields["IspString"].group.name = groupName;
            Fields["ThrustString"].group.displayName = groupName;
            Fields["IspString"].group.displayName = groupName;
        }

        public override void SetupData()
        {
            SetupOriginalData();
            SetupInfoStrings();
        }

        protected double GetKeyframeValue(Keyframe[] keyframes, double time)
        {
            foreach (var keyframe in keyframes)
            {
                if (keyframe.time == time) return keyframe.value;
            }
            return -1;
        }

        private void ComputeNewStats()
        {
            if (PropellantConfigOriginal is null || PropellantConfigCurrent is null) return;
            if (PropellantConfigOriginal.Propellants.Count == 0 || PropellantConfigCurrent.Propellants.Count == 0) return;

            var thrustMultiplier = PropellantConfigCurrent.ThrustMultiplier / PropellantConfigOriginal.ThrustMultiplier;
            thrustMultiplier = Math.Round(thrustMultiplier * 100) / 100;
            var thrustChange = Math.Round(GetScaledMaxThrustOriginal() * (thrustMultiplier - 1) / 0.1) * 0.1;
            if (Math.Abs(thrustChange) > 5) thrustChange = Math.Round(thrustChange);
            if (Math.Abs(thrustChange) > 20) thrustChange = Math.Round(thrustChange / 5) * 5;
            MaxThrustCurrent = GetScaledMaxThrustOriginal() + thrustChange;

            var ispVacuumMultiplier = PropellantConfigCurrent.IspMultiplier / PropellantConfigOriginal.IspMultiplier;
            ispVacuumMultiplier = Math.Round(ispVacuumMultiplier * 100) / 100;
            var ispVacuumChange = Math.Round(IspVacuumOriginal * (ispVacuumMultiplier - 1));
            if (Math.Abs(ispVacuumChange) > 10) ispVacuumChange = Math.Round(ispVacuumChange / 5) * 5;
            IspVacuumCurrent = IspVacuumOriginal + ispVacuumChange;

            if (UseIspSeaLevel())
            {
                var ispSeaLevelMultiplier = Math.Pow(ispVacuumMultiplier, 1 / thrustMultiplier);
                if (ispSeaLevelMultiplier < 0) ispSeaLevelMultiplier = 0;
                ispSeaLevelMultiplier = Math.Round(ispSeaLevelMultiplier * 100) / 100;
                var ispSeaLevelChange = Math.Round(IspSeaLevelOriginal * (ispSeaLevelMultiplier - 1));
                if (Math.Abs(ispSeaLevelChange) > 10) ispSeaLevelChange = Math.Round(ispSeaLevelChange / 5) * 5;
                IspSeaLevelCurrent = IspSeaLevelOriginal + ispSeaLevelChange;
            }
        }

        public override void ApplyPropellantConfig()
        {
            ComputeNewStats();

            if (ModuleIsNull()) return;
            if (PropellantConfigCurrent is null) return;
            if (PropellantConfigCurrent.Propellants.Count == 0) return;
            if (MaxThrustCurrent == -1) return;

            ApplyPropellantCombinationToModule();
            SetInfoStrings();
        }

        protected Keyframe[] GetIspKeys()
        {
            var ispKeys = new List<Keyframe> { new Keyframe(0, (float)IspVacuumCurrent) };
            if (UseIspSeaLevel())
            {
                ispKeys.Add(new Keyframe(1, (float)IspSeaLevelCurrent));
                ispKeys.Add(new Keyframe(12, 0.001f));
            }

            return ispKeys.ToArray();
        }

        protected List<Propellant> GetAllCurrentPropellants(List<Propellant> allPropellantsPrevious)
        {
            // Add current configured propellants
            var allPropellantsCurrent = new List<Propellant>(PropellantConfigCurrent.Propellants);

            // Add propellants corresponding to original propellant nodes, i.e. not created by Ignition
            if (!(PropellantNodeResourceNames is null))
            {
                var currentConfiguredPropellantNames = new List<string>();
                foreach (var propellant in PropellantConfigCurrent.Propellants) currentConfiguredPropellantNames.Add(propellant.name);

                var externalPropellantNames = PropellantNodeResourceNames.Split(';');
                foreach (var propellant in allPropellantsPrevious)
                {
                    if (externalPropellantNames.Contains(propellant.name) && !currentConfiguredPropellantNames.Contains(propellant.name)) allPropellantsCurrent.Add(propellant);
                }
            }

            return allPropellantsCurrent;
        }

        protected string GetValueString(string unit, double vacuumOriginal, double vacuumCurrent, double seaLevelCurrent = -1)
        {
            var str = vacuumCurrent.ToString("0.0") + unit;

            if (seaLevelCurrent != -1) str = seaLevelCurrent.ToString("0.0") + unit + " — " + str;

            if (vacuumCurrent > vacuumOriginal) str += " (<color=#44FF44>+" + Math.Round(100 * (vacuumCurrent / vacuumOriginal - 1)) + "</color>%)";
            else if (vacuumCurrent < vacuumOriginal) str += " (<color=#FF8888>-" + Math.Round(100 * (1 - vacuumCurrent / vacuumOriginal)) + "</color>%)";

            return str;
        }

        protected void SetInfoStrings()
        {
            if (ModuleIsNull()) return;

            if (UseIspSeaLevel())
            {
                ThrustString = GetValueString("kN", GetScaledMaxThrustOriginal(), MaxThrustCurrent, MaxThrustCurrent * IspSeaLevelCurrent / IspVacuumCurrent);
                IspString = GetValueString("s", IspVacuumOriginal, IspVacuumCurrent, IspSeaLevelCurrent);
            }
            else
            {
                ThrustString = GetValueString("kN", GetScaledMaxThrustOriginal(), MaxThrustCurrent);
                IspString = GetValueString("s", IspVacuumOriginal, IspVacuumCurrent);
            }
        }

        public override string GetInfo()
        {
            UpdateAndApply(true);
            RecompilePartInfo();

            return base.GetInfo();
        }
    }
}
