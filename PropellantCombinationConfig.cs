using System;
using System.Collections.Generic;

namespace Ignition
{
    public class PropellantCombinationConfig : PropellantConfigBase
    {
        private double TotalPropellantRatio
        {
            get
            {
                var totalRatio = 0.0;
                foreach (var propellant in Propellants)
                {
                    if (propellant.ignoreForIsp) continue;

                    totalRatio += propellant.ratio;
                }

                return totalRatio;
            }
        }

        private double _thrustMultiplier = 0;
        public override double ThrustMultiplier
        {
            get
            {
                if (_thrustMultiplier == 0 && TotalPropellantRatio > 0)
                {
                    _thrustMultiplier = 1;
                    foreach (var propellant in Propellants)
                    {
                        if (propellant.ignoreForIsp) continue;

                        _thrustMultiplier *= Math.Pow(PropellantConfigs[propellant.name].ThrustMultiplier, propellant.ratio / TotalPropellantRatio);
                    }
                }

                return _thrustMultiplier;
            }
            protected set
            {
                _thrustMultiplier = value;
            }
        }

        private double _ispMultiplier = 0;
        public override double IspMultiplier
        {
            get
            {
                if (_ispMultiplier == 0 && TotalPropellantRatio > 0)
                {
                    _ispMultiplier = 1;
                    foreach (var propellant in Propellants)
                    {
                        if (propellant.ignoreForIsp) continue;

                        _ispMultiplier *= Math.Pow(PropellantConfigs[propellant.name].IspMultiplier, propellant.ratio / TotalPropellantRatio);
                    }
                }

                return _ispMultiplier;
            }
            protected set
            {
                _ispMultiplier = value;
            }
        }

        private double _ignitionPotential = 0;
        public override double IgnitionPotential
        {
            get
            {
                if (_ignitionPotential == 0 && TotalPropellantRatio > 0)
                {
                    _ignitionPotential = 1;
                    foreach (var propellant in Propellants)
                    {
                        if (propellant.ignoreForIsp) continue;

                        _ignitionPotential *= Math.Pow(PropellantConfigs[propellant.name].IgnitionPotential, propellant.ratio / TotalPropellantRatio);
                    }
                }

                return _ignitionPotential;
            }
            protected set
            {
                _ignitionPotential = value;
            }
        }

        private double _tankDensity = 0;
        public override double TankDensity
        {
            get
            {
                if (_tankDensity == 0 && TotalPropellantRatio > 0)
                {
                    _tankDensity = 0.0;
                    foreach (var propellant in Propellants)
                    {
                        if (propellant.ignoreForIsp) continue;

                        _tankDensity += PropellantConfigs[propellant.name].TankDensity * propellant.ratio / TotalPropellantRatio;
                    }
                }

                return _tankDensity;
            }
            protected set
            {
                _tankDensity = value;
            }
        }

        private Dictionary<string, PropellantConfig> _propellantConfigs = null;
        public Dictionary<string, PropellantConfig> PropellantConfigs
        {
            get
            {
                if (_propellantConfigs == null)
                {
                    var allPropellantConfigNodes = GameDatabase.Instance.GetConfigNodes("IgnitionPropellantConfig");
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
                propellant.drawStackGauge = propellantNode.HasValue("drawStackGauge") && propellantNode.GetValue("drawStackGauge").ToLower() == "true";
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
            var fuelFraction = fuelConfig.MixtureConstant / (double)(fuelConfig.MixtureConstant + oxidizerConfig.MixtureConstant);
            var nearestTenth = Math.Round(fuelFraction * 10) / 10;
            var nearestSixteenth = Math.Round(fuelFraction * 16) / 16;
            var fuelFractionRounded = Math.Abs(fuelFraction - nearestTenth) < Math.Abs(fuelFraction - nearestSixteenth) ? nearestTenth : nearestSixteenth;

            var fuel = fuelConfig.GetPropellant(fuelFractionRounded, true);
            var oxidizer = oxidizerConfig.GetPropellant(1 - fuelFractionRounded);
            Propellants = new List<Propellant> { fuel, oxidizer };
        }

        public PropellantCombinationConfig(List<Propellant> propellants)
        {
            Propellants = propellants;
        }
    }
}
