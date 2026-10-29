using MfmeFmlDecoder.Decoder;
using MfmeFmlDecoder.Model;

namespace MfmeFmlDecoder.src.Decoder.Component.Profiles.V19_8
{
    internal static class LampProfileV19_8
    {
        public static readonly ComponentProfile Profile =
            (V20_1.LampProfileV20_1.Profile with { Tags = BuildTags() })
                .WithVersion(MfmeVersion.V19_8)
                .AsLegacyTwoSection()
                .WithOrientationRule(OrientationDefaultMode.Vertical);

        private static ComponentTagMap BuildTags()
        {
            ComponentTagMap inner = new(V20_1.LampProfileV20_1.Profile.Tags.NestedTagBlockMap);
            inner.Add(0x18, new TagInfo(0x01, "Unknown 0x18", new byte[] { 0x00 }, ValueRole.BYTE));

            ComponentTagMap outer = new(V20_1.LampProfileV20_1.Profile.Tags);
            return outer.WithNestedTagBlock(inner);
        }
    }
}
