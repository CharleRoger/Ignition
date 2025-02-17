using System.Collections.Generic;
using UnityEngine;

namespace FuelMixer
{
    public class PropellantCombinationConfig : PropellantConfigBase
    {
        private float TotalPropellantRatio
        {
            get
            {
                var totalRatio = 0f;
                foreach (var propellant in Propellants)
                {
                    if (propellant.ignoreForIsp) continue;

                    totalRatio += propellant.ratio;
                }

                return totalRatio;
            }
        }

        private float _thrustMultiplier = 0;
        public override float ThrustMultiplier
        {
            get
            {
                if (_thrustMultiplier == 0 && TotalPropellantRatio > 0)
                {
                    _thrustMultiplier = 1;
                    foreach (var propellant in Propellants)
                    {
                        if (propellant.ignoreForIsp) continue;

                        _thrustMultiplier *= Mathf.Pow(PropellantConfigs[propellant.name].ThrustMultiplier, propellant.ratio / TotalPropellantRatio);
                    }
                }

                return _thrustMultiplier;
            }
            protected set
            {
                _thrustMultiplier = value;
            }
        }

        private float _ispMultiplier = 0;
        public override float IspMultiplier
        {
            get
            {
                if (_ispMultiplier == 0 && TotalPropellantRatio > 0)
                {
                    _ispMultiplier = 1;
                    foreach (var propellant in Propellants)
                    {
                        if (propellant.ignoreForIsp) continue;

                        _ispMultiplier *= Mathf.Pow(PropellantConfigs[propellant.name].IspMultiplier, propellant.ratio / TotalPropellantRatio);
                    }
                }

                return _ispMultiplier;
            }
            protected set
            {
                _ispMultiplier = value;
            }
        }

        private Dictionary<string, PropellantConfig> _propellantConfigs = null;
        private Dictionary<string, PropellantConfig> PropellantConfigs
        {
            get
            {
                if (_propellantConfigs == null)
                {
                    var allPropellantConfigNodes = GameDatabase.Instance.GetConfigNodes("FuelMixerPropellantConfig");
                    var allPropellantConfigs = new Dictionary<string, PropellantConfig>();
                    foreach (var propellantConfigNode in allPropellantConfigNodes)
                    {
                        var propellantConfig = new PropellantConfig(propellantConfigNode);
                        allPropellantConfigs[propellantConfig.ResourceName] = propellantConfig;
                    }

                    _propellantConfigs = new Dictionary<string, PropellantConfig>();
                    foreach (var propellant in Propellants)
                    {
                        if (allPropellantConfigs.ContainsKey(propellant.name)) _propellantConfigs[propellant.name] = allPropellantConfigs[propellant.name];
                        else _propellantConfigs[propellant.name] = new PropellantConfig(propellant.name);
                    }
                }

                return _propellantConfigs;
            }
        }

        public PropellantCombinationConfig(ConfigNode node) : base(node)
        {
            var propellantNodes = node.GetNodes("PROPELLANT");
            foreach (var propellantNode in propellantNodes)
            {
                var propellant = new Propellant();
                propellant.Load(propellantNode);
                propellant.displayName = propellant.name;
                Propellants.Add(propellant);
            }
        }

        public PropellantCombinationConfig(PropellantConfig monopropellantConfig) : base()
        {
            var monopropellant = monopropellantConfig.GetPropellant(1, true);
            Propellants = new List<Propellant> { monopropellant };
        }

        public PropellantCombinationConfig(PropellantConfig fuelConfig, PropellantConfig oxidizerConfig)
        {
            var fuelFraction = fuelConfig.MixtureConstant / (float)(fuelConfig.MixtureConstant + oxidizerConfig.MixtureConstant);
            var nearestTenth = Mathf.Round(fuelFraction * 10) / 10;
            var nearestSixteenth = Mathf.Round(fuelFraction * 16) / 16;
            var fuelFractionRounded = Mathf.Abs(fuelFraction - nearestTenth) < Mathf.Abs(fuelFraction - nearestSixteenth) ? nearestTenth : nearestSixteenth;

            var fuel = fuelConfig.GetPropellant(80 * fuelFractionRounded, true);
            var oxidizer = oxidizerConfig.GetPropellant(Mathf.RoundToInt(80 * (1 - fuelFractionRounded)));
            Propellants = new List<Propellant> { fuel, oxidizer };
        }

        public PropellantCombinationConfig(List<Propellant> propellants)
        {
            Propellants = propellants;
        }
    }
}
