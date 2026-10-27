using MfmeFmlDecoder.Model;
using MfmeFmlDecoder.src.Decoder.Component.Core;
using MfmeFmlDecoder.src.Model.Component;
using System;
using static MfmeFmlDecoder.src.Decoder.Component.Core.ExtendedTagParser;

namespace MfmeFmlDecoder.src.Decoder.Component
{
    internal sealed class AceVideoParser : ComponentParserBase<AceVideo>
    {
        private ComponentTagMap componentTagMap = new ComponentTagMap
        {
            { 0x01, new TagInfo(0x01, "Unknown 0x01", new byte[] { }, ValueRole.BOOLEAN) },
            { 0x34, new TagInfo(0x01, "Unknown 0x34", new byte[] { }, ValueRole.UINT16) },
            { 0x36, new TagInfo(0x00, "Overlay Image", Array.Empty<byte>(), ValueRole.BITMAP) },
            { 0x3B, new TagInfo(0x04, "Unknown 0x3B", new byte[] { 0x00 }, ValueRole.UINT32) },
            { 0xE9, new TagInfo(0x01, "Unknown 0xE9", new byte[] { }, ValueRole.BOOLEAN) },
        };

        public AceVideo Parse(long componentOffset, uint componentId, byte[] data)
        {
            var (component, offset, parseData) = ParseBase(componentOffset, componentId, data);

            var parseOptions = WithComponentTagLogging(Options.Default);
            var parseResult = ParseExtendedTags(component, componentTagMap, parseData, offset, parseOptions);
            
            ApplyExtendedTags(component, parseResult);
            return component;
        }

        private void ApplyExtendedTags(AceVideo component, ParseResult parseResult)
        {

            ApplyExtendedTagsByReflection(component, parseResult, componentTagMap);
        }
    }
}
