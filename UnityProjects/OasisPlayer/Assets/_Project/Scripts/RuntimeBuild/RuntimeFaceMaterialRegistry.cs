using System.Collections.Generic;
using OasisPlayer.Settings;
using UnityEngine;

namespace OasisPlayer.RuntimeBuild
{
    public static class RuntimeFaceMaterialRegistry
    {
        private static readonly List<Material> Materials = new List<Material>();
        private static PlayerGraphicsSettings _current = PlayerGraphicsSettings.Defaults();

        public static void Register(Material material)
        {
            if (material == null || Materials.Contains(material)) return;
            Materials.Add(material);
            Apply(material, _current);
        }

        public static void Apply(PlayerGraphicsSettings settings)
        {
            _current = (settings ?? PlayerGraphicsSettings.Defaults()).Clone();
            _current.Validate();
            for (var i = Materials.Count - 1; i >= 0; i--)
            {
                var material = Materials[i];
                if (material == null) { Materials.RemoveAt(i); continue; }
                Apply(material, _current);
            }
        }

        private static void Apply(Material material, PlayerGraphicsSettings settings)
        {
            if (material.HasProperty(RuntimeFaceShaderProperties.LampExposureStops))
                material.SetFloat(RuntimeFaceShaderProperties.LampExposureStops, settings.LampExposureStops);
        }
    }
}
