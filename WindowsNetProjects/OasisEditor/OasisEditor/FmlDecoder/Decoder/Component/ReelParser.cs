using MfmeFmlDecoder.Model;
using MfmeFmlDecoder.src.Model.Component;
using System.Collections.Generic;
using System;
using MfmeFmlDecoder.src.Decoder.Component.Core;

namespace MfmeFmlDecoder.src.Decoder.Component
{
    internal sealed class ReelParser : ComponentParserBase<Reel>
    {
        private static readonly Dictionary<byte, string> offColourOptions = new Dictionary<byte, string>
        {
            { 0x0, "Mask 1" },
            { 0x1, "Mask 2" },
            { 0x2, "Mask 3" },
            { 0x3, "White" },
        };

        private ComponentTagMap componentTagMap = new ComponentTagMap
        {
            { 0x40, new TagInfo(0x01, "Reversed", new byte[] { 0x00 }, ValueRole.BOOLEAN) },
            { 0x3C, new TagInfo(0x01, "InvertedOpto", new byte[] { 0x00 }, ValueRole.BOOLEAN) },

            { 0x36, new TagInfo(0x00, "Overlay Image", new byte[] { }, ValueRole.BITMAP) },
            { 0x3B, new TagInfo(0x04, "Unknown 0x3B", new byte[] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x1C, new TagInfo(0x04, "NonNullSublampCount", new byte[] { 0x00 }, ValueRole.UINT32) },
            { 0x38, new TagInfo(0x04, "SublampCount", new byte[] { 0x00 }, ValueRole.SUBLAMP_COUNT) },
            { 0x39, new TagInfo(0x04, "SublampTable", new byte[] { 0x00 }, ValueRole.LAMP_SUBLAMP_TABLE) },
        }.WithNestedTagBlock(new ComponentTagMap
        {
            { 0x01, new TagInfo(0x04, "OptoTab", new byte[] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x03, new TagInfo(0x04, "BandOffset", new byte[] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.INT32) },
            { 0x04, new TagInfo(0x04, "Stops", new byte[] { 0x10, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x05, new TagInfo(0x04, "HalfSteps", new byte[] { 0x60, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x08, new TagInfo(0x04, "Resolution", new byte[] { 0x01, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x11, new TagInfo(0x04, "ReelHeight", new byte[] { 0xBE, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x25, new TagInfo(0x04, "WidthDiff", new byte[] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.INT32) },
            { 0x24, new TagInfo(0x04, "WinlinesColour", new byte[] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.ARGB_COLOR) },
            { 0x26, new TagInfo(0x04, "BackgroundFillColour", new byte[] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.ARGB_COLOR) },
            { 0x0D, new TagInfo(0x04, "BorderColour", new byte[] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.ARGB_COLOR) },
            { 0x0B, new TagInfo(0x04, "NumberOfWinlines", new byte[] { 0x01, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x20, new TagInfo(0x04, "WinlinesOffset", new byte[] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x0C, new TagInfo(0x04, "WinlinesThickness", new byte[] { 0x01, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x0A, new TagInfo(0x04, "BorderThickness", new byte[] { 0x01, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x1E, new TagInfo(0x04, "ReelNbrOffset", new byte[] { 0x01, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x17, new TagInfo(0x04, "Filter", new byte[] { 0x06, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x15, new TagInfo(0x04, "Bounce", new byte[] { 0x01, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x2A, new TagInfo(0x01, "SelectedOffColourId", new byte[] { 0x00 }, ValueRole.BYTE) },
            { 0x29, new TagInfo(0x01, "OffLevel", new byte[] { 0x40, 0x00, 0x00, 0x00 }, ValueRole.BYTE) },
            { 0x09, new TagInfo(0x01, "ToggleView", new byte[] { 0x00 }, ValueRole.BOOLEAN) },
            { 0x16, new TagInfo(0x01, "OpaqueBand", new byte[] { 0x00 }, ValueRole.BOOLEAN) },

            { 0x06, new TagInfo(0x01, "LampsEnabled", new byte[] { 0x00 }, ValueRole.BOOLEAN) },
            { 0x1A, new TagInfo(0x01, "LedsEnabled", new byte[] { 0x00 }, ValueRole.BOOLEAN) },
            { 0x07, new TagInfo(0x01, "CustomEnabled", new byte[] { 0x00 }, ValueRole.BOOLEAN) },
            { 0x21, new TagInfo(0x01, "Mirrored", new byte[] { 0x00 }, ValueRole.BOOLEAN) },
            { 0x27, new TagInfo(0x04, "MirroredLevel", new byte[] { 0x40, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },

            { 0x0E, new TagInfo(0x00, "BandImage", new byte[] { }, ValueRole.BITMAP) },
            { 0x0F, new TagInfo(0x00, "LampMasks_1to5", new byte[] { }, ValueRole.BITMAP) },
            { 0x19, new TagInfo(0x00, "LampMasks_6to10", new byte[] { }, ValueRole.BITMAP) }
            , { 0x28, new TagInfo(0x00, "LampMasks_11to15", new byte[] { }, ValueRole.BITMAP) },
            { 0x1C, new TagInfo(0x00, "GradientImage", new byte[] { }, ValueRole.BITMAP) },

            // ReelMode (0x1F) is decoded manually to DblSymbols / IGTReel in ApplyExtendedTags.
            { 0x1F, new TagInfo(0x04, "ReelMode", new byte[] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
        });

        private static readonly ExtendedTagParser.Options extendedTagOptions =
            ExtendedTagParser.Options.WithBitmapTags(0x0E, 0x0F, 0x05, 0x19, 0x28, 0x1C, 0x36);

        public Reel Parse(long componentOffset, uint componentId, byte[] data)
        {
            var (component, offset, parseData) = ParseBase(
                componentOffset,
                componentId,
                data,
                normalizationRule: new GeometryAngleNormalization.Rule(
                    RewriteTriggerOffsetDelta: 0,
                    ValidAngleOffsetDelta: 0));

            var parseOptions = WithComponentTagLogging(extendedTagOptions);
            var parseResult = ParseExtendedTags(component, componentTagMap, parseData, offset, parseOptions);

            ApplyExtendedTags(component, parseResult);

            return component;
        }

        private static void ApplyExtendedTags(Reel component, ExtendedTagParser.ParseResult parseResult)
        {
            if (parseResult.ValuesByTag.TryGetValue(0x39, out var sublampTableValue)
                && sublampTableValue is IReadOnlyList<LampSublampTableEntry> sublampTable)
            {
                component.SublampTable = sublampTable;
            }

            if (component.Int32s.TryGetValue("WidthDiff", out int widthDiff))
            {
                component.Int32s["WidthDiff"] = widthDiff * 2;
            }

            if (component.Bytes.TryGetValue("SelectedOffColourId", out byte colourId))
            {
                var offColourName = offColourOptions.TryGetValue(colourId, out var name) ? name : "Mask 1";
                component.Strings["OffColour"] = offColourName;
            }

            ApplyReelMode(component);
        }

        private static void ApplyReelMode(Reel component)
        {
            uint reelMode = component.UInt32s.TryGetValue("ReelMode", out uint value) ? value : 0;
            component.UInt32s.Remove("ReelMode");

            switch (reelMode)
            {
                case 0:
                    component.Booleans["DblSymbols"] = false;
                    component.Booleans["IGTReel"] = false;
                    break;
                case 1:
                    component.Booleans["DblSymbols"] = false;
                    component.Booleans["IGTReel"] = true;
                    break;
                case 2:
                    component.Booleans["DblSymbols"] = true;
                    component.Booleans["IGTReel"] = false;
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported ReelMode value {reelMode}.");
            }
        }
    }
}
