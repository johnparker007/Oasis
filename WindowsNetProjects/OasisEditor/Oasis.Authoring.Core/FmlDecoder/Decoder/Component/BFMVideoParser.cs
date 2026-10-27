using MfmeFmlDecoder.Model;
using MfmeFmlDecoder.src.Model.Component;
using System.Collections.Generic;
using System;
using MfmeFmlDecoder.src.Decoder.Component.Core;

namespace MfmeFmlDecoder.src.Decoder.Component
{
    internal sealed class BFMVideoParser : ComponentParserBase<BFMVideo>
    {
        private Dictionary<uint, string> videoModes = new Dictionary<uint, string>
        {
            { 0x0, "600 x 800 V"},
            { 0x1, "480 x 640 V"},
            { 0x2, "800 x 600 H"},
            { 0x3, "640 x 480 H"},
        };

        private ComponentTagMap componentTagMap = new ComponentTagMap
        {
            { 0x01, new TagInfo(0x04, "VideoMode Index", new byte[] { 0x00 }, ValueRole.UINT32) },
            { 0x36, new TagInfo(0x04, "Overlay Image", new byte[] { }, ValueRole.BITMAP) },
            { 0x33, new TagInfo(0x01, "Unknown 0x33", new byte[] { }, ValueRole.BOOLEAN) },
            { 0x3B, new TagInfo(0x04, "Unknown 0x3B", new byte[] { 0x00 }, ValueRole.UINT32) },
        };

        public BFMVideo Parse(long componentOffset, uint componentId, byte[] data)
        {
            // Normalize for MFME angle bug
            var (component, offset, parseData) = ParseBase(
                componentOffset,
                componentId,
                data,
                normalizationRule: new GeometryAngleNormalization.Rule(
                    RewriteTriggerOffsetDelta: 2,
                    ValidAngleOffsetDelta: 2));

            DumpRemaining(componentOffset, parseData, offset);

            var parseOptions = WithComponentTagLogging(
                ExtendedTagParser.Options
                .WithBitmapTags(0x36)
            );
            var parseResult = ParseExtendedTags(component, componentTagMap, parseData, offset, parseOptions);

            if (parseResult.ValuesByTag.TryGetValue(0x01, out var videoModeDropdownIndex)) component.Strings.Add("VideoMode", videoModes[(uint)videoModeDropdownIndex]);

            return component;
        }
    }
}



