using MfmeFmlDecoder.Decoder;
using MfmeFmlDecoder.Model;

namespace MfmeFmlDecoder.src.Decoder.Component.Profiles.V6_1
{
    internal static class AlphaNewProfileV6_1
    {
        public static readonly ComponentProfile Profile = Build();

        private static ComponentProfile Build()
        {
            ComponentProfile legacy = V19_8.AlphaNewProfileV19_8.Profile.WithVersion(MfmeVersion.V6_1);

            ComponentTagMap tags = new(legacy.Tags);
            tags.Add(0x05, new TagInfo(0x01, "Unknown 0x05", new byte[] { 0x00 }, ValueRole.BOOLEAN));

            return legacy.WithTags(tags);
        }
    }
}
