using MfmeFmlDecoder.Decoder;
using MfmeFmlDecoder.Model;
using MfmeFmlDecoder.src.Decoder.Component.Core;
using MfmeFmlDecoder.src.Decoder.Component.Profiles;
using MfmeFmlDecoder.src.Model.Component;
using System;
using System.Collections.Generic;

namespace MfmeFmlDecoder.src.Decoder.Component
{
    internal sealed class RGBLedParser : ComponentParserBase<RGBLed>
    {
        private Dictionary<uint, string> styleOptions = new Dictionary<uint, string>
        {
            { 0x0, "Round"},
            { 0x1, "Square"},
            { 0x2, "Flat"},
        };

        public RGBLed Parse(ParsingContext context, long componentOffset, uint componentId, byte[] data)
        {
            ComponentProfile profile = ComponentProfileCatalog.Resolve(MFMEComponentType.RGBLed, context.Version);
            // Normalize for MFME angle bug.
            // When angle wire is invalid, SublampCount (0x38) often overlaps the wire bytes;
            // rewrite preserves those tags after a zero-angle + terminator, so extended parse
            // must use parseData and start at the post-terminator offset (delta 0).
            var (component, offset, parseData) = ParseBase(context, profile, componentOffset, componentId, data);

                        var parseResult = ParseExtendedTags(component, profile, parseData, offset);

            ApplyExtendedTags(component, parseResult.ValuesByTag, styleOptions, profile.Defaults);

            return component;
        }

        private const uint UndefinedLampNumberRaw = 0xFFFFFFFE;

        private static readonly IReadOnlyDictionary<int, Action<RGBLed, uint>> lampNumberSetterByChannelIndex =
            new Dictionary<int, Action<RGBLed, uint>>
            {
                [1] = static (component, lampNumber) => component.Int32s.Add("RedLampNumber", (int) lampNumber),
                [2] = static (component, lampNumber) => component.Int32s.Add("GreenLampNumber", (int)lampNumber),
                [3] = static (component, lampNumber) => component.Int32s.Add("BlueLampNumber", (int)lampNumber),
                [4] = static (component, lampNumber) => component.Int32s.Add("WhiteLampNumber", (int)lampNumber),
            };

        private static void ApplyExtendedTags(
            RGBLed component,
            IReadOnlyDictionary<uint, object> valuesByTag,
            Dictionary<uint, string> styleOptions,
            DefaultsProfile defaults)
        {
            ApplyLampNumbersFromSublampTable(component, valuesByTag);
            if (defaults?.DefaultWhiteLampNumberZero == true
                && !component.Int32s.ContainsKey("WhiteLampNumber"))
            {
                component.Int32s["WhiteLampNumber"] = 0;
            }

            if (valuesByTag.TryGetValue(0x03, out var selectedStyle)) component.Strings.Add("Style", styleOptions[(uint)selectedStyle]);
        }

        private static void ApplyLampNumbersFromSublampTable(
            RGBLed component,
            IReadOnlyDictionary<uint, object> valuesByTag)
        {
            if (!valuesByTag.TryGetValue(0x39, out object tableValue)
                || tableValue is not IReadOnlyList<LampSublampTableEntry> sublampTable)
            {
                return;
            }

            foreach (LampSublampTableEntry entry in sublampTable)
            {
                if (!lampNumberSetterByChannelIndex.TryGetValue(entry.SublampIndex, out Action<RGBLed, uint> setter))
                {
                    continue;
                }

                setter(component, NormalizeRgbLedLampNumber(entry.SublampNumber));
            }
        }

        /// <summary>MFME uses <c>0xFFFFFFFE</c> (-2) when a channel lamp number is not defined.</summary>
        private static uint NormalizeRgbLedLampNumber(int rawLampNumber)
        {
            if (unchecked((uint)rawLampNumber) == UndefinedLampNumberRaw)
            {
                return 0;
            }

            return rawLampNumber < 0 ? 0u : (uint)rawLampNumber;
        }
    }
}
