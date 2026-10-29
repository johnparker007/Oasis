using MfmeFmlDecoder.Decoder;
using MfmeFmlDecoder.Model;
using MfmeFmlDecoder.src.Decoder.Component.Core;
using MfmeFmlDecoder.src.Decoder.Component.Profiles;
using MfmeFmlDecoder.src.Model.Component;
using System;
using System.Collections.Generic;

namespace MfmeFmlDecoder.src.Decoder.Component
{
    internal sealed class FrameParser : ComponentParserBase<Frame>
    {
        private Dictionary<uint, string> shapeOptions = new Dictionary<uint, string>
        {
            { 0x0, "Box"},
            { 0x1, "Frame"},
            { 0x2, "Top Line"},
            { 0x3, "Bottom Line"},
            { 0x4, "Left Line"},
            { 0x5, "Right Line"},
        };


        private Dictionary<uint, string> bevelOptions = new Dictionary<uint, string>
        {
            { 0x0, "Lowered"},
            { 0x1, "Raised"},
        };


        public Frame Parse(ParsingContext context, long componentOffset, uint componentId, byte[] data)
        {
            ComponentProfile profile = ComponentProfileCatalog.Resolve(MFMEComponentType.Frame, context.Version);
            // Normalize for MFME angle bug
            var (component, offset, parseData) = ParseBase(context, profile, componentOffset, componentId, data);

            DumpRemaining(componentOffset, parseData, offset);

                        var parseResult = ParseExtendedTags(component, profile, parseData, offset);

            if (parseResult.ValuesByTag.TryGetValue(0x01, out var selectedShapeOption)) component.Strings.Add("Shape", shapeOptions[(uint)selectedShapeOption]);
            if (parseResult.ValuesByTag.TryGetValue(0x02, out var selectedBevelOption)) component.Strings.Add("Bevel", bevelOptions[(uint)selectedBevelOption]);

            return component;
        }
    }
}
