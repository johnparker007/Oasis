using MfmeFmlDecoder.Decoder;

namespace MfmeFmlDecoder.src.Decoder.Component.Profiles.V5
{
    internal static class BFMLEDProfileV5
    {
        public static readonly ComponentProfile Profile = Build();

        private static ComponentProfile Build()
        {
            ComponentProfile legacy = V19_8.BFMLEDProfileV19_8.Profile.WithVersion(MfmeVersion.V5);
            return legacy.WithDefaults(legacy.Defaults with { BfmLedSizeFromXSize = true });
        }
    }
}