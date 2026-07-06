using MfmeFmlDecoder.Model;
using MfmeFmlDecoder.src.Decoder.Component.Core;
using MfmeFmlDecoder.src.Model.Component;
using System;

namespace MfmeFmlDecoder.src.Decoder.Component
{
    internal sealed class ReelBonusReelParser : ComponentParserBase<ReelBonusReel>
    {
        private ComponentTagMap componentTagMap = new ComponentTagMap
        {
            { 0x1C, new TagInfo(0x04, "NumberOfDefinedLampNumbers", new byte[] { }, ValueRole.UINT32) },
            { 0x38, new TagInfo(0x04, "SublampCount", new byte[] { }, ValueRole.SUBLAMP_COUNT) },
            { 0x39, new TagInfo(0x04, "SublampTable", new byte[] { }, ValueRole.LAMP_SUBLAMP_TABLE) },
            { 0x02, new TagInfo(0x04, "SymbolPos", new byte[] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x03, new TagInfo(0x00, "Lamp 1 On Image", new byte[] { }, ValueRole.BITMAP) },
            { 0x04, new TagInfo(0x00, "Lamp 2 On Image", new byte[] { }, ValueRole.BITMAP) },
            { 0x05, new TagInfo(0x00, "Lamp 3 On Image", new byte[] { }, ValueRole.BITMAP) },
            { 0x06, new TagInfo(0x00, "Lamp 4 On Image", new byte[] { }, ValueRole.BITMAP) },
            { 0x07, new TagInfo(0x00, "Mask Image", new byte[] { 0x00, 0xFF, 0xFF, 0xFF }, ValueRole.BITMAP) },
            { 0x08, new TagInfo(0x00, "Background Image", new byte[] { }, ValueRole.BITMAP) },
            { 0x36, new TagInfo(0x00, "Overlay Image", new byte[] { }, ValueRole.BITMAP) },
            { 0x3B, new TagInfo(0x04, "Unknown 0x3B", new byte[] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
        };

        private static readonly ExtendedTagParser.Options extendedTagOptions =
            ExtendedTagParser.Options.WithBitmapTags(0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x36);

        public ReelBonusReel Parse(long componentOffset, uint componentId, byte[] data)
        {
            var (component, offset, parseData) = ParseBase(
                componentOffset,
                componentId,
                data,
                normalizationRule: new GeometryAngleNormalization.Rule(
                    RewriteTriggerOffsetDelta: 4,
                    ValidAngleOffsetDelta: 0));

            var parseOptions = WithComponentTagLogging(extendedTagOptions);
            var parseResult = ParseExtendedTags(component, componentTagMap, parseData, offset, parseOptions);

            return component;
        }
    }
}
