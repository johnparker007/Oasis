using MfmeFmlDecoder.Decoder;

namespace MfmeFmlDecoder.src.Decoder.Component.Profiles.V6_1
{
    internal static class BFMVideoProfileV6_1
    {
        public static readonly ComponentProfile Profile =
            V19_8.BFMVideoProfileV19_8.Profile
                .WithVersion(MfmeVersion.V6_1)
                .WithAngle(AngleProfile.Rewrite(0));
    }
}
