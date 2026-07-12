using MfmeFmlDecoder.Model;
using MfmeFmlDecoder.src.Decoder.Component.Core;
using MfmeFmlDecoder.src.Model.Component;
using System;
using static MfmeFmlDecoder.src.Decoder.Component.Core.ExtendedTagParser;

namespace MfmeFmlDecoder.src.Decoder.Component
{
    internal sealed class BorderParser : ComponentParserBase<Border>
    {
        private const uint BoundingBoxTagId = 0x06;
        private const int BoundingBoxLength = 0x10;
        private const uint PaneSplitTagId = 0x07;
        private const int PaneSplitLength = 0x10;

        private readonly ComponentTagMap componentTagMap = new ComponentTagMap
        {
            { 0x0A, new TagInfo(0x01, "Unknown 0x0A", new byte[] { 0x00 }, ValueRole.BOOLEAN) },
            { 0x0B, new TagInfo(0x01, "Unknown 0x0B", new byte[] { 0x00 }, ValueRole.BOOLEAN) },
            { 0x37, new TagInfo(0x04, "Unknown 0x37", new byte[] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x36, new TagInfo(0x00, "Overlay Image", Array.Empty<byte>(), ValueRole.BITMAP) },
            { 0x3B, new TagInfo(0x04, "Unknown 0x3B", new byte[] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
        }.WithNestedTagBlock(new ComponentTagMap
        {
            { 0x01, new TagInfo(0x04, "OuterColour", new byte[] { 0x00, 0x00, 0xFF, 0xFF }, ValueRole.ARGB_COLOR) },
            { 0x02, new TagInfo(0x04, "BorderWidth", new byte[] { 0x08, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x03, new TagInfo(0x00, "Border Image", Array.Empty<byte>(), ValueRole.BITMAP) },
            { 0x04, new TagInfo(0x04, "Unknown 0x04", new byte[] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x05, new TagInfo(0x04, "InnerColour", new byte[] { 0x00, 0xFF, 0x00, 0xFF }, ValueRole.ARGB_COLOR) },
            // Four LE uint32s: BoxX, BoxY (always 0), BoxWidth, BoxHeight.
            { BoundingBoxTagId, new TagInfo(BoundingBoxLength, "BoundingBox", Array.Empty<byte>(), ValueRole.RAW) },
            // Four LE uint32s: LeftWidth, TopHeight, RightWidth, BottomHeight (2×2 pane split).
            { PaneSplitTagId, new TagInfo(PaneSplitLength, "PaneSplit", Array.Empty<byte>(), ValueRole.RAW) },
            { 0x08, new TagInfo(0x04, "Spacing", new byte[] { 0x09, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
        });

        private static readonly Options ExtendedTagOptions =
            Options.WithBitmapTags(0x03, 0x36);

        public Border Parse(long componentOffset, uint componentId, byte[] data)
        {
            var (component, offset, parseData) = ParseBase(
                componentOffset,
                componentId,
                data,
                normalizationRule: new GeometryAngleNormalization.Rule(
                    RewriteTriggerOffsetDelta: 0,
                    ValidAngleOffsetDelta: 0));

            var parseOptions = WithComponentTagLogging(ExtendedTagOptions);
            var parseResult = ParseExtendedTags(component, componentTagMap, parseData, offset, parseOptions);

            ApplyExtendedTags(component, parseResult);
            return component;
        }

        private void ApplyExtendedTags(Border component, ParseResult parseResult)
        {
            ApplyExtendedTagsByReflection(component, parseResult, componentTagMap);

            ParseResult nested = parseResult.NestedTagBlockResult ?? parseResult;
            ApplyBoundingBox(component, nested);
            ApplyPaneSplit(component, nested);
        }

        private static void ApplyBoundingBox(Border component, ParseResult parseResult)
        {
            if (!TryGetFixedRaw(parseResult, 0x06, 0x10, out byte[] raw))
            {
                return;
            }

            uint boxX = BitConverter.ToUInt32(raw, 0);
            uint boxY = BitConverter.ToUInt32(raw, 4);
            uint boxWidth = BitConverter.ToUInt32(raw, 8);
            uint boxHeight = BitConverter.ToUInt32(raw, 12);

            // Wire values stay local (BoxX/BoxY observed as always 0).
            component.BoxX = boxX;
            component.BoxY = boxY;
            component.BoxWidth = boxWidth;
            component.BoxHeight = boxHeight;

            // Presentation: absolute origin = geometry + local box offset.
            component.UInt32s["BoxX"] = checked(component.X + boxX);
            component.UInt32s["BoxY"] = checked(component.Y + boxY);
            component.UInt32s["BoxWidth"] = boxWidth;
            component.UInt32s["BoxHeight"] = boxHeight;
        }

        private static void ApplyPaneSplit(Border component, ParseResult parseResult)
        {
            if (!TryGetFixedRaw(parseResult, 0x07, 0x10, out byte[] raw))
            {
                return;
            }

            uint leftWidth = BitConverter.ToUInt32(raw, 0);
            uint topHeight = BitConverter.ToUInt32(raw, 4);
            uint rightWidth = BitConverter.ToUInt32(raw, 8);
            uint bottomHeight = BitConverter.ToUInt32(raw, 12);

            component.LeftWidth = leftWidth;
            component.TopHeight = topHeight;
            component.RightWidth = rightWidth;
            component.BottomHeight = bottomHeight;

            component.UInt32s["LeftWidth"] = leftWidth;
            component.UInt32s["TopHeight"] = topHeight;
            component.UInt32s["RightWidth"] = rightWidth;
            component.UInt32s["BottomHeight"] = bottomHeight;
        }

        private static bool TryGetFixedRaw(
            ParseResult parseResult,
            uint tagId,
            int expectedLength,
            out byte[] raw)
        {
            raw = null;
            if (parseResult?.ValuesByTag is null)
            {
                return false;
            }

            if (!parseResult.ValuesByTag.TryGetValue(tagId, out object value) || value is not byte[] bytes)
            {
                return false;
            }

            if (bytes.Length != expectedLength)
            {
                throw new InvalidOperationException(
                    $"Border tag 0x{tagId:X2} expected {expectedLength} bytes, got {bytes.Length}.");
            }

            raw = bytes;
            return true;
        }
    }
}
