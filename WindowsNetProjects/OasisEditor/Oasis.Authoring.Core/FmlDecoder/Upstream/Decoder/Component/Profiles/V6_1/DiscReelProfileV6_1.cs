using MfmeFmlDecoder.Decoder;
using MfmeFmlDecoder.Model;

namespace MfmeFmlDecoder.src.Decoder.Component.Profiles.V6_1
{
    internal static class DiscReelProfileV6_1
    {
        public static readonly ComponentProfile Profile = Build();

        private static ComponentProfile Build()
        {
            ComponentProfile legacy = V19_8.DiscReelProfileV19_8.Profile.WithVersion(MfmeVersion.V6_1);
            ComponentTagMap tags = new(legacy.Tags);
            tags.Add(0x04, new TagInfo(0x01, "Unknown 0x04", new byte[] { 0x00 }, ValueRole.BOOLEAN));
            tags.Add(0x12, new TagInfo(0x01, "Unknown 0x12", new byte[] { 0x00 }, ValueRole.BOOLEAN));

            for (uint tag = 0x13; tag <= 0x95; tag++)
            {
                if (!tags.ContainsKey(tag))
                {
                    tags.Add(tag, new TagInfo(0x04, $"Unknown 0x{tag:X2}", new byte[] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.UINT32));
                }
            }

            return legacy with
            {
                Tags = tags,
                Angle = AngleProfile.Rewrite(0),
                BaseTags = CommonBaseTags.For(tags),
                BaseSection = BaseSectionProfile.LegacyTwoSection(tags),
                Defaults = legacy.Defaults with { DiscReelLampCountFromStops = true },
            };
        }
    }
}
