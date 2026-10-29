using System.Collections.Generic;
using System;
using MfmeFmlDecoder.Decoder;
using MfmeFmlDecoder.Model;
using MfmeFmlDecoder.src.Decoder.Component.Core;

namespace MfmeFmlDecoder.src.Decoder.Component.Profiles.V20_1
{
    internal static class BandReelProfileV20_1
    {
        public static readonly ComponentProfile Profile = ComponentProfileFactory.Create(
            MFMEComponentType.BandReel,
            MfmeVersion.V20_1,
            tags: new ComponentTagMap
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
        },
            angle: AngleProfile.Rewrite(0),
            options: ExtendedTagParser.Options .WithBitmapTags(0x36, 0x09, 0x07, 0x0D, 0x16),
            baseSection: BaseSectionProfile.SingleStream);
    }
}
