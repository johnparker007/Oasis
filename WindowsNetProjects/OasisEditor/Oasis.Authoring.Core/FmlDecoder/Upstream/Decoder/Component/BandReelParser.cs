using MfmeFmlDecoder.Decoder;
using MfmeFmlDecoder.Model;
using MfmeFmlDecoder.src.Model.Component;
using System.Collections.Generic;
using System;
using MfmeFmlDecoder.src.Decoder.Component.Core;
using MfmeFmlDecoder.src.Decoder.Component.Profiles;

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


        public BandReel Parse(ParsingContext context, long componentOffset, uint componentId, byte[] data)
        {
            ComponentProfile profile = ComponentProfileCatalog.Resolve(MFMEComponentType.BandReel, context.Version);
            // Normalize for MFME angle bug
            var (component, offset, parseData) = ParseBase(context, profile, componentOffset, componentId, data);

            DumpRemaining(componentOffset, parseData, offset);

                        var parseResult = ParseExtendedTags(component, profile, parseData, offset);

            ApplyExtendedTags(component, parseResult.ValuesByTag, offColourOptions);

            // Populate mask slots from sublamp table (tag 0x39 = 57)
            if (parseResult.ValuesByTag.TryGetValue(0x39, out var sublampData) &&
                sublampData is List<LampSublampTableEntry> sublampTable)
            {
                PopulateMaskSlotsFromSublampTable(component, sublampTable, profile.Defaults);
            }

            return component;
        }

        private static void ApplyExtendedTags(BandReel component, System.Collections.Generic.IReadOnlyDictionary<uint, object> valuesByTag, Dictionary<uint, string> offColourOptions)
        {
            if (valuesByTag.TryGetValue(0x0B, out object legacyReverse)
                && legacyReverse is bool reverse)
            {
                component.Booleans["Reverse"] = reverse;
            }

            if (valuesByTag.TryGetValue(0x13, out var view)) component.View = (uint)view;
            if (valuesByTag.TryGetValue(0x18, out var selectedOffColour)) component.Strings.Add("OffColour", offColourOptions[(byte)selectedOffColour]);

            if (valuesByTag.TryGetValue(0x0A, out object orientationValue)
                && orientationValue is byte orientationFlag)
            {
                component.Orientation = orientationFlag == 0 ? "Vertical" : "Horizontal";
                component.UInt16s.Remove("HorizontalVertical");
            }
            else if (component.UInt16s.TryGetValue("HorizontalVertical", out ushort horizontalVertical))
            {
                component.Orientation = horizontalVertical == 0 ? "Vertical" : "Horizontal";
                component.UInt16s.Remove("HorizontalVertical");
            }
        }

        private static void PopulateSequentialMaskSlots(BandReel component, List<LampSublampTableEntry> sublampTable)
        {
            for (int mask = 0; mask < 3; mask++)
            {
                string maskName = $"mask{mask + 1}";
                int baseIndex = mask * 10;

                for (int slot = 0; slot < 5; slot++)
                {
                    component.MaskSlots.Masks[maskName][$"{slot + 1}a"].SublampNumber = -2;
                    component.MaskSlots.Masks[maskName][$"{slot + 1}b"].SublampNumber = -2;
                }

                for (int slot = 0; slot < 5; slot++)
                {
                    SetMaskSlot(component, sublampTable, maskName, $"{slot + 1}a", baseIndex + slot);
                    SetMaskSlot(component, sublampTable, maskName, $"{slot + 1}b", baseIndex + 5 + slot);
                }
            }
        }

        private static void SetMaskSlot(
            BandReel component,
            List<LampSublampTableEntry> sublampTable,
            string maskName,
            string slotName,
            int index)
        {
            if (index < sublampTable.Count)
            {
                component.MaskSlots.Masks[maskName][slotName].SublampNumber = sublampTable[index].SublampNumber;
            }
        }

        private static void PopulateMaskSlotsFromSublampTable(
            BandReel component,
            List<LampSublampTableEntry> sublampTable,
            DefaultsProfile defaults)
        {
            if (defaults?.SequentialBandReelMaskSlots == true)
            {
                PopulateSequentialMaskSlots(component, sublampTable);
            }
            else
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

            if (defaults?.DefaultUndefinedMaskSlotsToMinusTwo == true && defaults.SequentialBandReelMaskSlots != true)
            {
                foreach (var mask in component.MaskSlots.Masks.Values)
                {
                    foreach (var slot in mask.Values)
                    {
                        if (slot.SublampNumber == 0)
                        {
                            slot.SublampNumber = -2;
                        }
                    }
                }
            }
        }
    }
}