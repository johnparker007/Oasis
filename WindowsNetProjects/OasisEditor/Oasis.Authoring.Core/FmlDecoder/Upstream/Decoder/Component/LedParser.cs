using MfmeFmlDecoder.Decoder;
using MfmeFmlDecoder.Model;
using MfmeFmlDecoder.src.Model.Component;
using System.Collections.Generic;
using System;
using MfmeFmlDecoder.src.Decoder.Component.Core;
using MfmeFmlDecoder.src.Decoder.Component.Profiles;

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


        public Led Parse(ParsingContext context, long componentOffset, uint componentId, byte[] data)
        {
            ComponentProfile profile = ComponentProfileCatalog.Resolve(MFMEComponentType.Led, context.Version);
            // Normalize for MFME angle bug
            var (component, offset, parseData) = ParseBase(context, profile, componentOffset, componentId, data);

            DumpRemaining(componentOffset, parseData, offset);

                        var parseResult = ParseExtendedTags(component, profile, parseData, offset);

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



