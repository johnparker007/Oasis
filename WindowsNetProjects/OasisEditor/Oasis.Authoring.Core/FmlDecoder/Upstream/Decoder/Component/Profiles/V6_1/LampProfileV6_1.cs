using MfmeFmlDecoder.Decoder;
using MfmeFmlDecoder.Model;

namespace MfmeFmlDecoder.src.Decoder.Component.Profiles.V6_1
{
    internal static class LampProfileV6_1
    {
        public static readonly ComponentProfile Profile = Build();

        private static ComponentProfile Build()
        {
            ComponentProfile profile = V19_0.LampProfileV19_0.Profile.WithVersion(MfmeVersion.V6_1);

            ComponentTagMap inner = new(profile.Tags.NestedTagBlockMap);
            AddRaw(inner, 0x2A, 0x31, length: 0x10);
            AddRaw(inner, 0x3E, 0x41, length: 0x10);
            AddRaw(inner, 0x56, 0x59, length: 0x10);
            AddRaw(inner, 0x5C, 0x67, length: 0x18);
            if (!inner.ContainsKey(0x5B))
            {
                inner.Add(0x5B, new TagInfo(0x10, "Unknown 0x5B", new byte[0x10], ValueRole.RAW));
            }

            ComponentTagMap outer = new(profile.Tags);
            DefaultsProfile defaults = profile.Defaults with { LegacyTokenEffectDefaults = true };
            return profile
                .WithDefaults(defaults)
                .WithTags(outer.WithNestedTagBlock(inner))
                .AsLegacyTwoSection();
        }

        private static void AddRaw(ComponentTagMap map, uint first, uint last, int length)
        {
            for (uint tag = first; tag <= last; tag++)
            {
                if (!map.ContainsKey(tag))
                {
                    map.Add(tag, new TagInfo(length, $"Unknown 0x{tag:X2}", new byte[length], ValueRole.RAW));
                }
            }
        }
    }
}
