using MfmeFmlDecoder.Model;
using MfmeFmlDecoder.src.Decoder.Component.Core;
using MfmeFmlDecoder.src.Decoder.Component.Helper;
using MfmeFmlDecoder.src.Model.Component;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MfmeFmlDecoder.src.Decoder.Component
{
    internal sealed class LampParser : ComponentParserBase<Lamp>
    {
        private readonly ComponentTagMap componentTagMap = new ComponentTagMap
        {
            { 0x1C, new TagInfo(0x04, "NumberOfDefinedLampNumbers", new byte[] { }, ValueRole.UINT32) },
            { 0x38, new TagInfo(0x04, "SublampCount", new byte[] { }, ValueRole.SUBLAMP_COUNT) },
            { 0x39, new TagInfo(0x04, "SublampTable", new byte[] { }, ValueRole.LAMP_SUBLAMP_TABLE) },
            { 0x3F, new TagInfo(0x00, "OffText", Array.Empty<byte>(), ValueRole.TEXT) },
            { 0x27, new TagInfo(0x00, "PrimaryFont", Array.Empty<byte>(), ValueRole.FONT) },
            { 0x3B, new TagInfo(0x04, "Unknown 0x3B", Array.Empty<byte>(), ValueRole.UINT32) },
            { 0x3C, new TagInfo(0x01, "Inverted", new byte[] { 0x00 }, ValueRole.BOOLEAN) },
            { 0x17, new TagInfo(0x01, "Unknown 0x17", new byte[] { 0x00 }, ValueRole.BOOLEAN) },
            { 0x29, new TagInfo(0x01, "Lockout", new byte[] { 0x00 }, ValueRole.BOOLEAN) },
            { 0x49, new TagInfo(0x04, "SelectedCoinNoteId", new byte[ ] { 0xFF, 0xFF, 0xFF, 0xFF }, ValueRole.INT32) },
            { 0x48, new TagInfo(0x04, "SelectedEffectId", new byte[ ] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x18, new TagInfo(0x04, "ButtonNumber", new byte[ ] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x4A, new TagInfo(0x04, "InhibitLampNumber", new byte[ ] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x15, new TagInfo(0x01, "Shortcut 1 Enabled", new byte[ ] { 0x00 }, ValueRole.BOOLEAN) },
            { 0x16, new TagInfo(0x04, "Shortcut 1", new byte[ ] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x2A, new TagInfo(0x01, "Shortcut 2 Enabled", new byte[ ] { 0x00 }, ValueRole.BOOLEAN) },
            { 0x2B, new TagInfo(0x04, "Shortcut 2", new byte[ ] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x3E, new TagInfo(0x01, "Unknown 0x3E", new byte[ ] { 0x00 }, ValueRole.BOOLEAN) },
            { 0x46, new TagInfo(0x00, "Overlay Bitmap", Array.Empty<byte>(), ValueRole.BITMAP) },
            { 0x36, new TagInfo(0x00, "Overlay Bitmap", Array.Empty<byte>(), ValueRole.BITMAP) },
            { 0x68, new TagInfo(0x04, "Unknown 0x68", new byte[ ] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x19, new TagInfo(0x01, "Coin / Note Selected", new byte[ ] { 0x00 }, ValueRole.BOOLEAN) },
        }.WithNestedTagBlock(new ComponentTagMap
            {
                { 0x04, new TagInfo(0x04, "Sublamp1Colour", Array.Empty<byte>(), ValueRole.ARGB_COLOR) },
                { 0x05, new TagInfo(0x04, "Sublamp2Colour", Array.Empty<byte>(), ValueRole.ARGB_COLOR) },
                { 0x15, new TagInfo(0x04, "Sublamp3Colour", Array.Empty<byte>(), ValueRole.ARGB_COLOR) },
                { 0x16, new TagInfo(0x04, "Sublamp4Colour", Array.Empty<byte>(), ValueRole.ARGB_COLOR) },
                { 0x32, new TagInfo(0x04, "Sublamp5Colour", Array.Empty<byte>(), ValueRole.ARGB_COLOR) },
                { 0x33, new TagInfo(0x04, "Sublamp6Colour", Array.Empty<byte>(), ValueRole.ARGB_COLOR) },
                { 0x34, new TagInfo(0x04, "Sublamp7Colour", Array.Empty<byte>(), ValueRole.ARGB_COLOR) },
                { 0x35, new TagInfo(0x04, "Sublamp8Colour", Array.Empty<byte>(), ValueRole.ARGB_COLOR) },
                { 0x42, new TagInfo(0x04, "Sublamp9Colour", Array.Empty<byte>(), ValueRole.ARGB_COLOR) },
                { 0x43, new TagInfo(0x04, "Sublamp10Colour", Array.Empty<byte>(), ValueRole.ARGB_COLOR) },
                { 0x44, new TagInfo(0x04, "Sublamp11Colour", Array.Empty<byte>(), ValueRole.ARGB_COLOR) },
                { 0x45, new TagInfo(0x04, "Sublamp12Colour", Array.Empty<byte>(), ValueRole.ARGB_COLOR) },
                { 0x06, new TagInfo(0x04, "OffImageColour", Array.Empty<byte>(), ValueRole.ARGB_COLOR) },
                { 0x19, new TagInfo(0x00, "On1Font", Array.Empty<byte>(), ValueRole.FONT) },
                { 0x26, new TagInfo(0x00, "On1Text", Array.Empty<byte>(), ValueRole.TEXT) },
                { 0x1A, new TagInfo(0x00, "On2Font", Array.Empty<byte>(), ValueRole.FONT) },
                { 0x27, new TagInfo(0x00, "On2Text", Array.Empty<byte>(), ValueRole.TEXT) },
                { 0x1D, new TagInfo(0x00, "On3Font", Array.Empty<byte>(), ValueRole.FONT) },
                { 0x28, new TagInfo(0x00, "On3Text", Array.Empty<byte>(), ValueRole.TEXT) },
                { 0x1E, new TagInfo(0x00, "On4Font", Array.Empty<byte>(), ValueRole.FONT) },
                { 0x29, new TagInfo(0x00, "On4Text", Array.Empty<byte>(), ValueRole.TEXT) },
                { 0x4E, new TagInfo(0x00, "On5Font", Array.Empty<byte>(), ValueRole.FONT) },
                { 0x52, new TagInfo(0x00, "On5Text", Array.Empty<byte>(), ValueRole.TEXT) },
                { 0x4F, new TagInfo(0x00, "On6Font", Array.Empty<byte>(), ValueRole.FONT) },
                { 0x53, new TagInfo(0x00, "On6Text", Array.Empty<byte>(), ValueRole.TEXT) },
                { 0x50, new TagInfo(0x00, "On7Font", Array.Empty<byte>(), ValueRole.FONT) },
                { 0x54, new TagInfo(0x00, "On7Text", Array.Empty<byte>(), ValueRole.TEXT) },
                { 0x51, new TagInfo(0x00, "On8Font", Array.Empty<byte>(), ValueRole.FONT) },
                { 0x55, new TagInfo(0x00, "On8Text", Array.Empty<byte>(), ValueRole.TEXT) },
                { 0x01, new TagInfo(0x00, "Sublamp 1 Main image", Array.Empty<byte>(), ValueRole.BITMAP) },
                { 0x02, new TagInfo(0x00, "Sublamp 2 Main image", Array.Empty<byte>(), ValueRole.BITMAP) },
                { 0x0F, new TagInfo(0x00, "Sublamp 3 Main image", Array.Empty<byte>(), ValueRole.BITMAP) },
                { 0x10, new TagInfo(0x00, "Sublamp 4 Main image", Array.Empty<byte>(), ValueRole.BITMAP) },
                { 0x36, new TagInfo(0x00, "Sublamp 5 Main image", Array.Empty<byte>(), ValueRole.BITMAP) },
                { 0x37, new TagInfo(0x00, "Sublamp 6 Main image", Array.Empty<byte>(), ValueRole.BITMAP) },
                { 0x38, new TagInfo(0x00, "Sublamp 7 Main image", Array.Empty<byte>(), ValueRole.BITMAP) },
                { 0x39, new TagInfo(0x00, "Sublamp 8 Main image", Array.Empty<byte>(), ValueRole.BITMAP) },
                { 0x46, new TagInfo(0x00, "Sublamp 9 Main image", Array.Empty<byte>(), ValueRole.BITMAP) },
                { 0x47, new TagInfo(0x00, "Sublamp 10 Main image", Array.Empty<byte>(), ValueRole.BITMAP) },
                { 0x48, new TagInfo(0x00, "Sublamp 11 Main image", Array.Empty<byte>(), ValueRole.BITMAP) },
                { 0x49, new TagInfo(0x00, "Sublamp 12 Main image", Array.Empty<byte>(), ValueRole.BITMAP) },
                { 0x11, new TagInfo(0x00, "Sublamp 1 Mask image", Array.Empty<byte>(), ValueRole.BITMAP) },
                { 0x12, new TagInfo(0x00, "Sublamp 2 Mask image", Array.Empty<byte>(), ValueRole.BITMAP) },
                { 0x13, new TagInfo(0x00, "Sublamp 3 Mask image", Array.Empty<byte>(), ValueRole.BITMAP) },
                { 0x14, new TagInfo(0x00, "Sublamp 4 Mask image", Array.Empty<byte>(), ValueRole.BITMAP) },
                { 0x3A, new TagInfo(0x00, "Sublamp 5 Mask image", Array.Empty<byte>(), ValueRole.BITMAP) },
                { 0x3B, new TagInfo(0x00, "Sublamp 6 Mask image", Array.Empty<byte>(), ValueRole.BITMAP) },
                { 0x3C, new TagInfo(0x00, "Sublamp 7 Mask image", Array.Empty<byte>(), ValueRole.BITMAP) },
                { 0x3D, new TagInfo(0x00, "Sublamp 8 Mask image", Array.Empty<byte>(), ValueRole.BITMAP) },
                { 0x4A, new TagInfo(0x00, "Sublamp 9 Mask image", Array.Empty<byte>(), ValueRole.BITMAP) },
                { 0x4B, new TagInfo(0x00, "Sublamp 10 Mask image", Array.Empty<byte>(), ValueRole.BITMAP) },
                { 0x4C, new TagInfo(0x00, "Sublamp 11 Mask image", Array.Empty<byte>(), ValueRole.BITMAP) },
                { 0x4D, new TagInfo(0x00, "Sublamp 12 Mask image", Array.Empty<byte>(), ValueRole.BITMAP) },
                { 0x03, new TagInfo(0x00, "Brightmask Main", Array.Empty<byte>(), ValueRole.BITMAP) },
                { 0x5A, new TagInfo(0x00, "Brightmask Mask", Array.Empty<byte>(), ValueRole.BITMAP) },
                { 0x0C, new TagInfo(0x04, "Roundness", new byte[ ] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
                { 0x21, new TagInfo(0x04, "X Offset", new byte[ ] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
                { 0x22, new TagInfo(0x04, "Y Offset", new byte[ ] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
                { 0x25, new TagInfo(0x04, "OutlineColour", new byte[ ] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.ARGB_COLOR) },
                { 0x0B, new TagInfo(0x04, "ShapeParamter", new byte[ ] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.UINT32) }, // TODO: Add a half number mode for this type.
                { 0x0D, new TagInfo(0x04, "ShapeAngle", new byte[ ] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
                { 0x6A, new TagInfo(0x04, "PieSize", new byte[ ] { 0x78, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
                { 0x6B, new TagInfo(0x04, "PieStart", new byte[ ] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
                { 0x68, new TagInfo(0x00, "Unknown 0x68 (TLV Block)", new byte[ ] { }, ValueRole.TLVBLOCK) },
                { 0x08, new TagInfo(0x01, "No outline (inverted)", new byte[ ] { 0x01 }, ValueRole.BOOLEAN) },
                { 0x0A, new TagInfo(0x04, "SelectedShapeIndex", new byte[ ] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
                { 0x09, new TagInfo(0x01, "Transparent", new byte[] { 0x00 }, ValueRole.BOOLEAN) },
                { 0x24, new TagInfo(0x01, "LED", new byte[] { 0x00 }, ValueRole.BOOLEAN) },
                { 0x69, new TagInfo(0x01, "RGBLED", new byte[] { 0x00 }, ValueRole.BOOLEAN) },
                { 0x6C, new TagInfo(0x01, "Preserve Aspect Ratio", new byte[] { 0x00 }, ValueRole.BOOLEAN) },
                { 0x07, new TagInfo(0x01, "Graphic", new byte[] { 0x00 }, ValueRole.BOOLEAN) },
                { 0x17, new TagInfo(0x01, "Blend", new byte[] { 0x00 }, ValueRole.BOOLEAN) },
                { 0x23, new TagInfo(0x01, "ClickAll", new byte[] { 0x00 }, ValueRole.BOOLEAN) },

                // HACK
                // TODO: I have no idea what these do, they don't appear on forms anywhere
                // Might be related to the specific tech used
                // Appear to be inferred my MFME and added to the FML.
                { 0x5C, new TagInfo(0x14, "Unknown 0x5C", new byte[] { 0x00 }, ValueRole.RAW) },
                { 0x5D, new TagInfo(0x14, "Unknown 0x5D", new byte[] { 0x00 }, ValueRole.RAW) },
                { 0x5E, new TagInfo(0x14, "Unknown 0x5E", new byte[] { 0x00 }, ValueRole.RAW) },
            });

        public Lamp Parse(long componentOffset, uint componentId, byte[] data)
        {
            // Normalize for MFME angle bug
            var (component, offset, parseData) = ParseBase(componentOffset, componentId, data);

            var parseOptions = WithComponentTagLogging(ExtendedTagParser.Options.Default);
            var parseResult = ParseExtendedTags(component, componentTagMap, parseData, offset, parseOptions);
            ApplyExtendedTags(component, parseResult.ValuesByTag);

            return component;
        }

        private static void ApplyExtendedTags(
            Lamp component,
            IReadOnlyDictionary<uint, object> valuesByTag)
        {
            if ((valuesByTag.TryGetValue(0x49, out var selectedCoinNoteId)) && (valuesByTag.TryGetValue(0x48, out var selectedEffectId)))
            {
                component.Strings.Add("SelectedCoinNote", CoinNoteHierarchy.ResolveCoinNote((int)selectedCoinNoteId, (uint)selectedEffectId));
                component.Strings.Add("SelectedEffect", CoinNoteHierarchy.ResolveEffect((int)selectedCoinNoteId, (uint)selectedEffectId));
            }
            component.Int32s.Remove("SelectedCoinNoteId");
            component.UInt32s.Remove("SelectedEffectId");

            if (valuesByTag.TryGetValue(0x39, out var sublampTableValue)
                && sublampTableValue is IReadOnlyList<LampSublampTableEntry> sublampTable)
            {
                component.SublampTable = sublampTable;
            }
        }

    }
}
