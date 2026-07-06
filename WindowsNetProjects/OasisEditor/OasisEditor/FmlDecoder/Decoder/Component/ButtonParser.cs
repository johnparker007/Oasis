using MfmeFmlDecoder.Model;
using MfmeFmlDecoder.src.Model.Component;
using System.Collections.Generic;
using System;
using System.Linq;
using MfmeFmlDecoder.src.Decoder.Component.Core;
using MfmeFmlDecoder.src.Decoder.Component.Helper;

namespace MfmeFmlDecoder.src.Decoder.Component
{
    internal sealed class ButtonParser : ComponentParserBase<Button>
    {
        private Dictionary<uint, string> shapeOptions = new Dictionary<uint, string>
        {
            { 0x00, "Rectangle" },
            { 0x01, "Triangle Up" },
            { 0x02, "Triangle Down" },
            { 0x03, "Triangle Left" },
            { 0x04, "Triangle Right" },
            { 0x05, "Ellipse" },
        };

        private ComponentTagMap componentTagMap = new ComponentTagMap
        {
            { 0x17, new TagInfo(0x01, "Unknown 0x17", new byte[] { 0x01 }, ValueRole.BOOLEAN) },
            { 0x18, new TagInfo(0x04, "Button Number", new byte[] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x4A, new TagInfo(0x04, "InhibitLamp", new byte[] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x15, new TagInfo(0x01, "Shortcut 1 Enabled", new byte[] { 0x00 }, ValueRole.BOOLEAN) },
            { 0x16, new TagInfo(0x04, "Shortcut 1", new byte[] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x2A, new TagInfo(0x01, "Shortcut 2 Enabled", new byte[] { 0x00 }, ValueRole.BOOLEAN) },
            { 0x2B, new TagInfo(0x04, "Shortcut 2", new byte[] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x02, new TagInfo(0x04, "Lamp1Colour", new byte[] { 0x00, 0xFF, 0xFF, 0xFF}, ValueRole.ARGB_COLOR) },
            { 0x08, new TagInfo(0x04, "Lamp2Colour", new byte[] { 0x00, 0xFF, 0xFF, 0xFF}, ValueRole.ARGB_COLOR) },
            { 0x03, new TagInfo(0x04, "OffColour", new byte[] { 0x00, 0xFF, 0xFF, 0xFF}, ValueRole.ARGB_COLOR) },
            { 0x04, new TagInfo(0x01, "Graphic", new byte[] { 0x00 }, ValueRole.BOOLEAN) },
            { 0x05, new TagInfo(0x00, "Lamp 1 image", new byte[] { }, ValueRole.BITMAP) },
            { 0x07, new TagInfo(0x00, "Lamp 2 Image", new byte[] { }, ValueRole.BITMAP) },
            { 0x06, new TagInfo(0x00, "Off Image", new byte[] { }, ValueRole.BITMAP) },
            { 0x0B, new TagInfo(0x00, "Mask 1 Image", new byte[] { }, ValueRole.BITMAP) },
            { 0x0C, new TagInfo(0x00, "Mask 2 Image", new byte[] { }, ValueRole.BITMAP) },
            { 0x36, new TagInfo(0x00, "Overlay Image", new byte[] { }, ValueRole.BITMAP) },
            { 0x3C, new TagInfo(0x01, "Inverted", new byte[] { 0x00 }, ValueRole.BOOLEAN) },
            { 0x09, new TagInfo(0x01, "Split", new byte[] { 0x00 }, ValueRole.BOOLEAN) },
            { 0x29, new TagInfo(0x01, "Lockout", new byte[] { 0x00 }, ValueRole.BOOLEAN) },
            { 0x0F, new TagInfo(0x01, "Led", new byte[] { 0x00 }, ValueRole.BOOLEAN) },
            { 0x01, new TagInfo(0x04, "SelectedShapeId", new byte[] { }, ValueRole.UINT32) },
            { 0x49, new TagInfo(0x04, "SelectedCoinNoteId", new byte[ ] { 0xFF, 0xFF, 0xFF, 0xFF }, ValueRole.INT32) },
            { 0x48, new TagInfo(0x04, "SelectedEffectId", new byte[ ] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x27, new TagInfo(0x00, "PrimaryFont", Array.Empty<byte>(), ValueRole.FONT) },
            { 0x1C, new TagInfo(0x04, "NonNullSublampCount", new byte[] { 0x00 }, ValueRole.UINT32) },
            { 0x38, new TagInfo(0x04, "SublampCount", new byte[] { }, ValueRole.SUBLAMP_COUNT) },
            { 0x39, new TagInfo(0x04, "SublampTable", new byte[] { }, ValueRole.LAMP_SUBLAMP_TABLE) },
            { 0x3B, new TagInfo(0x04, "Unknown 0x3B", new byte[] { }, ValueRole.UINT32) },
            { 0x3F, new TagInfo(0x00, "Label (UTF-16)", new byte[] { }, ValueRole.TEXT) },
            { 0x30, new TagInfo(0x01, "Unknown 0x30", new byte[] { }, ValueRole.BOOLEAN) },
            { 0x19, new TagInfo(0x01, "Unknown 0x19", new byte[] { }, ValueRole.BOOLEAN) },
            { 0x0D, new TagInfo(0x04, "XOffset", new byte[ ] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x0E, new TagInfo(0x04, "YOffset", new byte[ ] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
        };

        public Button Parse(long componentOffset, uint componentId, byte[] data)
        {
            // Normalize for MFME angle bug
            var (component, offset, parseData) = ParseBase(
                componentOffset,
                componentId,
                data,
                normalizationRule: new GeometryAngleNormalization.Rule(
                    RewriteTriggerOffsetDelta: 0,
                    ValidAngleOffsetDelta: 0));

            DumpRemaining(componentOffset, parseData, offset);

            var parseOptions = WithComponentTagLogging(ExtendedTagParser.Options.Default);
            var parseResult = ParseExtendedTags(component, componentTagMap, parseData, offset, parseOptions);

            ApplyExtendedTags(component, parseResult.ValuesByTag, shapeOptions);

            return component;
        }

        private static void ApplyExtendedTags(Button component, System.Collections.Generic.IReadOnlyDictionary<uint, object> valuesByTag, Dictionary<uint, string> shapeOptions)
        {
            if ((valuesByTag.TryGetValue(0x49, out var selectedCoinNoteId)) && (valuesByTag.TryGetValue(0x48, out var selectedEffectId)))
            {
                component.Strings.Add("SelectedCoinNote", CoinNoteHierarchy.ResolveCoinNote((int)selectedCoinNoteId, (uint)selectedEffectId));
                component.Strings.Add("SelectedEffect", CoinNoteHierarchy.ResolveEffect((int)selectedCoinNoteId, (uint)selectedEffectId));
            }
            component.Int32s.Remove("SelectedCoinNoteId");
            component.UInt32s.Remove("SelectedEffectId");
            if (valuesByTag.TryGetValue(0x01, out var selectedShapeId)) component.Strings.Add("SelectedShape", shapeOptions[(uint)selectedShapeId]);

            if (valuesByTag.TryGetValue(0x39, out var sublampTableValue)
                && sublampTableValue is IReadOnlyList<LampSublampTableEntry> sublampTable)
            {
                component.SublampTable = sublampTable;
            }
        }
    }
}