using MfmeFmlDecoder.Model;
using MfmeFmlDecoder.src.Model.Component;
using System.Collections.Generic;
using System;
using MfmeFmlDecoder.src.Decoder.Component.Core;

namespace MfmeFmlDecoder.src.Decoder.Component
{
    internal sealed class BFMAlphaParser : ComponentParserBase<BFMAlpha>
    {
        private ComponentTagMap componentTagMap = new ComponentTagMap
        {
            { 0x01, new TagInfo(0x04, "Colour", new byte[] { }, ValueRole.ARGB_COLOR) },
            { 0x02, new TagInfo(0x04, "OffLevel", new byte[] { }, ValueRole.UINT32) },
            { 0x03, new TagInfo(0x04, "DigitWidth", new byte[] { }, ValueRole.UINT32) },
            { 0x04, new TagInfo(0x04, "Columns", new byte[] { }, ValueRole.UINT32) },
            { 0x05, new TagInfo(0x04, "Character Image", new byte[] { }, ValueRole.BITMAP) },
            { 0x40, new TagInfo(0x01, "Reversed", new byte[] { }, ValueRole.BOOLEAN) },
            { 0x36, new TagInfo(0x04, "Overlay Image", new byte[] { }, ValueRole.BITMAP) },
            { 0x3B, new TagInfo(0x04, "Unknown 0x3B", new byte[] { 0x00 }, ValueRole.UINT32) },
        };

        public BFMAlpha Parse(long componentOffset, uint componentId, byte[] data)
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
                ExtendedTagParser.Options.WithBitmapTags(0x05, 0x36));
            var parseResult = ParseExtendedTags(component, componentTagMap, parseData, offset, parseOptions);

            return component;
        }
    }
}



