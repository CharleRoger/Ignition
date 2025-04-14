using System.Collections.Generic;

namespace FuelMixer
{
    public abstract class PropellantConfigBase
    {
        public virtual float ThrustMultiplier { get; protected set; } = 1;
        public virtual float IspMultiplier { get; protected set; } = 1;
        public virtual float IgnitionPotential { get; protected set; } = 1;
        public List<Propellant> Propellants { get; protected set; } = new List<Propellant>();

        public PropellantConfigBase()
        {

        }

        public PropellantConfigBase(ConfigNode node)
        {
            if (node.HasValue("ThrustMultiplier")) ThrustMultiplier = float.Parse(node.GetValue("ThrustMultiplier"));
            if (node.HasValue("IspMultiplier")) IspMultiplier = float.Parse(node.GetValue("IspMultiplier"));
            if (node.HasValue("IgnitionPotential")) IgnitionPotential = float.Parse(node.GetValue("IgnitionPotential"));
        }
    }
}