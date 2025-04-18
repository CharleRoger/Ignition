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
        public float Amount = 0;
        [SerializeField]
        public float ScaledAmount = 0;
        [SerializeField]
        public float AddedIgnitionPotential = 1;
        [SerializeField]
        public bool AlwaysRequired = false;

        public void Load(ConfigNode node)
        {
            ResourceName = node.GetValue("name");
            if (node.HasValue("Amount")) Amount = Mathf.Max(0.0f, float.Parse(node.GetValue("Amount")));
            if (node.HasValue("ScaledAmount")) ScaledAmount = Mathf.Max(0.0f, float.Parse(node.GetValue("ScaledAmount")));
            if (node.HasValue("AddedIgnitionPotential")) AddedIgnitionPotential = Mathf.Max(0.0f, float.Parse(node.GetValue("AddedIgnitionPotential")));
            if (node.HasValue("AlwaysRequired")) AlwaysRequired = bool.Parse(node.GetValue("AlwaysRequired"));
        }

        public void Save(ConfigNode node)
        {
            node.AddValue("name", ResourceName);
            node.AddValue("Amount", Mathf.Max(0.0f, Amount));
            node.AddValue("ScaledAmount", Mathf.Max(0.0f, ScaledAmount));
            node.AddValue("AddedIgnitionPotential", Mathf.Max(0.0f, AddedIgnitionPotential));
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
            ignitionResource.Amount = float.Parse(values[0]);
            ignitionResource.ScaledAmount = float.Parse(values[1]);
            ignitionResource.AddedIgnitionPotential = float.Parse(values[2]);
            ignitionResource.AlwaysRequired = bool.Parse(values[3]);

            return ignitionResource;
        }
    }
}