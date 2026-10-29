using MfmeFmlDecoder.Decoder;
using MfmeFmlDecoder.Model;

namespace MfmeFmlDecoder.src.Decoder.Component.Profiles.V6_1
{
    internal static class BFMAlphaProfileV6_1
    {
        public static readonly ComponentProfile Profile = Build();

        private static ComponentProfile Build()
        {
            ComponentProfile legacy = V19_8.BFMAlphaProfileV19_8.Profile.WithVersion(MfmeVersion.V6_1);
            ComponentTagMap tags = new(legacy.Tags);
            tags.Add(0x06, new TagInfo(0x01, "Unknown 0x06", new byte[] { 0x00 }, ValueRole.BOOLEAN));

            if (tags.TryGetValue(0x40, out TagInfo reversedInfo))
            {
                tags.Remove(0x40);
                tags.Add(0x40, new TagInfo(
                    reversedInfo.Length,
                    reversedInfo.AttributeName,
                    new byte[] { 0x01 },
                    reversedInfo.Role));
            }

            return legacy.WithTags(tags);
        }
    }
}
