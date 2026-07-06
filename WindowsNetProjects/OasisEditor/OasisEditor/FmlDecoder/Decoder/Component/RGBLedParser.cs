using MfmeFmlDecoder.Model;
using MfmeFmlDecoder.src.Decoder.Component.Core;
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
        private ComponentTagMap componentTagMap = new ComponentTagMap
        {
            { 0x1C, new TagInfo(0x04, "Unknown 0x1C", new byte[] { 0x00 }, ValueRole.UINT32) },
            { 0x06, new TagInfo(0x01, "MaxLED", new byte[] { 0x01 }, ValueRole.BOOLEAN) },
            { 0x07, new TagInfo(0x01, "NoOutline", new byte[] { 0x00 }, ValueRole.BOOLEAN) },
            { 0x08, new TagInfo(0x01, "NoShadow", new byte[] { 0x00 }, ValueRole.BOOLEAN) },
            { 0x03, new TagInfo(0x04, "SelectedStyle", new byte[] { 0x00 }, ValueRole.UINT32) },

            { 0x02, new TagInfo(0x04, "AdjustedOff", new byte[] { 0x00, 0x00, 0x00, 0xFF }, ValueRole.ARGB_COLOR) },
            { 0x09, new TagInfo(0x04, "AdjustedRed", new byte[] { 0x00, 0x00, 0x00, 0xFF }, ValueRole.ARGB_COLOR) },
            { 0x0A, new TagInfo(0x04, "AdjustedGreen", new byte[] { 0x00, 0x00, 0x00, 0xFF }, ValueRole.ARGB_COLOR) },
            { 0x0B, new TagInfo(0x04, "AdjustedRedGreen", new byte[] { 0x00, 0x00, 0x00, 0xFF }, ValueRole.ARGB_COLOR) },
            { 0x0C, new TagInfo(0x04, "AdjustedBlue", new byte[] { 0x00, 0x00, 0x00, 0xFF }, ValueRole.ARGB_COLOR) },
            { 0x0D, new TagInfo(0x04, "AdjustedRedBlue", new byte[] { 0x00, 0x00, 0x00, 0xFF }, ValueRole.ARGB_COLOR) },
            { 0x0E, new TagInfo(0x04, "AdjustedRedGreen2", new byte[] { 0x00, 0x00, 0x00, 0xFF }, ValueRole.ARGB_COLOR) },
            { 0x0F, new TagInfo(0x04, "AdjustedRedGreenBlue", new byte[] { 0x00, 0x00, 0x00, 0xFF }, ValueRole.ARGB_COLOR) },

            { 0x38, new TagInfo(0x04, "SublampCount", new byte[] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.SUBLAMP_COUNT) },
            { 0x39, new TagInfo(0x00, "LampSublampTable", Array.Empty<byte>(), ValueRole.LAMP_SUBLAMP_TABLE) },

            { 0x36, new TagInfo(0x00, "Overlay Image", Array.Empty<byte>(), ValueRole.BITMAP) },
            { 0x3B, new TagInfo(0x04, "Unknown 0x3B", new byte[] { 0x00 }, ValueRole.UINT32) },
        };

        public RGBLed Parse(long componentOffset, uint componentId, byte[] data)
        {
            // Normalize for MFME angle bug
            var (component, offset, parseData) = ParseBase(
                componentOffset,
                componentId,
                data,
                normalizationRule: new GeometryAngleNormalization.Rule(
                    RewriteTriggerOffsetDelta: -1,
                    ValidAngleOffsetDelta: 0));

            DumpRemaining(componentOffset, data, offset);

            var parseOptions = WithComponentTagLogging(ExtendedTagParser.Options.Default);
            var parseResult = ParseExtendedTags(component, componentTagMap, data, offset, parseOptions);

            ApplyExtendedTags(component, parseResult.ValuesByTag, styleOptions);

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

        private static void ApplyExtendedTags(RGBLed component, IReadOnlyDictionary<uint, object> valuesByTag, Dictionary<uint, string> styleOptions)
        {
            ApplyLampNumbersFromSublampTable(component, valuesByTag);
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
