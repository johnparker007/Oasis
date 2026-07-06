using MfmeFmlDecoder.Model;
using MfmeFmlDecoder.src.Decoder.Component.Core;
using MfmeFmlDecoder.src.Model.Component;
using System;
using System.Collections.Generic;
using System.Xml.Linq;

namespace MfmeFmlDecoder.src.Decoder.Component
{
    internal sealed class DiscReelParser : ComponentParserBase<DiscReel>
    {
        private ComponentTagMap componentTagMap = new ComponentTagMap
        {
            { 0x01, new TagInfo(0x04, "HalfSteps", new byte[] { 0x60, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x02, new TagInfo(0x04, "Stops", new byte[] { 0x0C, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x03, new TagInfo(0x04, "Resolution", new byte[] { 0x01, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x05, new TagInfo(0x04, "Offset", new byte[] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x08, new TagInfo(0x04, "OuterLampSize", new byte[] { 0x35, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x0A, new TagInfo(0x04, "OuterH", new byte[] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x0B, new TagInfo(0x04, "OuterL", new byte[] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x0C, new TagInfo(0x04, "InnerH", new byte[] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x0D, new TagInfo(0x04, "InnerL", new byte[] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x11, new TagInfo(0x04, "InnerLampSize", new byte[] { 0x35, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x96, new TagInfo(0x04, "OptoTab", new byte[] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x97, new TagInfo(0x01, "LampsEnabled", new byte[] { 0x01 }, ValueRole.BOOLEAN) },
            { 0x98, new TagInfo(0x04, "NumberOfLamps", new byte[] { 0x0C, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x99, new TagInfo(0x04, "Bounce", new byte[] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x38, new TagInfo(0x04, "SublampCount", new byte[] { }, ValueRole.SUBLAMP_COUNT) },
            { 0x39, new TagInfo(0x04, "SublampTable", new byte[] { }, ValueRole.LAMP_SUBLAMP_TABLE) },
            { 0x07, new TagInfo(0x04, "LampPositionsOffset", new byte[] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x9A, new TagInfo(0x01, "LampPositionsGapEnabled", new byte[] { 0x00 }, ValueRole.BOOLEAN) },
            { 0x9B, new TagInfo(0x04, "LampPositionsGap", new byte[] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x40, new TagInfo(0x01, "Reversed", new byte[] { 0x00 }, ValueRole.BOOLEAN) },
            { 0x3C, new TagInfo(0x01, "Inverted", new byte[] { 0x00 }, ValueRole.BOOLEAN) },
            { 0x0E, new TagInfo(0x00, "Disc Reel Image", new byte[] { }, ValueRole.BITMAP) },
            { 0x0F, new TagInfo(0x00, "Disc Overlay Image", new byte[] { }, ValueRole.BITMAP) },
            { 0x10, new TagInfo(0x00, "Outer Mask 1 Image", new byte[] { }, ValueRole.BITMAP) },
            { 0x54, new TagInfo(0x00, "Outer Mask 2 Image", new byte[] { }, ValueRole.BITMAP) },
            { 0x33, new TagInfo(0x00, "Inner Mask 1 Image", new byte[] { }, ValueRole.BITMAP) },
            { 0x55, new TagInfo(0x00, "Inner Mask 2 Image", new byte[] { }, ValueRole.BITMAP) },
            { 0x36, new TagInfo(0x00, "Overlay Image", Array.Empty<byte>(), ValueRole.BITMAP) },
            { 0x1C, new TagInfo(0x04, "Unknown1", new byte[] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x3B, new TagInfo(0x04, "Unknown 0x3B", new byte[] { }, ValueRole.UINT32) },
            { 0x3E, new TagInfo(0x01, "Unknown 0x3E", new byte[] { 0x00 }, ValueRole.BOOLEAN) },
        };

        public DiscReel Parse(long componentOffset, uint componentId, byte[] data)
        {
            // Normalize for MFME angle bug
            var (component, offset, parseData) = ParseBase(
                componentOffset,
                componentId,
                data,
                normalizationRule: new GeometryAngleNormalization.Rule(
                    RewriteTriggerOffsetDelta: 2,
                    ValidAngleOffsetDelta: 0));

            DumpRemaining(componentOffset, parseData, offset);

            var parseOptions = WithComponentTagLogging(
                new ExtendedTagParser.Options(
                        new uint[] { 0x0E, 0x0F, 0x10, 0x54, 0x33, 0x55, 0x36 })).WithTagLogging();

            var parseResult = ParseExtendedTags(component, componentTagMap, parseData, offset, parseOptions);

            ApplyExtendedTags(component, parseResult);

            return component;
        }

        private void ApplyExtendedTags(DiscReel component, ExtendedTagParser.ParseResult parseResult)
        {
            ApplyExtendedTagsByReflection(component, parseResult, componentTagMap);

            if (parseResult.ValuesByTag.TryGetValue(0x39, out var sublampTableValue)
                && sublampTableValue is IReadOnlyList<LampSublampTableEntry> sublampTable)
            {
                component.SublampTable = sublampTable;
            }
        }
    }
}
