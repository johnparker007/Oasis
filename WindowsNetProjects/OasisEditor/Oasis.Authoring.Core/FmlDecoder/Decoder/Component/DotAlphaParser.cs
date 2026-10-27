using MfmeFmlDecoder.Model;
using MfmeFmlDecoder.src.Decoder.Component.Core;
using MfmeFmlDecoder.src.Model.Component;
using System;
using System.Collections.Generic;

namespace MfmeFmlDecoder.src.Decoder.Component
{
    internal sealed class DotAlphaParser : ComponentParserBase<DotAlpha>
    {
        private ComponentTagMap componentTagMap = new ComponentTagMap
        {
            { 0x03, new TagInfo(0x04, "Unknown 0x03", new byte[] { }, ValueRole.UINT32) },
            { 0x05, new TagInfo(0x04, "OnColour", new byte[] { 0xFF, 0xFF, 0x00, 0xFF }, ValueRole.ARGB_COLOR) },
            { 0x06, new TagInfo(0x04, "OffColour", new byte[] { 0x2C, 0x2C, 0x00, 0xFF }, ValueRole.ARGB_COLOR) },
            { 0x07, new TagInfo(0x04, "BackgroundColour", new byte[] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.ARGB_COLOR) },
            { 0x01, new TagInfo(0x04, "XSize", new byte[] { 0x02 }, ValueRole.UINT32) },
            { 0x02, new TagInfo(0x04, "YSize", new byte[] { 0x02 }, ValueRole.UINT32) },
            { 0x08, new TagInfo(0x04, "DotSpacing", new byte[] { 0x01 }, ValueRole.UINT32) },
            { 0x04, new TagInfo(0x04, "DigitSpacing", new byte[] { 0x02 }, ValueRole.UINT32) },
            { 0x36, new TagInfo(0x00, "Overlay Image", new byte[] { }, ValueRole.BITMAP) },
            { 0x3B, new TagInfo(0x04, "Unknown 0x3B", new byte[] { 0x00 }, ValueRole.UINT32) },
        };

        public DotAlpha Parse(long componentOffset, uint componentId, byte[] data)
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

            return component;
        }
    }
}
