using MfmeFmlDecoder.Decoder;
using MfmeFmlDecoder.Model;

namespace MfmeFmlDecoder.src.Decoder.Component.Profiles.V6_1
{
    internal static class BarcrestBWBVideoProfileV6_1
    {
        public static readonly ComponentProfile Profile = Build();

        private static ComponentProfile Build()
        {
            ComponentProfile legacy = V19_8.BarcrestBWBVideoProfileV19_8.Profile.WithVersion(MfmeVersion.V6_1);
            ComponentTagMap tags = new(legacy.Tags);
            tags.Add(0x02, new TagInfo(0x04, "LegacyLeftSkew", new byte[] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.INT32));
            tags.Add(0x03, new TagInfo(0x04, "LegacyRightSkew", new byte[] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.INT32));

            return legacy
                .WithTags(tags)
                .WithAngle(AngleProfile.Rewrite(0))
                .WithDefaults(legacy.Defaults with { LegacyScaledSkew = true, UseNormalizedExtendedData = true });
        }
    }
}
