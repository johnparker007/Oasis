using System.Collections.Generic;
using System;
using MfmeFmlDecoder.Decoder;
using MfmeFmlDecoder.Model;
using MfmeFmlDecoder.src.Decoder.Component.Core;

namespace MfmeFmlDecoder.src.Decoder.Component.Profiles.V20_1
{
    internal static class SevenSegBlockProfileV20_1
    {
        public static readonly ComponentProfile Profile = ComponentProfileFactory.Create(
            MFMEComponentType.SevenSegBlock,
            MfmeVersion.V20_1,
            tags: new ComponentTagMap
        {
            { 0x38, new TagInfo(0x04, "SublampCount", new byte[] { 0x00 }, ValueRole.SUBLAMP_COUNT) },
            { 0x39, new TagInfo(0x04, "SublampTable", new byte[] { 0x00 }, ValueRole.LAMP_SUBLAMP_TABLE) },
            { 0x3B, new TagInfo(0x04, "Unknown 0x3B", Array.Empty<byte>(), ValueRole.UINT32) },
            { 0x36, new TagInfo(0x00, "Overlay Image", Array.Empty<byte>(), ValueRole.BITMAP) },
        }.WithNestedTagBlock(new ComponentTagMap
        {
            { 0x15, new TagInfo(0x04, "Columns", new byte[] { 0x01, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x14, new TagInfo(0x04, "Rows", new byte[] { 0x01, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x16, new TagInfo(0x04, "RowSpacing", new byte[] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x17, new TagInfo(0x04, "ColumnSpacing", new byte[] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x19, new TagInfo(0x04, "DigitWidth", new byte[] { 0x18, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x1A, new TagInfo(0x04, "DigitHeight", new byte[] { 0x20, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x10, new TagInfo(0x01, "SelectedTypeId", new byte[] { 0x00 }, ValueRole.BYTE) },
            { 0x08, new TagInfo(0x01, "DPRight", new byte[] { 0x01 }, ValueRole.BOOLEAN) },
            { 0x0A, new TagInfo(0x01, "Segment14", new byte[] { 0x00 }, ValueRole.BOOLEAN) },
            { 0x01, new TagInfo(0x04, "Thickness", new byte[] { 0x03, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x02, new TagInfo(0x04, "Spacing", new byte[] { 0x01, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x03, new TagInfo(0x04, "HorizontalSizePercent", new byte[] { 0x40, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x04, new TagInfo(0x04, "VerticalSizePercent", new byte[] { 0x4E, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x05, new TagInfo(0x04, "BackColour", new byte[] { 0x00, 0x00, 0xFF, 0xFF }, ValueRole.ARGB_COLOR) },
            { 0x06, new TagInfo(0x04, "OnColour", new byte[] { 0xFF, 0x00, 0x00, 0xFF }, ValueRole.ARGB_COLOR) },
            { 0x07, new TagInfo(0x04, "OffColour", new byte[] { 0x00, 0xFF, 0x00, 0xFF }, ValueRole.ARGB_COLOR) },
            { 0x1C, new TagInfo(0x04, "Offset", new byte[] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x12, new TagInfo(0x04, "DigitAngle", new byte[] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x0C, new TagInfo(0x04, "Slant", new byte[] { 0x06, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x0E, new TagInfo(0x04, "Chop", new byte[] { 0x4B, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x0F, new TagInfo(0x04, "Centre", new byte[] { 0x32, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x0B, new TagInfo(48, "ProgrammableDigitMask", Array.Empty<byte>(), ValueRole.PROGRAMMABLE_DIGIT_MASK) },
            { 0x1B, new TagInfo(0x00, "ProgrammableLampTable", Array.Empty<byte>(), ValueRole.PROGRAMMABLE_LAMP_TABLE) },
            { 0x09, new TagInfo(48, "DPOn", Array.Empty<byte>(), ValueRole.DIGIT_FLAG_ARRAY) },
            { 0x0D, new TagInfo(48, "ZeroOn", Array.Empty<byte>(), ValueRole.DIGIT_FLAG_ARRAY) },
            { 0x11, new TagInfo(48, "AutoDP", Array.Empty<byte>(), ValueRole.DIGIT_FLAG_ARRAY) },
            { 0x13, new TagInfo(48, "Visible", Array.Empty<byte>(), ValueRole.DIGIT_FLAG_ARRAY) },
            { 0x1D, new TagInfo(48, "DPOff", Array.Empty<byte>(), ValueRole.DIGIT_FLAG_ARRAY) },
            { 0x36, new TagInfo(0x00, "Overlay Image", Array.Empty<byte>(), ValueRole.BITMAP) },

        }),
            angle: AngleProfile.Rewrite(4),
            options: ExtendedTagParser.Options.Default,
            baseSection: BaseSectionProfile.SingleStream);
    }
}
