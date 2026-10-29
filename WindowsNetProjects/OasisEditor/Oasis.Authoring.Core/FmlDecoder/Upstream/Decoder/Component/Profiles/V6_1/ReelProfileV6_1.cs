using MfmeFmlDecoder.Decoder;
using MfmeFmlDecoder.Model;

namespace MfmeFmlDecoder.src.Decoder.Component.Profiles.V6_1
{
    internal static class ReelProfileV6_1
    {
        public static readonly ComponentProfile Profile = Build();

        private static ComponentProfile Build()
        {
            ComponentProfile legacy = V19_8.ReelProfileV19_8.Profile.WithVersion(MfmeVersion.V6_1);

            ComponentTagMap inner = new(legacy.Tags.NestedTagBlockMap);
            inner.Add(0x02, new TagInfo(0x01, "InvertedOpto", new byte[] { 0x00 }, ValueRole.BOOLEAN));
            inner.Add(0x13, new TagInfo(0x01, "Unknown 0x13", new byte[] { 0x00 }, ValueRole.BOOLEAN));
            inner.Remove(0x14);
            inner.Add(0x14, new TagInfo(0x04, "Unknown 0x14", new byte[] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.UINT32));
            inner.Add(0x1B, new TagInfo(0x04, "Unknown 0x1B", new byte[] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.UINT32));
            inner.Add(0x1D, new TagInfo(0x01, "Unknown 0x1D", new byte[] { 0x00 }, ValueRole.BYTE));
            inner.Add(0x22, new TagInfo(0x04, "Unknown 0x22", new byte[] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.UINT32));
            inner.Add(0x23, new TagInfo(0x04, "Unknown 0x23", new byte[] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.UINT32));

            ComponentTagMap outer = new(legacy.Tags);
            return legacy
                .WithTags(outer.WithNestedTagBlock(inner))
                .AsLegacyTwoSection();
        }
    }
}
