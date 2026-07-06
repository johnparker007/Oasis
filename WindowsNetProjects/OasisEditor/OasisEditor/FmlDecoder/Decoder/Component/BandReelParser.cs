using MfmeFmlDecoder.Model;
using MfmeFmlDecoder.src.Model.Component;
using System.Collections.Generic;
using System;
using MfmeFmlDecoder.src.Decoder.Component.Core;

namespace MfmeFmlDecoder.src.Decoder.Component
{
    internal sealed class BandReelParser : ComponentParserBase<BandReel>
    {
        private static readonly Dictionary<(string mask, string slot), int> SlotToIndexMap = new()
        {
            // Mask 1
            [("mask1", "1a")] = 0,
            [("mask1", "1b")] = 6,
            [("mask1", "2a")] = 1,
            [("mask1", "2b")] = 7,
            [("mask1", "3a")] = 2,
            [("mask1", "3b")] = 8,
            [("mask1", "4a")] = 3,
            [("mask1", "4b")] = 9,
            [("mask1", "5a")] = 4,
            [("mask1", "5b")] = 10,
            [("mask1", "6a")] = 5,
            [("mask1", "6b")] = 11,
            [("mask1", "7a")] = 24,
            [("mask1", "7b")] = 25,
            [("mask1", "8a")] = 29,
            [("mask1", "8b")] = 29,
            [("mask1", "9a")] = 33,
            [("mask1", "9b")] = 33,
            [("mask1", "10a")] = 37,
            [("mask1", "10b")] = 38,

            // Mask 2
            [("mask2", "1a")] = 12,
            [("mask2", "1b")] = 18,
            [("mask2", "2a")] = 13,
            [("mask2", "2b")] = 19,
            [("mask2", "3a")] = 14,
            [("mask2", "3b")] = 20,
            [("mask2", "4a")] = 15,
            [("mask2", "4b")] = 21,
            [("mask2", "5a")] = 16,
            [("mask2", "5b")] = 22,
            [("mask2", "6a")] = 17,
            [("mask2", "6b")] = 23,
            [("mask2", "7a")] = 26,
            [("mask2", "7b")] = 27,
            [("mask2", "8a")] = 30,
            [("mask2", "8b")] = 31,
            [("mask2", "9a")] = 34,
            [("mask2", "9b")] = 35,
            [("mask2", "10a")] = 38,
            [("mask2", "10b")] = 39,

            // Mask 3
            [("mask3", "1a")] = 40,
            [("mask3", "1b")] = 50,
            [("mask3", "2a")] = 41,
            [("mask3", "2b")] = 51,
            [("mask3", "3a")] = 42,
            [("mask3", "3b")] = 52,
            [("mask3", "4a")] = 43,
            [("mask3", "4b")] = 53,
            [("mask3", "5a")] = 44,
            [("mask3", "5b")] = 54,
            [("mask3", "6a")] = 45,
            [("mask3", "6b")] = 55,
            [("mask3", "7a")] = 46,
            [("mask3", "7b")] = 56,
            [("mask3", "8a")] = 47,
            [("mask3", "8b")] = 57,
            [("mask3", "9a")] = 48,
            [("mask3", "9b")] = 58,
            [("mask3", "10a")] = 49,
            [("mask3", "10b")] = 59
        };

        private Dictionary<uint, string> offColourOptions = new Dictionary<uint, string>
        {
            { 0x0, "Mask 1"},
            { 0x1, "Mask 2"},
            { 0x2, "Mask 3"},
            { 0x3, "White"},
        };

        private ComponentTagMap componentTagMap = new ComponentTagMap
        {
            { 0x01, new TagInfo(0x04, "Stops", new byte[] { 0x10, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x02, new TagInfo(0x04, "HalfSteps", new byte[] { 0x40, 0x01 }, ValueRole.UINT32) },
            { 0x13, new TagInfo(0x04, "View", new byte[] { 0x05, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x03, new TagInfo(0x04, "Offset", new byte[] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x19, new TagInfo(0x04, "Spacing", new byte[] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x0E, new TagInfo(0x04, "OptoTab", new byte[] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x04, new TagInfo(0x04, "BorderWidth", new byte[] { 0x01, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x05, new TagInfo(0x04, "BorderColour", new byte[] { 0x00, 0x00, 0x00, 0xFF }, ValueRole.ARGB_COLOR) },
            { 0x40, new TagInfo(0x01, "Reverse", new byte[] { 0x00 }, ValueRole.BOOLEAN) },
            { 0x3C, new TagInfo(0x01, "Inverted", new byte[] { 0x00 }, ValueRole.BOOLEAN) },
            { 0x0C, new TagInfo(0x01, "Opaque", new byte[] { 0x00 }, ValueRole.BOOLEAN) },
            { 0x10, new TagInfo(0x01, "Custom", new byte[] { 0x00 }, ValueRole.BOOLEAN) },
            { 0x06, new TagInfo(0x01, "Lamps", new byte[] { 0x00 }, ValueRole.BOOLEAN) },
            { 0x4C, new TagInfo(0x02, "HorizontalVertical", new byte[] { 0x01, 0x00 }, ValueRole.UINT16) },
            { 0x17, new TagInfo(0x01, "OffLevel", new byte[] { 0x40 }, ValueRole.BYTE) },
            { 0x18, new TagInfo(0x01, "SelectedOffColour", new byte[] { 0x00 }, ValueRole.BYTE) },
            { 0x1C, new TagInfo(0x04, "NonNullSublampCount", new byte[] { 0x00 }, ValueRole.UINT32) },
            { 0x38, new TagInfo(0x04, "SublampCount", new byte[] { 0x00 }, ValueRole.SUBLAMP_COUNT) },
            { 0x39, new TagInfo(0x04, "SublampTable", new byte[] { 0x00 }, ValueRole.LAMP_SUBLAMP_TABLE) },
            { 0x3B, new TagInfo(0x04, "Unknown 0x3B", new byte[] { 0x00 }, ValueRole.UINT32) },
            { 0x36, new TagInfo(0x00, "Overlay Image", Array.Empty<byte>(), ValueRole.BITMAP) },
            { 0x09, new TagInfo(0x00, "Band Image", Array.Empty<byte>(), ValueRole.BITMAP) },
            { 0x07, new TagInfo(0x00, "Lamp Mask 1-20 Image", Array.Empty<byte>(), ValueRole.BITMAP) },
            { 0x0D, new TagInfo(0x00, "Lamp Mask 21-40 Image", Array.Empty<byte>(), ValueRole.BITMAP) },
            { 0x16, new TagInfo(0x00, "Lamp Mask 41-60 Image", Array.Empty<byte>(), ValueRole.BITMAP) },
        };

        public BandReel Parse(long componentOffset, uint componentId, byte[] data)
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

            var parseOptions = WithComponentTagLogging(
                ExtendedTagParser.Options
                .WithBitmapTags(0x36, 0x09, 0x07, 0x0D, 0x16)
            );
            var parseResult = ParseExtendedTags(component, componentTagMap, parseData, offset, parseOptions);

            ApplyExtendedTags(component, parseResult.ValuesByTag, offColourOptions);

            // Populate mask slots from sublamp table (tag 0x39 = 57)
            if (parseResult.ValuesByTag.TryGetValue(0x39, out var sublampData) &&
                sublampData is List<LampSublampTableEntry> sublampTable)
            {
                PopulateMaskSlotsFromSublampTable(component, sublampTable);
            }

            return component;
        }

        private static void ApplyExtendedTags(BandReel component, System.Collections.Generic.IReadOnlyDictionary<uint, object> valuesByTag, Dictionary<uint, string> offColourOptions)
        {
            if (valuesByTag.TryGetValue(0x13, out var view)) component.View = (uint)view;
            if (valuesByTag.TryGetValue(0x18, out var selectedOffColour)) component.Strings.Add("OffColour", offColourOptions[(byte)selectedOffColour]);
        }

        private static void PopulateMaskSlotsFromSublampTable(
            BandReel component,
            List<LampSublampTableEntry> sublampTable)
        {
            foreach (var mapping in SlotToIndexMap)
            {
                string maskName = mapping.Key.mask;
                string slotName = mapping.Key.slot;
                int index = mapping.Value;

                if (index < sublampTable.Count)
                {
                    var entry = sublampTable[index];
                    component.MaskSlots.Masks[maskName][slotName].SublampNumber = entry.SublampNumber;
                }
            }
        }
    }
}