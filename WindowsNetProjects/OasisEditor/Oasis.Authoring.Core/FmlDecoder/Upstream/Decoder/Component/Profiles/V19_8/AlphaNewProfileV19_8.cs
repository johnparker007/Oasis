using MfmeFmlDecoder.Decoder;

namespace MfmeFmlDecoder.src.Decoder.Component.Profiles.V19_8
{
    internal static class AlphaNewProfileV19_8
    {
        public static readonly ComponentProfile Profile =
            (V20_1.AlphaNewProfileV20_1.Profile with { Angle = AngleProfile.Rewrite(0) })
                .WithVersion(MfmeVersion.V19_8)
                .AsLegacyTwoSection()
                .WithOrientationRule(OrientationDefaultMode.Vertical);
    }
}
