using MfmeFmlDecoder.Model;
using MfmeFmlDecoder.src.Decoder.Component.Core;
using MfmeFmlDecoder.src.Model.Component;
using System;
using System.Collections.Generic;

namespace MfmeFmlDecoder.src.Decoder.Component
{
    internal sealed class SevenSegBlockParser : ComponentParserBase<SevenSegBlock>
    {
        // Programmable-digit mask (0x0B) and lamp table (0x1B).
        private const uint ProgrammableDigitMaskTag = 0x0B;
        private const uint ProgrammableLampTableTag = 0x1B;

        // Single lamp number per digit (used by non-programmable digits; 0 for programmable ones).
        private const uint SubLampTableTag = 0x39;

        // Display style selector (0x10): maps the raw type id to a friendly style name.
        private const uint SelectedTypeTag = 0x10;
        private const string SelectedTypeIdAttribute = "SelectedTypeId";
        private static readonly IReadOnlyDictionary<byte, string> SelectedTypeOptions = new Dictionary<byte, string>
        {
            { 0x00, "Style 1" },
            { 0x01, "Style 2" },
            { 0x02, "Style 3" },
        };

        // Per-digit boolean display-flag arrays, keyed by tag id -> flag name.
        private static readonly IReadOnlyDictionary<uint, string> DigitFlagTagNames = new Dictionary<uint, string>
        {
            { 0x09, "DPOn" },
            { 0x0B, "Programmable" },
            { 0x0D, "ZeroOn" },
            { 0x11, "AutoDP" },
            { 0x13, "Visible" },
            { 0x1D, "DPOff" },
        };

        // All SevenSegBlock TLV tags live inside the nested tag block (opened by 4C ?? 00).
        private ComponentTagMap componentTagMap = new ComponentTagMap
        {
            { 0x38, new TagInfo(0x04, "SublampCount", new byte[] { 0x00 }, ValueRole.SUBLAMP_COUNT) },
            { 0x39, new TagInfo(0x04, "SublampTable", new byte[] { 0x00 }, ValueRole.LAMP_SUBLAMP_TABLE) },
            { 0x3B, new TagInfo(0x04, "Unknown 0x3B", Array.Empty<byte>(), ValueRole.UINT32) }
        }.WithNestedTagBlock(new ComponentTagMap
        {
            { 0x15, new TagInfo(0x04, "Columns", new byte[] { 0x01, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x14, new TagInfo(0x04, "Rows", new byte[] { 0x01, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x16, new TagInfo(0x04, "RowSpacing", new byte[] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x17, new TagInfo(0x04, "ColumnSpacing", new byte[] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x19, new TagInfo(0x04, "DigitWidth", new byte[] { 0x18, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x1A, new TagInfo(0x04, "DigitHeight", new byte[] { 0x20, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x10, new TagInfo(0x01, "SelectedTypeId", new byte[] { 0x00 }, ValueRole.BYTE) },
            { 0x08, new TagInfo(0x01, "DPRight", new byte[] { 0x01 }, ValueRole.BOOLEAN) },
            { 0x0A, new TagInfo(0x01, "Segment14", new byte[] { 0x00 }, ValueRole.BOOLEAN) },
            { 0x01, new TagInfo(0x04, "Thickness", new byte[] { 0x03, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x02, new TagInfo(0x04, "Spacing", new byte[] { 0x01, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x03, new TagInfo(0x04, "HorizontalSizePercent", new byte[] { 0x40, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x04, new TagInfo(0x04, "VerticalSizePercent", new byte[] { 0x4E, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x05, new TagInfo(0x04, "BackColour", new byte[] { 0x00, 0x00, 0xFF, 0xFF }, ValueRole.ARGB_COLOR) },
            { 0x06, new TagInfo(0x04, "OnColour", new byte[] { 0xFF, 0x00, 0x00, 0xFF }, ValueRole.ARGB_COLOR) },
            { 0x07, new TagInfo(0x04, "OffColour", new byte[] { 0x00, 0xFF, 0x00, 0xFF }, ValueRole.ARGB_COLOR) },
            { 0x1C, new TagInfo(0x04, "Offset", new byte[] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x12, new TagInfo(0x04, "DigitAngle", new byte[] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x0C, new TagInfo(0x04, "Slant", new byte[] { 0x06, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x0E, new TagInfo(0x04, "Chop", new byte[] { 0x4B, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x0F, new TagInfo(0x04, "Centre", new byte[] { 0x32, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x0B, new TagInfo(48, "ProgrammableDigitMask", Array.Empty<byte>(), ValueRole.PROGRAMMABLE_DIGIT_MASK) },
            { 0x1B, new TagInfo(0x00, "ProgrammableLampTable", Array.Empty<byte>(), ValueRole.PROGRAMMABLE_LAMP_TABLE) },
            { 0x09, new TagInfo(48, "DPOn", Array.Empty<byte>(), ValueRole.DIGIT_FLAG_ARRAY) },
            { 0x0D, new TagInfo(48, "ZeroOn", Array.Empty<byte>(), ValueRole.DIGIT_FLAG_ARRAY) },
            { 0x11, new TagInfo(48, "AutoDP", Array.Empty<byte>(), ValueRole.DIGIT_FLAG_ARRAY) },
            { 0x13, new TagInfo(48, "Visible", Array.Empty<byte>(), ValueRole.DIGIT_FLAG_ARRAY) },
            { 0x1D, new TagInfo(48, "DPOff", Array.Empty<byte>(), ValueRole.DIGIT_FLAG_ARRAY) },
            { 0x36, new TagInfo(0x00, "Overlay Image", Array.Empty<byte>(), ValueRole.BITMAP) },
        });

        public SevenSegBlock Parse(long componentOffset, uint componentId, byte[] data)
        {
            var (component, offset, parseData) = ParseBase(
                componentOffset,
                componentId,
                data,
                normalizationRule: new GeometryAngleNormalization.Rule(
                    RewriteTriggerOffsetDelta: 4,
                    ValidAngleOffsetDelta: 0));

            var parseOptions = WithComponentTagLogging(ExtendedTagParser.Options.Default);
            var parseResult = ParseExtendedTags(component, componentTagMap, parseData, offset, parseOptions);

            // The digit flags (0x09..0x1D), programmable mask (0x0B) and lamp table (0x1B) live inside
            // the nested tag block, while the sublamp table (0x38/0x39) sits in the outer section. Decode
            // from a merged view so every applier sees the tag it needs regardless of scope.
            ApplyExtendedTags(component, CollectValuesByTag(parseResult));

            return component;
        }

        // Merge the outer and nested TLV values. The two scopes hold disjoint tags for SevenSegBlock
        // (outer: 0x38/0x39/0x3B; nested: everything else), so there are no key collisions.
        private static IReadOnlyDictionary<uint, object> CollectValuesByTag(
            ExtendedTagParser.ParseResult parseResult)
        {
            Dictionary<uint, object> merged = new(parseResult.ValuesByTag);

            var nestedResult = parseResult.NestedTagBlockResult;
            if (nestedResult is not null)
            {
                foreach (var (tag, value) in nestedResult.ValuesByTag)
                {
                    merged[tag] = value;
                }
            }

            return merged;
        }

        private static void ApplyExtendedTags(
            SevenSegBlock component,
            IReadOnlyDictionary<uint, object> valuesByTag)
        {
            ApplyDigitFlags(component, valuesByTag);
            ApplyProgrammableLampNumbers(component, valuesByTag);
            ApplyDigitLampNumbers(component, valuesByTag);
            ApplySelectedType(component, valuesByTag);
        }

        // Resolve the raw SelectedTypeId byte to a friendly style name, dropping the raw id.
        private static void ApplySelectedType(
            SevenSegBlock component,
            IReadOnlyDictionary<uint, object> valuesByTag)
        {
            if (valuesByTag.TryGetValue(SelectedTypeTag, out object value)
                && value is byte typeId
                && SelectedTypeOptions.TryGetValue(typeId, out string styleName))
            {
                component.Strings["SelectedType"] = styleName;
            }

            component.Bytes.Remove(SelectedTypeIdAttribute);
        }

        // The SubLampTable holds one lamp number per digit (indexed by sublamp/digit index).
        // Non-programmable digits carry their single lamp number here; programmable digits store 0.
        private static void ApplyDigitLampNumbers(
            SevenSegBlock component,
            IReadOnlyDictionary<uint, object> valuesByTag)
        {
            if (!valuesByTag.TryGetValue(SubLampTableTag, out object tableValue)
                || tableValue is not IReadOnlyList<LampSublampTableEntry> subLampTable
                || subLampTable.Count == 0)
            {
                return;
            }

            int[] digitLampNumbers = new int[subLampTable.Count];
            foreach (LampSublampTableEntry entry in subLampTable)
            {
                int digitIndex = entry.SublampIndex - 1;
                if (digitIndex >= 0 && digitIndex < digitLampNumbers.Length)
                {
                    digitLampNumbers[digitIndex] = entry.SublampNumber;
                }
            }

            component.DigitLampNumbers = digitLampNumbers;
        }

        private static void ApplyDigitFlags(
            SevenSegBlock component,
            IReadOnlyDictionary<uint, object> valuesByTag)
        {
            Dictionary<string, bool[]> digitFlags = new();
            foreach (var (tag, name) in DigitFlagTagNames)
            {
                if (!valuesByTag.TryGetValue(tag, out object value))
                {
                    continue;
                }

                bool[] flags = value switch
                {
                    bool[] boolArray => boolArray,
                    byte[] byteArray => ToBoolArray(byteArray),
                    _ => null,
                };

                if (flags is not null)
                {
                    digitFlags[name] = flags;
                }
            }

            if (digitFlags.Count > 0)
            {
                component.DigitFlags = digitFlags;
            }
        }

        private static void ApplyProgrammableLampNumbers(
            SevenSegBlock component,
            IReadOnlyDictionary<uint, object> valuesByTag)
        {
            if (!valuesByTag.TryGetValue(ProgrammableDigitMaskTag, out object maskValue)
                || maskValue is not byte[] mask)
            {
                return;
            }

            if (!valuesByTag.TryGetValue(ProgrammableLampTableTag, out object tableValue)
                || tableValue is not IReadOnlyList<uint[]> digitBlocks)
            {
                return;
            }

            Dictionary<int, uint[]> programmableLampNumbers = new();
            int digitCount = Math.Min(mask.Length, digitBlocks.Count);
            for (int digitIndex = 0; digitIndex < digitCount; digitIndex++)
            {
                if (mask[digitIndex] != 0x00)
                {
                    programmableLampNumbers[digitIndex] = digitBlocks[digitIndex];
                }
            }

            component.ProgrammableLampNumbers = programmableLampNumbers;
        }

        private static bool[] ToBoolArray(byte[] bytes)
        {
            bool[] flags = new bool[bytes.Length];
            for (int i = 0; i < bytes.Length; i++)
            {
                flags[i] = bytes[i] != 0x00;
            }

            return flags;
        }
    }
}
