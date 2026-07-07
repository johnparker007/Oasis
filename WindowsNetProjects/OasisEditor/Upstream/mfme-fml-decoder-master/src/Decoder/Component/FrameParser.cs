using MfmeFmlDecoder.Model;
using MfmeFmlDecoder.src.Decoder.Component.Core;
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

        private ComponentTagMap componentTagMap = new ComponentTagMap
        {
            { 0x01, new TagInfo(0x04, "ShapeSelectedIndex", new byte[] { 0x00 }, ValueRole.UINT32) },
            { 0x02, new TagInfo(0x04, "BevelSelectedIndex", new byte[] { 0x00 }, ValueRole.UINT32) },
            { 0x36, new TagInfo(0x00, "Overlay Image", new byte[] { }, ValueRole.BITMAP) },
            { 0x3B, new TagInfo(0x04, "Unknown 0x3B", new byte[] { 0x00 }, ValueRole.UINT32) },
        };

        public Frame Parse(long componentOffset, uint componentId, byte[] data)
        {
            // Normalize for MFME angle bug
            var (component, offset, parseData) = ParseBase(
                componentOffset,
                componentId,
                data,
                normalizationRule: new GeometryAngleNormalization.Rule(
                    RewriteTriggerOffsetDelta: 0,
                    ValidAngleOffsetDelta: 0));

            DumpRemaining(componentOffset, parseData, offset);

            var parseOptions = WithComponentTagLogging(
                ExtendedTagParser.Options
                .WithBitmapTags(0x36)
            );
            var parseResult = ParseExtendedTags(component, componentTagMap, parseData, offset, parseOptions);

            if (parseResult.ValuesByTag.TryGetValue(0x01, out var selectedShapeOption)) component.Strings.Add("Shape", shapeOptions[(uint)selectedShapeOption]);
            if (parseResult.ValuesByTag.TryGetValue(0x02, out var selectedBevelOption)) component.Strings.Add("Bevel", bevelOptions[(uint)selectedBevelOption]);

            return component;
        }
    }
}
