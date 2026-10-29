using MfmeFmlDecoder.Decoder;

namespace MfmeFmlDecoder.src.Decoder.Component.Profiles.V6_1
{
    internal static class DotAlphaProfileV6_1
    {
        public static readonly ComponentProfile Profile = Build();

        private static ComponentProfile Build()
        {
            ComponentProfile legacy = V19_8.DotAlphaProfileV19_8.Profile.WithVersion(MfmeVersion.V6_1);
            return legacy.WithDefaults(legacy.Defaults with { DotAlphaAutoSize = true });
        }
    }
}
