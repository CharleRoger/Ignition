using System.Collections.Generic;

namespace Ignition
{
    public abstract class PropellantConfigBase
    {
        public virtual double ThrustMultiplier { get; protected set; } = 1;
        public virtual double IspMultiplier { get; protected set; } = 1;
        public virtual double IgnitionPotential { get; protected set; } = 1;
        public List<Propellant> Propellants { get; protected set; } = new List<Propellant>();

        public PropellantConfigBase()
        {

        }

        public PropellantConfigBase(ConfigNode node)
        {
            if (node.HasValue("ThrustMultiplier")) ThrustMultiplier = double.Parse(node.GetValue("ThrustMultiplier"));
            if (node.HasValue("IspMultiplier")) IspMultiplier = double.Parse(node.GetValue("IspMultiplier"));
            if (node.HasValue("IgnitionPotential")) IgnitionPotential = double.Parse(node.GetValue("IgnitionPotential"));
        }
    }
}