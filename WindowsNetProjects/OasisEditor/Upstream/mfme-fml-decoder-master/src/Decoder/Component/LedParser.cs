using MfmeFmlDecoder.Model;
using MfmeFmlDecoder.src.Model.Component;
using System.Collections.Generic;
using System;
using MfmeFmlDecoder.src.Decoder.Component.Core;

namespace MfmeFmlDecoder.src.Decoder.Component
{
    internal sealed class LedParser : ComponentParserBase<Led>
    {
        private Dictionary<uint, string> styleOptions = new Dictionary<uint, string>
        {
            { 0x0, "Round"},
            { 0x1, "Square"},
        };

        private Dictionary<uint, string> segmentOptions = new Dictionary<uint, string>
        {
            { 0x0, ""},
            { 0x1, "Top"},
            { 0x2, "Top Right"},
            { 0x3, "Bottom Right"},
            { 0x4, "Bottom"},
            { 0x5, "Bottom Left"},
            { 0x6, "Top Left"},
            { 0x7, "Centre"},
            { 0x8, "DP"},
        };

        private ComponentTagMap componentTagMap = new ComponentTagMap
        {
            { 0x1C, new TagInfo(0x04, "Unknown 0x1C", new byte[] { }, ValueRole.UINT32) },
            { 0x38, new TagInfo(0x04, "Unknown 0x38", new byte[] { }, ValueRole.UINT32) },
            { 0x39, new TagInfo(0x04, "Unknown 0x39", new byte[] { }, ValueRole.UINT32) },
            { 0x3B, new TagInfo(0x04, "Unknown 0x3B", new byte[] { }, ValueRole.UINT32) },
            { 0x01, new TagInfo(0x04, "OnColour", new byte[] { }, ValueRole.ARGB_COLOR) },
            { 0x02, new TagInfo(0x04, "OffColour", new byte[] { }, ValueRole.ARGB_COLOR) },
            { 0x03, new TagInfo(0x04, "SelectedStyleIndex", new byte[] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x04, new TagInfo(0x04, "SelectedSegmentIndex", new byte[] { 0xFF, 0xFF, 0xFF, 0xFF }, ValueRole.INT32) },
            { 0x05, new TagInfo(0x01, "Led", new byte[] { 0x00 }, ValueRole.BOOLEAN) },
            { 0x06, new TagInfo(0x01, "NoOutline", new byte[] { 0x00 }, ValueRole.BOOLEAN) },
            { 0x07, new TagInfo(0x01, "NoShadow", new byte[] { 0x00 }, ValueRole.BOOLEAN) },
            { 0x36, new TagInfo(0x00, "Overlay Image", Array.Empty<byte>(), ValueRole.BITMAP) },
        };

        public Led Parse(long componentOffset, uint componentId, byte[] data)
        {
            // Normalize for MFME angle bug
            var (component, offset, parseData) = ParseBase(
                componentOffset,
                componentId,
                data,
                normalizationRule: new GeometryAngleNormalization.Rule(
                    RewriteTriggerOffsetDelta: 5,
                    ValidAngleOffsetDelta: 0));

            DumpRemaining(componentOffset, parseData, offset);

            var parseOptions = WithComponentTagLogging(
                ExtendedTagParser.Options
                .WithBitmapTags(0x36)
            );
            var parseResult = ParseExtendedTags(component, componentTagMap, parseData, offset, parseOptions);

            ApplyExtendedTags(component, parseResult.ValuesByTag, styleOptions, segmentOptions);

            return component;
        }

        private static void ApplyExtendedTags(Led component, System.Collections.Generic.IReadOnlyDictionary<uint, object> valuesByTag, Dictionary<uint, string> styleOptions, Dictionary<uint, string> segmentOptions)
        {
            if (valuesByTag.TryGetValue(0x03, out var selectedStyleIndex)) component.Strings.Add("Style",  styleOptions[(uint)selectedStyleIndex]);
            if (valuesByTag.TryGetValue(0x04, out var selectedSegmentIndex)) component.Strings.Add("Segment", segmentOptions[(uint)((int)selectedSegmentIndex + 1)]);
            // If there is a selected segment then the number should become digit
            if ((int)selectedSegmentIndex > -1)
            {
                component.UInt32s.Add("Digit", (uint) component.Number);
            }
        }
    }
}



