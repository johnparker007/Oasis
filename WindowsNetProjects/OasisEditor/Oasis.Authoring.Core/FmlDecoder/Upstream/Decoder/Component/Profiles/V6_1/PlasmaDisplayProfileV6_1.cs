using MfmeFmlDecoder.Decoder;

namespace MfmeFmlDecoder.src.Decoder.Component.Profiles.V6_1
{
    internal static class PlasmaDisplayProfileV6_1
    {
        public static readonly ComponentProfile Profile = Build();

        private static ComponentProfile Build()
        {
            ComponentProfile legacy = V19_8.PlasmaDisplayProfileV19_8.Profile.WithVersion(MfmeVersion.V6_1);
            return legacy with
            {
                Angle = AngleProfile.Rewrite(0),
                BaseTags = CommonBaseTags.For(legacy.Tags),
                BaseSection = BaseSectionProfile.LegacyTwoSection(legacy.Tags),
            };
        }
    }
}
