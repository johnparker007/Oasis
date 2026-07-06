using MfmeFmlDecoder.Model;
using MfmeFmlDecoder.src.Decoder.Component.Core;
using MfmeFmlDecoder.src.Model.Component;
using System;

namespace MfmeFmlDecoder.src.Decoder.Component
{
    internal sealed class AceMatrixParser : ComponentParserBase<AceMatrix>
    {
        private ComponentTagMap componentTagMap = new ComponentTagMap
        {
            { 0x01, new TagInfo(0x04, "Size", new byte[] { 0x07 }, ValueRole.UINT32) },
            { 0x02, new TagInfo(0x01, "Flip180", new byte[] { 0x00 }, ValueRole.BOOLEAN) },
            { 0x03, new TagInfo(0x04, "OnColour", new byte[] { 0x00, 0x00, 0xFF, 0xFF }, ValueRole.ARGB_COLOR) },
            { 0x04, new TagInfo(0x04, "OffColour", new byte[] { 0x00, 0x00, 0x00, 0xFF }, ValueRole.ARGB_COLOR) },
            { 0x05, new TagInfo(0x01, "Vertical", new byte[] { 0x00 }, ValueRole.BOOLEAN) },
            { 0x06, new TagInfo(0x04, "BackgroundColour", new byte[] { 0x00, 0x00, 0x00, 0xFF }, ValueRole.ARGB_COLOR) },
            { 0x36, new TagInfo(0x00, "Overlay Image", Array.Empty<byte>(), ValueRole.BITMAP) },
            { 0x3B, new TagInfo(0x04, "Unknown 0x3B", new byte[] { 0x00 }, ValueRole.UINT32) },
        };

        public AceMatrix Parse(long componentOffset, uint componentId, byte[] data)
        {
            var (component, offset, parseData) = ParseBase(
                componentOffset,
                componentId,
                data,
                normalizationRule: new GeometryAngleNormalization.Rule(
                    RewriteTriggerOffsetDelta: 0,
                    ValidAngleOffsetDelta: 0));

            var parseResult = ParseExtendedTags(component, componentTagMap, parseData, offset, WithComponentTagLogging(ExtendedTagParser.Options.Default));

            ApplyExtendedTags(component, parseResult, componentTagMap);
            return component;
        }

        private void ApplyExtendedTags(AceMatrix component, ExtendedTagParser.ParseResult parseResult, ComponentTagMap componentTagMap)
        {

            ApplyExtendedTagsByReflection(component, parseResult, componentTagMap);
        }
    }
}
