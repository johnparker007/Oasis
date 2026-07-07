using MfmeFmlDecoder.Model;
using MfmeFmlDecoder.src.Decoder.Component.Core;
using MfmeFmlDecoder.src.Model.Component;
using MfmeFmlDecoder.Utilities;
using System;
using System.Collections.Generic;

namespace MfmeFmlDecoder.src.Decoder.Component
{
    internal sealed class AlphaParser : ComponentParserBase<Alpha>
    {
        /// <summary>
        /// Synthetic tag for the merged Alpha character bitmap exposed under Images["alpha_character_bitmap"].
        /// </summary>
        private const uint AlphaCharacterBitmapSyntheticTag = 0xACBC01u;

        private ComponentTagMap componentTagMap = new ComponentTagMap
        {
            { 0x01, new TagInfo(0x04, "Colour", Array.Empty<byte>(), ValueRole.ARGB_COLOR) },
            { 0x02, new TagInfo(0x04, "DigitWidth", new byte[] { 0x11, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x03, new TagInfo(0x04, "Columns", new byte[] { 0x10, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x04, new TagInfo(0x00, "Alpha Character Bitmap", Array.Empty<byte>(), ValueRole.BITMAP) },
            { 0x36, new TagInfo(0x00, "Overlay Image", Array.Empty<byte>(), ValueRole.BITMAP) },
            { 0x3B, new TagInfo(0x04, "Unknown3B", new byte[] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x40, new TagInfo(0x01, "Reversed", new byte[] { 0x00 }, ValueRole.BOOLEAN) },
        };

        private static readonly ExtendedTagParser.Options extendedTagOptions =
            ExtendedTagParser.Options.WithBitmapTags(0x04, 0x36);

        public Alpha Parse(long componentOffset, uint componentId, byte[] data)
        {
            var (component, offset, parseData) = ParseBase(componentOffset, componentId, data);

            var parseOptions = WithComponentTagLogging(extendedTagOptions);
            var parseResult = ParseExtendedTags(component, componentTagMap, parseData, offset, parseOptions);

            ApplyExtendedTags(component, parseResult, componentTagMap);

            return component;
        }

        private void ApplyExtendedTags(Alpha component, ExtendedTagParser.ParseResult parseResult, ComponentTagMap componentTagMap)
        {

            ApplyExtendedTagsByReflection(component, parseResult, componentTagMap);
        }
    }
}
