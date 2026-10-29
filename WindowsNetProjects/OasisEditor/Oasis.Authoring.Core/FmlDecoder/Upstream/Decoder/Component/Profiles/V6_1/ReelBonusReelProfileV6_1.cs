using MfmeFmlDecoder.Decoder;

namespace MfmeFmlDecoder.src.Decoder.Component.Profiles.V6_1
{
    internal static class ReelBonusReelProfileV6_1
    {
        public static readonly ComponentProfile Profile = Build();

        private static ComponentProfile Build()
        {
            ComponentProfile legacy = V19_8.ReelBonusReelProfileV19_8.Profile
                .WithVersion(MfmeVersion.V6_1)
                .WithOrientationRule(OrientationDefaultMode.Vertical)
                .WithAngle(AngleProfile.Rewrite(0));

            return legacy with
            {
                BaseTags = CommonBaseTags.For(legacy.Tags),
                BaseSection = BaseSectionProfile.LegacyTwoSection(legacy.Tags),
            };
        }
    }
}
