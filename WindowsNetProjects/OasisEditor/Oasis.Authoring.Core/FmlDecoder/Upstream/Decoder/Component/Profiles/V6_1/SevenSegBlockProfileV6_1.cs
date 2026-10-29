using MfmeFmlDecoder.Decoder;

namespace MfmeFmlDecoder.src.Decoder.Component.Profiles.V6_1
{
    internal static class SevenSegBlockProfileV6_1
    {
        public static readonly ComponentProfile Profile =
            V19_8.SevenSegBlockProfileV19_8.Profile
                .WithVersion(MfmeVersion.V6_1)
                .WithAngle(AngleProfile.Rewrite(0))
                .WithBaseTags(CommonBaseTags.For(V19_8.SevenSegBlockProfileV19_8.Profile.Tags));
    }
}
