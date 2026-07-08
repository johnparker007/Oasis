using MfmeFmlDecoder.Model;
using MfmeFmlDecoder.src.Decoder.Component.Core;
using MfmeFmlDecoder.src.Model.Component;
using System;
using static MfmeFmlDecoder.src.Decoder.Component.Core.ExtendedTagParser;

namespace MfmeFmlDecoder.src.Decoder.Component
{
    internal sealed class BorderParser : ComponentParserBase<Border>
    {
        private ComponentTagMap componentTagMap = new ComponentTagMap
        {
            // TODO: TOTAL HACK -- we need to do a proper job here
            { 0x0A, new TagInfo(0x01, "Unknown 0x0A", new byte[] { 0x00 }, ValueRole.BOOLEAN) },
            { 0x0B, new TagInfo(0x01, "Unknown 0x0B", new byte[] { 0x00 }, ValueRole.BOOLEAN) },
            { 0x37, new TagInfo(0x04, "Unknown 0x37", new byte[] { 0x00 }, ValueRole.UINT32) },
            { 0x36, new TagInfo(0x00, "Overlay Image", Array.Empty<byte>(), ValueRole.BITMAP) },
            
            { 0x3B, new TagInfo(0x04, "Unknown 0x3B", new byte[] { 0x00 }, ValueRole.UINT32) },
            { 0x06, new TagInfo(0x08, "Unknown 0x06", new byte[] { 0x00 }, ValueRole.RAW) },



        }.WithNestedTagBlock(new ComponentTagMap
        {
            { 0x03, new TagInfo(0x00, "Border Image", Array.Empty<byte>(), ValueRole.BITMAP) },
            { 0x01, new TagInfo(0x04, "Unknown 0x01", new byte[] { 0x00 }, ValueRole.ARGB_COLOR) },
            { 0x05, new TagInfo(0x04, "Unknown 0x05", new byte[] { 0x00 }, ValueRole.ARGB_COLOR) },
            { 0x06, new TagInfo(0x08, "Unknown 0x06", new byte[] { 0x00 }, ValueRole.RAW) },
            { 0xCA, new TagInfo(0x04, "Unknown 0xCA", new byte[] { 0x00 }, ValueRole.UINT32) },
            { 0x4D, new TagInfo(0x04, "Unknown 0x4D", new byte[] { 0x00 }, ValueRole.UINT32) },
            { 0x22, new TagInfo(0x04, "Unknown 0x4D", new byte[] { 0x00 }, ValueRole.UINT32) },
            { 0x27, new TagInfo(0x04, "Unknown 0x27", Array.Empty<byte>(), ValueRole.UINT32) },

        });

        public Border Parse(long componentOffset, uint componentId, byte[] data)
        {
            var (component, offset, parseData) = ParseBase(componentOffset, componentId, data);

            var parseOptions = WithComponentTagLogging(Options.Default);
            var parseResult = ParseExtendedTags(component, componentTagMap, parseData, offset, parseOptions);
            
            ApplyExtendedTags(component, parseResult);
            return component;
        }

        private void ApplyExtendedTags(Border component, ParseResult parseResult)
        {

            ApplyExtendedTagsByReflection(component, parseResult, componentTagMap);
        }
    }
}
