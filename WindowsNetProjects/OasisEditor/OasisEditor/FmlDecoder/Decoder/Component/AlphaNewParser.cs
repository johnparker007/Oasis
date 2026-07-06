using MfmeFmlDecoder.Model;
using MfmeFmlDecoder.src.Decoder.Component.Core;
using MfmeFmlDecoder.src.Model.Component;
using System;

namespace MfmeFmlDecoder.src.Decoder.Component
{
    internal sealed class AlphaNewParser : ComponentParserBase<AlphaNew>
    {
        private ComponentTagMap componentTagMap = new ComponentTagMap
        {
            { 0x01, new TagInfo(0x04, "OnColour", Array.Empty<byte>(), ValueRole.ARGB_COLOR) },
            { 0x02, new TagInfo(0x04, "OffColour", Array.Empty<byte>(), ValueRole.ARGB_COLOR) },
            { 0x03, new TagInfo(0x04, "BackgroundColour", Array.Empty<byte>(), ValueRole.ARGB_COLOR) },
            { 0x04, new TagInfo(0x04, "UNKNOWN 0x04", Array.Empty<byte>(), ValueRole.ARGB_COLOR) },
            { 0x06, new TagInfo(0x04, "Thickness", new byte[] { 0x00, 0x00, 0x00, 0x40 }, ValueRole.FLOAT) },
            { 0x07, new TagInfo(0x04, "Spacing", new byte[] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.FLOAT) },
            { 0x08, new TagInfo(0x04, "HorizontalSpacing", new byte[] { 0x00, 0x00, 0xA0, 0x40 }, ValueRole.FLOAT) },
            { 0x09, new TagInfo(0x04, "VerticalSpacing", new byte[] { 0x00, 0x00, 0x80, 0x40 }, ValueRole.FLOAT) },
            { 0x0A, new TagInfo(0x04, "Slant", new byte[] { 0x00, 0x00, 0xA0, 0x40 }, ValueRole.FLOAT) },
            { 0x0B, new TagInfo(0x04, "Centre", new byte[] { 0x00, 0x00, 0x40, 0x42 }, ValueRole.FLOAT) },
            { 0x99, new TagInfo(0x04, "Chop", new byte[] { 0x00, 0x00, 0xB4, 0x42 }, ValueRole.FLOAT) },
            { 0x0D, new TagInfo(0x04, "UNKNOWN 0x0D", Array.Empty<byte>(), ValueRole.ARGB_COLOR) },
            { 0x0E, new TagInfo(0x04, "UNKNOWN 0x0E", Array.Empty<byte>(), ValueRole.ARGB_COLOR) },
            { 0x0C, new TagInfo(0x01, "Charset", new byte[] { 0x00 }, ValueRole.BYTE) },
            { 0x0F, new TagInfo(0x01, "Segment16", new byte[] { 0x01 }, ValueRole.BOOLEAN) },
            { 0x40, new TagInfo(0x01, "Reversed", new byte[] { 0x00 }, ValueRole.BOOLEAN) },
            { 0x36, new TagInfo(0x00, "Overlay Image", Array.Empty<byte>(), ValueRole.BITMAP) },
            { 0x3B, new TagInfo(0x04, "Unknown3B", new byte[] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
        };

        public AlphaNew Parse(long componentOffset, uint componentId, byte[] data)
        {
            var (component, offset, parseData) = ParseBase(
                componentOffset,
                componentId,
                data,
                normalizationRule: new GeometryAngleNormalization.Rule(
                    RewriteTriggerOffsetDelta: 5,
                    ValidAngleOffsetDelta: 0));

            var parseOptions = WithComponentTagLogging(ExtendedTagParser.Options.Default);
            var parseResult = ParseExtendedTags(component, componentTagMap, parseData, offset, parseOptions);

            ApplyExtendedTags(component, parseResult, componentTagMap);
            return component;
        }

        private void ApplyExtendedTags(AlphaNew component, ExtendedTagParser.ParseResult parseResult, ComponentTagMap componentTagMap)
        {

            ApplyExtendedTagsByReflection(component, parseResult, componentTagMap);

            if (parseResult.ValuesByTag.TryGetValue(0x0C, out var charset)) component.Charset = MapCharset((byte)charset);
        }

        private static AlphaNewCharset MapCharset(byte value)
        {
            return value switch
            {
                0 => AlphaNewCharset.OKI_1937,
                1 => AlphaNewCharset.OLD_Charset,
                2 => AlphaNewCharset.BFM_Charset,
                _ => throw new InvalidOperationException(
                    $"Unknown AlphaNew charset tag value 0x{value:X2} (expected 0, 1, or 2)."),
            };
        }
    }
}
