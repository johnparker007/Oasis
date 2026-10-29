using System.Collections.Generic;
using System;
using MfmeFmlDecoder.Decoder;
using MfmeFmlDecoder.Model;
using MfmeFmlDecoder.src.Decoder.Component.Core;

namespace MfmeFmlDecoder.src.Decoder.Component.Profiles.V20_1
{
    internal static class SevenSegProfileV20_1
    {
        public static readonly ComponentProfile Profile = ComponentProfileFactory.Create(
            MFMEComponentType.SevenSeg,
            MfmeVersion.V20_1,
            tags: new ComponentTagMap
        {
            { 0x1C, new TagInfo(0x04, "NumberOfDefinedLampNumbers", Array.Empty<byte>(), ValueRole.UINT32) },
            { 0x36, new TagInfo(0x00, "Overlay Image", Array.Empty<byte>(), ValueRole.BITMAP) },
            { 0x38, new TagInfo(0x04, "SublampCount", new byte[] { 0x00 }, ValueRole.SUBLAMP_COUNT) },
            { 0x39, new TagInfo(0x04, "SublampTable", new byte[] { 0x00 }, ValueRole.LAMP_SUBLAMP_TABLE) },
            { 0x3B, new TagInfo(0x04, "Unknown 0x3B", Array.Empty<byte>(), ValueRole.UINT32) },
            { 0x08, new TagInfo(0x04, "Unknown 0x08", new byte[] { 0x32, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
        }.WithNestedTagBlock(new ComponentTagMap
        {
            { 0x26, new TagInfo(0x01, "Alpha", new byte[] { 0x00 }, ValueRole.BOOLEAN) },
            { 0x27, new TagInfo(0x01, "DPOff", new byte[] { 0x00 }, ValueRole.BOOLEAN) },
            { 0x09, new TagInfo(0x01, "DPOn", new byte[] { 0x00 }, ValueRole.BOOLEAN) },
            { 0x21, new TagInfo(0x01, "AutoDP", new byte[] { 0x00 }, ValueRole.BOOLEAN) },
            { 0x0A, new TagInfo(0x01, "Segment16", new byte[] { 0x00 }, ValueRole.BOOLEAN) },
            { 0x1D, new TagInfo(0x01, "ZeroOn", new byte[] { 0x00 }, ValueRole.BOOLEAN) },
            { 0x08, new TagInfo(0x01, "DPRight", new byte[] { 0x00 }, ValueRole.BOOLEAN) },
            { 0x06, new TagInfo(0x04, "SegmentOnColour", new byte[] { 0x00, 0x00, 0xFF, 0xFF }, ValueRole.ARGB_COLOR) },
            { 0x07, new TagInfo(0x04, "SegmentOffColour", new byte[] { 0x30, 0x30, 0x30, 0xFF }, ValueRole.ARGB_COLOR) },
            { 0x05, new TagInfo(0x04, "BackgroundColour", new byte[] { 0x00, 0x00, 0x00, 0xFF }, ValueRole.ARGB_COLOR) },
            { 0x01, new TagInfo(0x04, "Thickness", new byte[] { 0x03, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x02, new TagInfo(0x04, "Spacing", new byte[] { 0x01, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x03, new TagInfo(0x04, "Horizontal Spacing", new byte[] { 0x0A, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x04, new TagInfo(0x04, "Vertical Spacing", new byte[] { 0x06, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x25, new TagInfo(0x04, "Offset", new byte[] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x22, new TagInfo(0x04, "DigitAngle", new byte[] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x1C, new TagInfo(0x04, "Slant", new byte[] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x1E, new TagInfo(0x04, "Chop", new byte[] { 0x4B, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x1F, new TagInfo(0x04, "Centre", new byte[] { 0x32, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x0B, new TagInfo(0x01, "Programmable", new byte[] { 0x00 }, ValueRole.BOOLEAN) },
            { 0x20, new TagInfo(0x01, "SelectedStyleId", new byte[] { 0x00 }, ValueRole.BYTE) },
            { 0x24, new TagInfo(0x01, "Unknown 0x24", new byte[] { 0x00 }, ValueRole.BYTE) },
        }),
            angle: AngleProfile.Rewrite(0),
            options: ExtendedTagParser.Options .WithBitmapTags(0x36),
            baseSection: BaseSectionProfile.SingleStream);
    }
}
