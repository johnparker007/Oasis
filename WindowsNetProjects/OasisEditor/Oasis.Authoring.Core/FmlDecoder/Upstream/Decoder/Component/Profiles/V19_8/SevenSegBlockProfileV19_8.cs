using MfmeFmlDecoder.Decoder;
using MfmeFmlDecoder.Model;

namespace MfmeFmlDecoder.src.Decoder.Component.Profiles.V19_8
{
    internal static class SevenSegBlockProfileV19_8
    {
        public static readonly ComponentProfile Profile =
            (V20_1.SevenSegBlockProfileV20_1.Profile with { Tags = BuildTags() })
                .WithVersion(MfmeVersion.V19_8)
                .AsLegacyTwoSection()
                .WithOrientationRule(OrientationDefaultMode.Vertical);

        private static ComponentTagMap BuildTags()
        {
            ComponentTagMap inner = new(V20_1.SevenSegBlockProfileV20_1.Profile.Tags.NestedTagBlockMap);
            inner.Add(0x18, new TagInfo(0x04, "Unknown 0x18", new byte[] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.UINT32));

            if (inner.TryGetValue(0x1D, out TagInfo dpOff))
            {
                inner[0x1D] = new TagInfo(
                    dpOff.Length,
                    dpOff.AttributeName,
                    new byte[48],
                    dpOff.Role);
            }

            ComponentTagMap outer = new(V20_1.SevenSegBlockProfileV20_1.Profile.Tags);
            return outer.WithNestedTagBlock(inner);
        }
    }
}
