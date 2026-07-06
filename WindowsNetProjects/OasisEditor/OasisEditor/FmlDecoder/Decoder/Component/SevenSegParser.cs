using MfmeFmlDecoder.Model;
using MfmeFmlDecoder.src.Decoder.Component.Core;
using MfmeFmlDecoder.src.Model.Component;
using System;
using System.Collections.Generic;
using static MfmeFmlDecoder.src.Decoder.Component.Core.ExtendedTagParser;

namespace MfmeFmlDecoder.src.Decoder.Component
{
    internal sealed class SevenSegParser : ComponentParserBase<SevenSeg>
    {
        // Per-segment lamp numbers (0x39), emitted as a flat array on the component.
        private const uint SubLampTableTag = 0x39;

        // Display style selector (0x20): maps the raw style id to a friendly style name,
        // matching how SevenSegBlock resolves its SelectedTypeId.
        private const uint SelectedStyleTag = 0x20;
        private const string SelectedStyleIdAttribute = "SelectedStyleId";
        private static readonly IReadOnlyDictionary<byte, string> SelectedStyleOptions = new Dictionary<byte, string>
        {
            { 0x00, "Style 1" },
            { 0x01, "Style 2" },
            { 0x02, "Style 3" },
        };

        // Outer-section tags (before the 4C ?? 00 nested block); the rest live inside the nested block.
        private ComponentTagMap componentTagMap = new ComponentTagMap
        {
            { 0x1C, new TagInfo(0x04, "NumberOfDefinedLampNumbers", Array.Empty<byte>(), ValueRole.UINT32) },
            { 0x36, new TagInfo(0x00, "Overlay Image", Array.Empty<byte>(), ValueRole.BITMAP) },
            { 0x38, new TagInfo(0x04, "SublampCount", new byte[] { 0x00 }, ValueRole.SUBLAMP_COUNT) },
            { 0x39, new TagInfo(0x04, "SublampTable", new byte[] { 0x00 }, ValueRole.LAMP_SUBLAMP_TABLE) },
            { 0x3B, new TagInfo(0x04, "Unknown 0x3B", Array.Empty<byte>(), ValueRole.UINT32) }
        }.WithNestedTagBlock(new ComponentTagMap
        {
            { 0x26, new TagInfo(0x01, "Alpha", new byte[] { 0x00 }, ValueRole.BOOLEAN) },
            { 0x27, new TagInfo(0x01, "DPOff", new byte[] { 0x00 }, ValueRole.BOOLEAN) },
            { 0x09, new TagInfo(0x01, "DPOn", new byte[] { 0x00 }, ValueRole.BOOLEAN) },
            { 0x21, new TagInfo(0x01, "AutoDP", new byte[] { 0x00 }, ValueRole.BOOLEAN) },
            { 0x0A, new TagInfo(0x01, "Segment16", new byte[] { 0x00 }, ValueRole.BOOLEAN) },
            { 0x1D, new TagInfo(0x01, "ZeroOn", new byte[] { 0x00 }, ValueRole.BOOLEAN) },
            { 0x08, new TagInfo(0x01, "DPRight", new byte[] { 0x00 }, ValueRole.BOOLEAN) },
            { 0x06, new TagInfo(0x04, "SegmentOnColour", new byte[] { 0x00, 0x00, 0xFF, 0xFF }, ValueRole.ARGB_COLOR) },
            { 0x07, new TagInfo(0x04, "SegmentOffColour", new byte[] { 0x30, 0x30, 0x30, 0xFF }, ValueRole.ARGB_COLOR) },
            { 0x05, new TagInfo(0x04, "BackgroundColour", new byte[] { 0x00, 0x00, 0x00, 0xFF }, ValueRole.ARGB_COLOR) },
            { 0x01, new TagInfo(0x04, "Thickness", new byte[] { 0x03, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x02, new TagInfo(0x04, "Spacing", new byte[] { 0x01, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x03, new TagInfo(0x04, "Horizontal Spacing", new byte[] { 0x0A, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x04, new TagInfo(0x04, "Vertical Spacing", new byte[] { 0x06, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x25, new TagInfo(0x04, "Offset", new byte[] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x22, new TagInfo(0x04, "DigitAngle", new byte[] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x1C, new TagInfo(0x04, "Slant", new byte[] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x1E, new TagInfo(0x04, "Chop", new byte[] { 0x4B, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x1F, new TagInfo(0x04, "Centre", new byte[] { 0x32, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x0B, new TagInfo(0x01, "Programmable", new byte[] { 0x00 }, ValueRole.BOOLEAN) },
            { 0x20, new TagInfo(0x01, "SelectedStyleId", new byte[] { 0x00 }, ValueRole.BYTE) },
            { 0x24, new TagInfo(0x01, "Unknown 0x24", new byte[] { 0x00 }, ValueRole.BYTE) },
        });


        public SevenSeg Parse(long componentOffset, uint componentId, byte[] data)
        {
            var (component, offset, parseData) = ParseBase(
                componentOffset,
                componentId,
                data,
                normalizationRule: new GeometryAngleNormalization.Rule(
                    RewriteTriggerOffsetDelta: 0,
                    ValidAngleOffsetDelta: 0));

            var parseOptions = WithComponentTagLogging(
                ExtendedTagParser.Options
                .WithBitmapTags(0x36)
            );

            var parseResult = ParseExtendedTags(component, componentTagMap, parseData, offset, parseOptions);

            ApplyExtendedTags(component, parseResult, componentTagMap);

            return component;
        }

        private void ApplyExtendedTags(SevenSeg component, ExtendedTagParser.ParseResult parseResult, ComponentTagMap componentTagMap)
        {

            ApplyExtendedTagsByReflection(component, parseResult, componentTagMap);
            ApplySelectedStyle(component, parseResult);
            ApplySubLampTable(component, parseResult);
        }

        // The SubLampTable (tag 0x39) lives in the outer section; expose its lamp numbers as a flat array.
        private static void ApplySubLampTable(SevenSeg component, ExtendedTagParser.ParseResult parseResult)
        {
            if (parseResult.ValuesByTag.TryGetValue(SubLampTableTag, out object value)
                && value is IReadOnlyList<LampSublampTableEntry> subLampTable
                && subLampTable.Count > 0)
            {
                component.SublampTable = subLampTable;
            }
        }

        // Resolve the raw SelectedStyleId byte to a friendly style name, dropping the raw id.
        private static void ApplySelectedStyle(SevenSeg component, ExtendedTagParser.ParseResult parseResult)
        {
            var nestedResult = parseResult.NestedTagBlockResult;
            IReadOnlyDictionary<uint, object> valuesByTag =
                nestedResult is not null ? nestedResult.ValuesByTag : parseResult.ValuesByTag;

            if (valuesByTag.TryGetValue(SelectedStyleTag, out object value)
                && value is byte styleId
                && SelectedStyleOptions.TryGetValue(styleId, out string styleName))
            {
                component.Strings["SelectedType"] = styleName;
            }

            component.Bytes.Remove(SelectedStyleIdAttribute);
        }
    }
}
