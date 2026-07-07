using MfmeFmlDecoder.Model;
using MfmeFmlDecoder.src.Model.Component;
using System.Collections.Generic;
using System;
using MfmeFmlDecoder.src.Decoder.Component.Core;

namespace MfmeFmlDecoder.src.Decoder.Component
{
    internal sealed class BFMLEDParser : ComponentParserBase<BFMLED>
    {
        private ComponentTagMap componentTagMap = new ComponentTagMap
        {
            { 0x04, new TagInfo(0x04, "OnColour", new byte[] { 0x00, 0x00, 0x00, 0xFF }, ValueRole.ARGB_COLOR) },
            { 0x05, new TagInfo(0x04, "OffColour", new byte[] { 0x00, 0x00, 0x00, 0xFF }, ValueRole.ARGB_COLOR) },
            { 0x06, new TagInfo(0x04, "BackColour", new byte[] { 0x00, 0x00, 0x00, 0xFF }, ValueRole.ARGB_COLOR) },
            { 0x01, new TagInfo(0x04, "XSize", new byte[] { 0x02 }, ValueRole.UINT32) },
            { 0x02, new TagInfo(0x04, "YSize", new byte[] { 0x02 }, ValueRole.UINT32) },
            { 0x03, new TagInfo(0x04, "DigitSpacing", new byte[] { 0x02 }, ValueRole.UINT32) },
            { 0x07, new TagInfo(0x04, "LedSize", new byte[] { 0x07 }, ValueRole.UINT32) },
            { 0x33, new TagInfo(0x01, "Unknown 0x33", new byte[] { }, ValueRole.BOOLEAN) },
            { 0x36, new TagInfo(0x04, "Overlay Image", new byte[] { }, ValueRole.BITMAP) },
            { 0x3B, new TagInfo(0x04, "Unknown 0x3B", new byte[] { 0x00 }, ValueRole.UINT32) },
        };

        public BFMLED Parse(long componentOffset, uint componentId, byte[] data)
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
                ExtendedTagParser.Options.WithBitmapTags(0x36));
            var parseResult = ParseExtendedTags(component, componentTagMap, parseData, offset, parseOptions);

            return component;
        }
    }
}



