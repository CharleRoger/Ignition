namespace Ignition
{
    public class PropellantConfig : PropellantConfigBase
    {
        public string ResourceName { get; private set; } = "";
        public bool IsOxidizer { get; private set; } = false;
        public int MixtureConstant { get; private set; } = 0;

        public PropellantConfig(ConfigNode node) : base(node)
        {
            if (node.HasValue("name")) ResourceName = node.GetValue("name");
            if (node.HasValue("IsOxidizer")) IsOxidizer = bool.Parse(node.GetValue("IsOxidizer"));
            if (node.HasValue("MixtureConstant")) MixtureConstant = int.Parse(node.GetValue("MixtureConstant"));
            Propellants.Add(GetPropellant(1, true));
        }

        public PropellantConfig(string resourceName)
        {
            ResourceName = resourceName;
        }

        public Propellant GetPropellant(float ratio, bool drawStackGauge = false, bool ignoreForIsp = false)
        {
            var node = new ConfigNode();
            node.name = "PROPELLANT";
            node.AddValue("name", ResourceName);
            node.AddValue("ratio", ratio);
            node.AddValue("drawStackGauge", drawStackGauge);
            node.AddValue("ignoreForIsp", ignoreForIsp);
            var propellant = new Propellant();
            propellant.Load(node);
            propellant.displayName = ResourceName;
            return propellant;
        }
    }
}