using MfmeFmlDecoder.Decoder;
using MfmeFmlDecoder.Model;
using MfmeFmlDecoder.src.Decoder.Component.Core;
using MfmeFmlDecoder.src.Decoder.Component.Profiles;
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

        public SevenSegBlock Parse(ParsingContext context, long componentOffset, uint componentId, byte[] data)
        {
            ComponentProfile profile = ComponentProfileCatalog.Resolve(MFMEComponentType.SevenSegBlock, context.Version);
            var (component, offset, parseData) = ParseBase(context, profile, componentOffset, componentId, data);

                        var parseResult = ParseExtendedTags(component, profile, parseData, offset);

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
