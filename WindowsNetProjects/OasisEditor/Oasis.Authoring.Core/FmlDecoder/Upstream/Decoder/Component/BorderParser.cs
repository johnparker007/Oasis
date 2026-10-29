using MfmeFmlDecoder.Decoder;
using MfmeFmlDecoder.Model;
using MfmeFmlDecoder.src.Decoder.Component.Core;
using MfmeFmlDecoder.src.Decoder.Component.Profiles;
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



        public Border Parse(ParsingContext context, long componentOffset, uint componentId, byte[] data)
        {
            ComponentProfile profile = ComponentProfileCatalog.Resolve(MFMEComponentType.Border, context.Version);
            var (component, offset, parseData) = ParseBase(context, profile, componentOffset, componentId, data);

                        var parseResult = ParseExtendedTags(component, profile, parseData, offset);

            ApplyExtendedTags(component, parseResult, profile.Tags);
            return component;
        }

        private void ApplyExtendedTags(Border component, ParseResult parseResult, ComponentTagMap componentTagMap)
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
