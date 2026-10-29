using MfmeFmlDecoder.Model;
using System;
using System.Collections.Generic;

namespace MfmeFmlDecoder.src.Decoder.Component.Core
{
    /// <summary>
    /// Builds the tag map for the MFME 20.x base section. The base section uses fixed-width
    /// primitive tags, while the type-specific map often reuses the same tag ids for own-section
    /// fields. Scalar tags whose type-specific meaning conflicts with the base width are read
    /// as opaque base fields here; the own-section parser sees the original type map.
    /// </summary>
    internal static class BaseSectionTagMapBuilder
    {
        internal sealed record BaseSectionTagMap(
            ComponentTagMap Map,
            IReadOnlySet<uint> TypeMappedTags
        );

        private sealed record BaseTagSpec(
            uint Tag,
            int Length,
            ValueRole Role,
            string GenericName,
            bool ForceGeneric = false
        );

        private static readonly BaseTagSpec[] BaseTags =
        {
            new(0x06, 4, ValueRole.UINT32, "Base 0x06", ForceGeneric: true),
            new(0x08, 4, ValueRole.UINT32, "Base 0x08", ForceGeneric: true),
            new(0x09, 1, ValueRole.BYTE, "Base 0x09", ForceGeneric: true),
            new(0x0A, 1, ValueRole.BYTE, "Base 0x0A", ForceGeneric: true),
            new(0x0B, 1, ValueRole.BYTE, "Base 0x0B", ForceGeneric: true),
            new(0x0C, 1, ValueRole.BYTE, "Base 0x0C", ForceGeneric: true),
            new(0x15, 1, ValueRole.BYTE, "Base 0x15"),
            new(0x16, 4, ValueRole.UINT32, "Base 0x16"),
            new(0x17, 1, ValueRole.BYTE, "Base 0x17"),
            new(0x18, 4, ValueRole.UINT32, "Base 0x18"),
            new(0x19, 1, ValueRole.BYTE, "Base 0x19"),
            new(0x1A, 4, ValueRole.UINT32, "Base 0x1A"),
            new(0x1C, 4, ValueRole.UINT32, "Base 0x1C"),
            new(0x1D, 4, ValueRole.UINT32, "Base 0x1D"),
            new(0x1E, 4, ValueRole.UINT32, "Base 0x1E"),
            new(0x1F, 4, ValueRole.UINT32, "Base 0x1F"),
            new(0x20, 4, ValueRole.UINT32, "Base 0x20"),
            new(0x21, 4, ValueRole.UINT32, "Base 0x21"),
            new(0x22, 4, ValueRole.UINT32, "Base 0x22"),
            new(0x23, 4, ValueRole.UINT32, "Base 0x23"),
            new(0x24, 4, ValueRole.UINT32, "Base 0x24"),
            new(0x25, 4, ValueRole.UINT32, "Base 0x25"),
            new(0x26, 4, ValueRole.UINT32, "Base 0x26"),
            new(0x27, 0, ValueRole.FONT, "Base 0x27"),
            new(0x28, 0, ValueRole.TEXT, "Base 0x28"),
            new(0x29, 1, ValueRole.BYTE, "Base 0x29"),
            new(0x2A, 1, ValueRole.BYTE, "Base 0x2A"),
            new(0x2B, 4, ValueRole.UINT32, "Base 0x2B"),
            new(0x2C, 4, ValueRole.UINT32, "Base 0x2C"),
            new(0x2D, 4, ValueRole.UINT32, "Base 0x2D"),
            new(0x2E, 4, ValueRole.UINT32, "Base 0x2E"),
            new(0x2F, 4, ValueRole.UINT32, "Base 0x2F"),
            new(0x30, 4, ValueRole.UINT32, "Base 0x30"),
            new(0x31, 4, ValueRole.UINT32, "Base 0x31"),
            new(0x32, 4, ValueRole.UINT32, "Base 0x32"),
            new(0x33, 4, ValueRole.UINT32, "Base 0x33"),
            new(0x34, 4, ValueRole.UINT32, "Base 0x34"),
            new(0x35, 4, ValueRole.UINT32, "Base 0x35"),
            new(0x36, 0, ValueRole.BITMAP, "Base 0x36"),
            new(0x37, 4, ValueRole.UINT32, "Base 0x37"),
            new(0x38, 4, ValueRole.SUBLAMP_COUNT, "Base 0x38"),
            new(0x39, 4, ValueRole.LAMP_SUBLAMP_TABLE, "Base 0x39"),
            new(0x3A, 4, ValueRole.LAMP_SUBLAMP_TABLE, "Base 0x3A"),
            new(0x3B, 4, ValueRole.UINT32, "Base 0x3B"),
            new(0x3C, 1, ValueRole.BYTE, "Base 0x3C"),
            new(0x3E, 1, ValueRole.BYTE, "Base 0x3E"),
            new(0x3F, 0, ValueRole.TEXT, "Base 0x3F"),
            new(0x40, 1, ValueRole.BYTE, "Base 0x40"),
            new(0x48, 4, ValueRole.UINT32, "Base 0x48"),
            new(0x49, 4, ValueRole.UINT32, "Base 0x49"),
            new(0x4A, 4, ValueRole.UINT32, "Base 0x4A"),
            new(0x4B, 1, ValueRole.BYTE, "Base 0x4B"),
            new(0x4C, 1, ValueRole.BYTE, "Base 0x4C", ForceGeneric: true),
        };

        internal static BaseSectionTagMap Build(ComponentTagMap typeMap)
        {
            if (typeMap is null) throw new ArgumentNullException(nameof(typeMap));

            var map = new ComponentTagMap();
            var typeMappedTags = new HashSet<uint>();

            foreach (BaseTagSpec spec in BaseTags)
            {
                if (!spec.ForceGeneric
                    && typeMap.TryGetValue(spec.Tag, out TagInfo typeTagInfo)
                    && IsCompatible(typeTagInfo, spec))
                {
                    map.Add(
                        spec.Tag,
                        new TagInfo(
                            spec.Role is ValueRole.FONT or ValueRole.TEXT or ValueRole.BITMAP
                                ? typeTagInfo.Length
                                : spec.Length,
                            typeTagInfo.AttributeName,
                            typeTagInfo.DefaultValues,
                            typeTagInfo.Role));
                    typeMappedTags.Add(spec.Tag);
                    continue;
                }

                // Opaque scalar base tags are parsed for alignment but not surfaced as component
                // Values; only the type-mapped base fields keep their names/roles.
                ValueRole genericRole = spec.Role is ValueRole.BYTE or ValueRole.UINT32
                    ? ValueRole.RAW
                    : spec.Role;
                map.Add(
                    spec.Tag,
                    new TagInfo(spec.Length, spec.GenericName, Array.Empty<byte>(), genericRole));
            }

            return new BaseSectionTagMap(map, typeMappedTags);
        }

        private static bool IsCompatible(TagInfo typeTagInfo, BaseTagSpec spec)
        {
            if (typeTagInfo is null) return false;

            return spec.Role switch
            {
                ValueRole.BYTE => typeTagInfo.Role is ValueRole.BYTE or ValueRole.BOOLEAN,
                ValueRole.UINT32 => typeTagInfo.Role is ValueRole.UINT32 or ValueRole.INT32,
                ValueRole.FONT => typeTagInfo.Role == ValueRole.FONT,
                ValueRole.TEXT => typeTagInfo.Role == ValueRole.TEXT,
                ValueRole.BITMAP => typeTagInfo.Role == ValueRole.BITMAP,
                ValueRole.SUBLAMP_COUNT => typeTagInfo.Role == ValueRole.SUBLAMP_COUNT,
                ValueRole.LAMP_SUBLAMP_TABLE => typeTagInfo.Role == ValueRole.LAMP_SUBLAMP_TABLE,
                _ => false,
            };
        }
    }
}
