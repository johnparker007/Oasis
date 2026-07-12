using MfmeFmlDecoder.Model;
using MfmeFmlDecoder.src.Decoder.Component.Core;
using MfmeFmlDecoder.src.Model.Component;
using System;
using System.Collections.Generic;

namespace MfmeFmlDecoder.src.Decoder.Component
{
    internal sealed class BackgroundParser : ComponentParserBase<Background>
    {
        private const string TransparencyTypeDropdownName = "Transparency Type";
        private const uint NoOverlayInFullscreenModeTagId = 0x4B;
        private const uint TiledTagId = 0x0D;
        private const uint RandomTileTagId = 0x0E;

        private ComponentTagMap componentTagMap = new ComponentTagMap
        {
            { 0x09, new TagInfo(0x01, "Unknown 0x09", new byte[] { 0x00 }, ValueRole.BOOLEAN) },
            { 0x0A, new TagInfo(0x01, "Unknown 0x0A", new byte[] { 0x00 }, ValueRole.BOOLEAN) },
            { 0x0B, new TagInfo(0x01, "Unknown 0x0B", new byte[] { 0x00 }, ValueRole.BOOLEAN) },
            { 0x3B, new TagInfo(0x04, "Unknown3B", new byte[] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x36, new TagInfo(0x00, "Overlay image", Array.Empty<byte>(), ValueRole.BITMAP) },
            { 0x4B, new TagInfo(0x01, "NoOverlayInFullscreenMode", new byte[] { 0x00 }, ValueRole.BOOLEAN) },
        }.WithNestedTagBlock(new ComponentTagMap
        {
            { 0x01, new TagInfo(0x04, "Colour", new byte[] { 0xF0, 0xF0, 0xF0, 0xFF }, ValueRole.ARGB_COLOR) },
            { 0x07, new TagInfo(0x04, "BorderColour", new byte[] { 0x00, 0x00, 0x00, 0xFF }, ValueRole.ARGB_COLOR) },
            { 0x09, new TagInfo(0x04, "TransparentColour", new byte[] { 0x00 }, ValueRole.ARGB_COLOR) },
            { 0x0C, new TagInfo(0x00, "Tile Image", Array.Empty<byte>(), ValueRole.BITMAP) },
            { 0x03, new TagInfo(0x00, "Background Image", Array.Empty<byte>(), ValueRole.BITMAP) },
            { 0x0D, new TagInfo(0x01, "Tiled", new byte[] { 0x00 }, ValueRole.BOOLEAN) },
            { 0x0E, new TagInfo(0x01, "RandomTile", new byte[] { 0x00 }, ValueRole.BOOLEAN) },
            { 0x0F, new TagInfo(0x01, "Transparency_UseAlphaChannel", new byte[] { 0x00 }, ValueRole.BOOLEAN) },
            { 0x08, new TagInfo(0x01, "Transparency_UseColour", new byte[] { 0x00 }, ValueRole.BOOLEAN) },
            { 0x0A, new TagInfo(0x04, "Unknown 0x0A", new byte[] { 0x00 }, ValueRole.ARGB_COLOR) },
            { 0x0B, new TagInfo(0x04, "Unknown 0x0B", new byte[] { 0x00 }, ValueRole.ARGB_COLOR) },
        });

        public Background Parse(long componentOffset, uint componentId, byte[] data)
        {
            var (component, offset, parseData) = ParseBase(
                    componentOffset,
                    componentId,
                    data, 
                    normalizationRule: new GeometryAngleNormalization.Rule(
                    RewriteTriggerOffsetDelta: 0,
                    ValidAngleOffsetDelta: 0));

            var parseOptions = ExtendedTagParser.Options
                .WithBitmapTags(0x03, 0x36, 0x0C)
                .WithFlagDropdown(
                TransparencyTypeDropdownName,
                defaultOption: "None",
                optionByTag: new Dictionary<uint, string>
                {
                    [0x08] = "Use Colour",
                    [0x0F] = "Use Alpha Channel",
                });
            var parseResult = ParseExtendedTags(component, componentTagMap, parseData, offset, parseOptions);

            ApplyExtendedTags(component, parseResult, componentTagMap);

            return component;
        }

        private void ApplyExtendedTags(Background component, ExtendedTagParser.ParseResult parseResult, ComponentTagMap componentTagMap)
        {

            ApplyExtendedTagsByReflection(component, parseResult, componentTagMap);

            component.TransparencyType = parseResult.FlagDropdownSelectionsByName[TransparencyTypeDropdownName];
        }
    }
}
