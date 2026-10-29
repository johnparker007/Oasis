using MfmeFmlDecoder.Decoder;

namespace MfmeFmlDecoder.src.Decoder.Component.Profiles.V19_5
{
    internal static class ButtonProfileV19_5
    {
        public static readonly ComponentProfile Profile =
            V19_8.ButtonProfileV19_8.Profile
                .WithVersion(MfmeVersion.V19_5)
                .AsLegacyTwoSection()
                .WithAngle(new AngleProfile(
                    V19_8.ButtonProfileV19_8.Profile.Angle.NormalizationRule,
                    V19_8.ButtonProfileV19_8.Profile.Angle.ValidAngleTrailingSkipBytes,
                    FinalOffsetAdjustment: 0,
                    PreListTag6: true));
    }
}
