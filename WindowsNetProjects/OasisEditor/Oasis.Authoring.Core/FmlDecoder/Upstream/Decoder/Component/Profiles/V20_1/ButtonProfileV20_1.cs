using System.Collections.Generic;
using System;
using MfmeFmlDecoder.Decoder;
using MfmeFmlDecoder.Model;
using MfmeFmlDecoder.src.Decoder.Component.Core;

namespace MfmeFmlDecoder.src.Decoder.Component.Profiles.V20_1
{
    internal static class ButtonProfileV20_1
    {
        public static readonly ComponentProfile Profile = ComponentProfileFactory.Create(
            MFMEComponentType.Button,
            MfmeVersion.V20_1,
            tags: new ComponentTagMap
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
            { 0x0D, new TagInfo(0x04, "XOffset", new byte[ ] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.INT32) },
            { 0x0E, new TagInfo(0x04, "YOffset", new byte[ ] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.INT32) },
        },
            angle: AngleProfile.Rewrite(0),
            options: ExtendedTagParser.Options.Default,
            baseSection: BaseSectionProfile.V20Signature);
    }
}
