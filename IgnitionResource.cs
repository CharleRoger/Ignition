using System;
using UnityEngine;

namespace Ignition
{
    [Serializable]
    public class IgnitionResource : IConfigNode
    {
        [SerializeField]
        public string ResourceName = null;
        [SerializeField]
        public double Amount = 0;
        [SerializeField]
        public double ScaledAmount = 0;
        [SerializeField]
        public double AddedIgnitionPotential = 0;
        [SerializeField]
        public bool AlwaysRequired = false;

        public void Load(ConfigNode node)
        {
            ResourceName = node.GetValue("name");
            if (node.HasValue("Amount")) Amount = Math.Max(0.0, double.Parse(node.GetValue("Amount")));
            if (node.HasValue("ScaledAmount")) ScaledAmount = Math.Max(0.0, double.Parse(node.GetValue("ScaledAmount")));
            if (node.HasValue("AddedIgnitionPotential")) AddedIgnitionPotential = Math.Max(0.0, double.Parse(node.GetValue("AddedIgnitionPotential")));
            if (node.HasValue("AlwaysRequired")) AlwaysRequired = bool.Parse(node.GetValue("AlwaysRequired"));
        }

        public void Save(ConfigNode node)
        {
            node.AddValue("name", ResourceName);
            node.AddValue("Amount", Math.Max(0.0, Amount));
            node.AddValue("ScaledAmount", Math.Max(0.0, ScaledAmount));
            node.AddValue("AddedIgnitionPotential", Math.Max(0.0, AddedIgnitionPotential));
            node.AddValue("AlwaysRequired", AlwaysRequired);
        }

        public override string ToString()
        {
            var str = ResourceName + ':' + Amount.ToString("F3") + '/' + ScaledAmount.ToString("F3") + '/' + AddedIgnitionPotential.ToString("F3") + '/' + AlwaysRequired.ToString();
            return str;
        }

        public static IgnitionResource FromString(string str)
        {
            IgnitionResource ignitionResource = new IgnitionResource();

            var split = str.Split(':');
            var values = split[1].Split('/');

            ignitionResource.ResourceName = split[0];
            ignitionResource.Amount = double.Parse(values[0]);
            ignitionResource.ScaledAmount = double.Parse(values[1]);
            ignitionResource.AddedIgnitionPotential = double.Parse(values[2]);
            ignitionResource.AlwaysRequired = bool.Parse(values[3]);

            return ignitionResource;
        }
    }
}