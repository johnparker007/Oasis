using MfmeFmlDecoder.Decoder;
using MfmeFmlDecoder.Model;

namespace MfmeFmlDecoder.src.Decoder.Component.Profiles.V6_1
{
    internal static class BandReelProfileV6_1
    {
        public static readonly ComponentProfile Profile = Build();

        private static ComponentProfile Build()
        {
            ComponentProfile legacy = V19_8.BandReelProfileV19_8.Profile.WithVersion(MfmeVersion.V6_1);
            ComponentTagMap tags = new(legacy.Tags);
            tags.Remove(0x0B);
            tags.Add(0x0B, new TagInfo(0x01, "Unknown 0x0B", new byte[] { 0x00 }, ValueRole.BOOLEAN));
            return legacy.WithTags(tags);
        }
    }
}
