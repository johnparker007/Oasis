using MfmeFmlDecoder.Decoder;
using MfmeFmlDecoder.Model;

namespace MfmeFmlDecoder.src.Decoder.Component.Profiles.V5
{
    internal static class LampProfileV5
    {
        private static readonly uint[] BaseOverrideTags =
        {
            0x15, 0x16, 0x17, 0x18, 0x19, 0x1A, 0x1C, 0x27,
            0x29, 0x2A, 0x2B, 0x36, 0x38, 0x39, 0x3B, 0x3C, 0x3E,
            0x48, 0x49, 0x4A, 0x4C,
        };

        public static readonly ComponentProfile Profile = Build();

        private static ComponentProfile Build()
        {
            ComponentProfile legacy = V6_1.LampProfileV6_1.Profile.WithVersion(MfmeVersion.V5);
            ComponentTagMap inner = new(legacy.Tags.NestedTagBlockMap);

            ComponentTagMap second = new(inner);
            Replace(second, 0x1B, new TagInfo(0x00, "On1Text", System.Array.Empty<byte>(), ValueRole.TEXT));
            Replace(second, 0x1C, new TagInfo(0x00, "On2Text", System.Array.Empty<byte>(), ValueRole.TEXT));
            Replace(second, 0x1F, new TagInfo(0x00, "On3Text", System.Array.Empty<byte>(), ValueRole.TEXT));
            Replace(second, 0x20, new TagInfo(0x00, "On4Text", System.Array.Empty<byte>(), ValueRole.TEXT));

            ComponentTagMap merged = new(inner);
            ComponentTagMap baseTags = legacy.EffectiveBaseTags;

            foreach (uint tag in BaseOverrideTags)
            {
                if (baseTags.TryGetValue(tag, out TagInfo info))
                {
                    Replace(merged, tag, info);
                }
            }

            Replace(merged, 0x28, new TagInfo(
                0x00,
                "OffText",
                System.Array.Empty<byte>(),
                ValueRole.TEXT));

            return legacy with
            {
                Tags = merged,
                BaseTags = merged,
                BaseSection = BaseSectionProfile.LegacyTwoSection(second),
                Options = legacy.Options with { LegacyAnsiText = true },
                Defaults = legacy.Defaults with { ClearDuplicateLampShortcut2 = true, LegacyBbTokenEffects = true },
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
