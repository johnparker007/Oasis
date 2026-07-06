using MfmeFmlDecoder.Model;
using MfmeFmlDecoder.src.Decoder.Component.Core;
using MfmeFmlDecoder.src.Model.Component;
using System;
using System.Collections.Generic;

namespace MfmeFmlDecoder.src.Decoder.Component
{
    internal sealed class PlasmaDisplayParser : ComponentParserBase<PlasmaDisplay>
    {
        private ComponentTagMap componentTagMap = new ComponentTagMap
        {
            { 0x01, new TagInfo(0x04, "Size", new byte[] { 0x05 }, ValueRole.UINT32) },
            { 0x03, new TagInfo(0x04, "OffColour", new byte[] { 0x00, 0x00, 0x00, 0xFF }, ValueRole.ARGB_COLOR) },
            { 0x02, new TagInfo(0x04, "OnColour", new byte[] { 0x1C, 0x8D, 0xFF, 0xFF }, ValueRole.ARGB_COLOR) },
            { 0x04, new TagInfo(0x04, "BackgroundColour", new byte[] { 0x00, 0x00, 0x00, 0xFF }, ValueRole.ARGB_COLOR) },
            { 0x36, new TagInfo(0x00, "Overlay Image", Array.Empty<byte>(), ValueRole.BITMAP) },
            { 0x3B, new TagInfo(0x04, "Unknown 0x3B", new byte[] { 0x00 }, ValueRole.UINT32) },
        };

        public PlasmaDisplay Parse(long componentOffset, uint componentId, byte[] data)
        {
            // Normalize for MFME angle bug
            var (component, offset, parseData) = ParseBase(
                componentOffset,
                componentId,
                data,
                normalizationRule: new GeometryAngleNormalization.Rule(
                    RewriteTriggerOffsetDelta: -2,
                    ValidAngleOffsetDelta: 0));

            DumpRemaining(componentOffset, data, offset);

            var parseOptions = WithComponentTagLogging(ExtendedTagParser.Options.Default);
            var parseResult = ParseExtendedTags(component, componentTagMap, data, offset, parseOptions);

            return component;
        }
    }
}
