using MfmeFmlDecoder.Decoder;
using MfmeFmlDecoder.Model;

namespace MfmeFmlDecoder.src.Decoder.Component.Profiles.V6_1
{
    internal static class RGBLedProfileV6_1
    {
        public static readonly ComponentProfile Profile = Build();

        private static ComponentProfile Build()
        {
            ComponentProfile legacy = V19_8.RGBLedProfileV19_8.Profile.WithVersion(MfmeVersion.V6_1);
            ComponentTagMap tags = new(legacy.Tags);

            SetColourDefault(tags, 0x09, "AdjustedRed", 0x00, 0x00, 0xFF);
            SetColourDefault(tags, 0x0A, "AdjustedGreen", 0x00, 0xFF, 0x00);
            SetColourDefault(tags, 0x0B, "AdjustedRedGreen", 0x00, 0xFF, 0xFF);
            SetColourDefault(tags, 0x0C, "AdjustedBlue", 0xFF, 0x00, 0x00);
            SetColourDefault(tags, 0x0D, "AdjustedRedBlue", 0xFF, 0x00, 0xFF);
            SetColourDefault(tags, 0x0E, "AdjustedRedGreen2", 0xFF, 0xFF, 0x00);
            SetColourDefault(tags, 0x0F, "AdjustedRedGreenBlue", 0xFF, 0xFF, 0xFF);

            return legacy.WithTags(tags);
        }

        private static void SetColourDefault(ComponentTagMap tags, uint tag, string name, byte blue, byte green, byte red)
        {
            if (!tags.TryGetValue(tag, out TagInfo info))
            {
                return;
            }

            tags.Remove(tag);
            tags.Add(tag, new TagInfo(
                info.Length,
                name,
                new[] { blue, green, red, (byte)0xFF },
                info.Role));
        }
    }
}
