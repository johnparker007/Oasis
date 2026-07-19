using System;
using UnityEngine;

namespace OasisPlayer.Settings
{
    [Serializable]
    public sealed class PlayerGraphicsSettings
    {
        public const float MinLampExposureStops = 0f;
        public const float MaxLampExposureStops = 8f;
        public const float MinBloomIntensity = 0f;
        public const float MaxBloomIntensity = 10f;

        public float LampExposureStops = 2.5f;
        public bool BloomEnabled = true;
        public float BloomIntensity = 0.8f;

        public static PlayerGraphicsSettings Defaults()
        {
            return new PlayerGraphicsSettings();
        }

        public PlayerGraphicsSettings Clone()
        {
            return new PlayerGraphicsSettings
            {
                LampExposureStops = LampExposureStops,
                BloomEnabled = BloomEnabled,
                BloomIntensity = BloomIntensity
            };
        }

        public void Validate()
        {
            LampExposureStops = ClampFinite(LampExposureStops, MinLampExposureStops, MaxLampExposureStops, Defaults().LampExposureStops);
            BloomIntensity = ClampFinite(BloomIntensity, MinBloomIntensity, MaxBloomIntensity, Defaults().BloomIntensity);
        }

        private static float ClampFinite(float value, float min, float max, float fallback)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp(value, min, max);
        }
    }
}
