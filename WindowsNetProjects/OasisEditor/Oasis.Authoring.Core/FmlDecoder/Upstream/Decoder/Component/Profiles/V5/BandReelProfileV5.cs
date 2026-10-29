using MfmeFmlDecoder.Decoder;

namespace MfmeFmlDecoder.src.Decoder.Component.Profiles.V5
{
    internal static class BandReelProfileV5
    {
        public static readonly ComponentProfile Profile = Build();

        private static ComponentProfile Build()
        {
            ComponentProfile legacy = V6_1.BandReelProfileV6_1.Profile.WithVersion(MfmeVersion.V5);
            return legacy.WithDefaults(legacy.Defaults with { SequentialBandReelMaskSlots = true });
        }
    }
}