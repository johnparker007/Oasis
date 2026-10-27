using MfmeFmlDecoder.Model;
using MfmeFmlDecoder.src.Decoder.Component.Core;
using MfmeFmlDecoder.src.Model.Component;
using System;
using System.Collections.Generic;
using static MfmeFmlDecoder.src.Decoder.Component.Core.ExtendedTagParser;

namespace MfmeFmlDecoder.src.Decoder.Component
{
    internal sealed class AstraMatrixParser : ComponentParserBase<AstraMatrix>
    {
        private ComponentTagMap componentTagMap = new ComponentTagMap
        {
            { 0x01, new TagInfo(0x04, "Size", new byte[] { 0x07 }, ValueRole.UINT32) },
            { 0x02, new TagInfo(0x04, "OnColour", new byte[] { 0x00, 0x00, 0xFF, 0xFF }, ValueRole.ARGB_COLOR) },
            { 0x03, new TagInfo(0x04, "OffColour", new byte[] { 0x00, 0x00, 0x00, 0xFF }, ValueRole.ARGB_COLOR) },
            { 0x04, new TagInfo(0x04, "BackgroundColour", new byte[] { 0x00, 0x00, 0x00, 0xFF }, ValueRole.ARGB_COLOR) },
            { 0x3B, new TagInfo(0x04, "Unknown3B", new byte[] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x36, new TagInfo(0x00, "Overlay Image", Array.Empty<byte>(), ValueRole.BITMAP) },
        };

        public AstraMatrix Parse(long componentOffset, uint componentId, byte[] data)
        {
            var (component, offset, parseData) = ParseBase(componentOffset, componentId, data);

            var parseOptions = WithComponentTagLogging(Options.Default);
            var parseResult = ParseExtendedTags(component, componentTagMap, parseData, offset, parseOptions);

            ApplyExtendedTags(component, parseResult, componentTagMap);

            return component;
        }

        private void ApplyExtendedTags(AstraMatrix component, ExtendedTagParser.ParseResult parseResult, ComponentTagMap componentTagMap)
        {

            ApplyExtendedTagsByReflection(component, parseResult, componentTagMap);
        }
    }
}
