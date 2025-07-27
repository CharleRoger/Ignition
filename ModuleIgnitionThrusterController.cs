using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Ignition
{
    abstract class ModuleIgnitionThrusterController : ModuleIgnitionController
    {
        [KSPField(isPersistant = true)]
        public double MaxThrustOriginal = 0;
        protected double MaxThrustCurrent = 0;

        [KSPField(isPersistant = true)]
        public double IspVacuumOriginal = 0;
        protected double IspVacuumCurrent = 0;

        [KSPField(isPersistant = true)]
        public double IspSeaLevelOriginal = 0;
        protected double IspSeaLevelCurrent = 0;

        protected double MaxFuelFlowCurrent => MaxThrustCurrent / (GetG() * IspVacuumCurrent);

        [KSPField(isPersistant = true)]
        public string PropellantNodeResourceNames = null;

        [KSPField(guiName = "<b>Propellants</b>")]
        [UI_Label(scene = UI_Scene.All)]
        public string PropellantsString = "";

        [KSPField(guiName = "<b>Thrust</b>")]
        [UI_Label(scene = UI_Scene.All)]
        public string ThrustString = "";

        [KSPField(guiName = "<b>Isp</b>")]
        [UI_Label(scene = UI_Scene.All)]
        public string IspString = "";

        protected abstract bool ModuleIsNull();
        protected abstract void SetupOriginalData();
        protected abstract void ApplyPropellantCombinationToModule();
        protected abstract void RecompilePartInfo();
        protected abstract double GetG();
        protected abstract bool UseIspSeaLevel();
        protected abstract double GetScaledMaxThrustOriginal();
        protected abstract string GetGuiGroupName();
        protected abstract List<Propellant> GetModulePropellants();

        protected override bool DisplayGuiStrings()
        {
            return base.DisplayGuiStrings() && !ModuleIsNull();
        }

        protected void SetupPropellantNodeResourceNames()
        {
            var propellants = GetModulePropellants();
            if (PropellantNodeResourceNames is null && !(propellants is null))
            {
                PropellantNodeResourceNames = "";
                for (int i = 0; i < propellants.Count; i++)
                {
                    PropellantNodeResourceNames += propellants[i].name;
                    if (i != propellants.Count - 1) PropellantNodeResourceNames += ";";
                }
            }
        }

        protected override void SetInfoStrings()
        {
            bool isActive = DisplayGuiStrings();
            Fields["PropellantsString"].guiActiveEditor = isActive;
            Fields["ThrustString"].guiActiveEditor = isActive;
            Fields["IspString"].guiActiveEditor = isActive;
            Fields["PropellantsString"].guiActive = isActive;
            Fields["ThrustString"].guiActive = isActive;
            Fields["IspString"].guiActive = isActive;

            if (!isActive) return;

            string groupName = GetGuiGroupName();
            Fields["PropellantsString"].group.name = groupName;
            Fields["ThrustString"].group.name = groupName;
            Fields["IspString"].group.name = groupName;
            Fields["PropellantsString"].group.displayName = groupName;
            Fields["ThrustString"].group.displayName = groupName;
            Fields["IspString"].group.displayName = groupName;

            var configuredPropellantNames = new List<string>();
            foreach (var propellant in PropellantConfigCurrent.Propellants) configuredPropellantNames.Add(propellant.name);

            PropellantsString = PropellantConfigUtils.GetPropellantRatiosString(GetModulePropellants(), configuredPropellantNames);

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

        public override void SetupData()
        {
            SetupOriginalData();
            SetupPropellantNodeResourceNames();
        }

        protected double GetKeyframeValue(Keyframe[] keyframes, double time)
        {
            foreach (var keyframe in keyframes)
            {
                if (keyframe.time == time) return keyframe.value;
            }
            return 0;
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
            if (MaxThrustCurrent < 0) MaxThrustCurrent = 0;

            var ispVacuumMultiplier = PropellantConfigCurrent.IspMultiplier / PropellantConfigOriginal.IspMultiplier;
            ispVacuumMultiplier = Math.Round(ispVacuumMultiplier * 100) / 100;
            var ispVacuumChange = Math.Round(IspVacuumOriginal * (ispVacuumMultiplier - 1));
            if (Math.Abs(ispVacuumChange) > 10) ispVacuumChange = Math.Round(ispVacuumChange / 5) * 5;
            IspVacuumCurrent = IspVacuumOriginal + ispVacuumChange;
            if (IspVacuumCurrent < 0) IspVacuumCurrent = 0;

            if (UseIspSeaLevel())
            {
                var ispSeaLevelVacuumDifference = Math.Round((IspSeaLevelOriginal - IspVacuumOriginal) * ispVacuumMultiplier / thrustMultiplier);
                if (Math.Abs(ispSeaLevelVacuumDifference) > 10) ispSeaLevelVacuumDifference = Math.Round(ispSeaLevelVacuumDifference / 5) * 5;
                IspSeaLevelCurrent = IspVacuumCurrent + ispSeaLevelVacuumDifference;
                if (IspSeaLevelCurrent < 0) IspSeaLevelCurrent = 0;
            }
        }

        public override void ApplyPropellantConfig()
        {
            ComputeNewStats();

            if (ModuleIsNull()) return;
            if (PropellantConfigCurrent is null) return;
            if (PropellantConfigCurrent.Propellants.Count == 0) return;
            if (MaxThrustCurrent == 0) return;

            ApplyPropellantCombinationToModule();
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

        protected string GetValueString(string unit, double vacuumOriginal, double vacuumCurrent, double seaLevelCurrent = 0)
        {
            var str = vacuumCurrent.ToString("0.0") + unit;

            if (seaLevelCurrent != 0) str = seaLevelCurrent.ToString("0.0") + unit + " — " + str;

            if (vacuumCurrent > vacuumOriginal) str += " (<color=#44FF44>+" + Math.Round(100 * (vacuumCurrent / vacuumOriginal - 1)) + "</color>%)";
            else if (vacuumCurrent < vacuumOriginal) str += " (<color=#FF8888>-" + Math.Round(100 * (1 - vacuumCurrent / vacuumOriginal)) + "</color>%)";

            return str;
        }

        public override string GetInfo()
        {
            UpdateAndApply(true);
            RecompilePartInfo();

            return base.GetInfo();
        }
    }
}
