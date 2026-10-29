using MfmeFmlDecoder.Decoder;
using MfmeFmlDecoder.Model;

namespace MfmeFmlDecoder.src.Decoder.Component.Profiles.V5
{
    internal static class LabelProfileV5
    {
        public static readonly ComponentProfile Profile = Build();

        private static ComponentProfile Build()
        {
            ComponentProfile legacy = V19_8.LabelProfileV19_8.Profile.WithVersion(MfmeVersion.V5);

            ComponentTagMap second = new(legacy.Tags);
            Replace(second, 0x39, new TagInfo(0x04, "Lamp", System.Array.Empty<byte>(), ValueRole.INT32));

            ComponentTagMap first = new(second);
            first.Add(0x28, new TagInfo(0x00, "Label", System.Array.Empty<byte>(), ValueRole.TEXT));

            return legacy with
            {
                BaseTags = first,
                BaseSection = BaseSectionProfile.LegacyTwoSection(second),
                Options = legacy.Options with { LegacyAnsiText = true },
            };
        }

        private static void Replace(ComponentTagMap map, uint tag, TagInfo info)
        {
            if (map.ContainsKey(tag))
            {
                map.Remove(tag);
            }

            map.Add(tag, info);
        }
    }
}
