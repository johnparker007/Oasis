using System.Collections.Generic;
using System;
using MfmeFmlDecoder.Decoder;
using MfmeFmlDecoder.Model;
using MfmeFmlDecoder.src.Decoder.Component.Core;

namespace MfmeFmlDecoder.src.Decoder.Component.Profiles.V20_1
{
    internal static class FlipReelProfileV20_1
    {
        public static readonly ComponentProfile Profile = ComponentProfileFactory.Create(
            MFMEComponentType.FlipReel,
            MfmeVersion.V20_1,
            tags: new ComponentTagMap
        {
            { 0x01, new TagInfo(0x04, "Stops", new byte[] { 0x47, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x02, new TagInfo(0x04, "HalfSteps", new byte[] { 0xB0, 0x01, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x03, new TagInfo(0x04, "Offset", new byte[] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x05, new TagInfo(0x04, "BorderColour", new byte[] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.ARGB_COLOR) },
            { 0x04, new TagInfo(0x04, "BorderWidth", new byte[] { 0x04, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x08, new TagInfo(0x00, "Reel Image", Array.Empty<byte>(), ValueRole.BITMAP) },
            { 0x07, new TagInfo(0x00, "Lamp Mask 1", Array.Empty<byte>(), ValueRole.BITMAP) },
            { 0x09, new TagInfo(0x01, "Unknown 0x09", new byte[] { 0x00 }, ValueRole.BYTE) },
            { 0x0A, new TagInfo(0x00, "Lamp Mask 2", Array.Empty<byte>(), ValueRole.BITMAP) },
            { 0x0E, new TagInfo(0x00, "Lamp Mask 3", Array.Empty<byte>(), ValueRole.BITMAP) },
            { 0x36, new TagInfo(0x00, "Overlay Image", Array.Empty<byte>(), ValueRole.BITMAP) },
            { 0x1C, new TagInfo(0x04, "NumberOfDefinedLampNumbers", new byte[] { }, ValueRole.UINT32) },
            { 0x38, new TagInfo(0x04, "SublampCount", new byte[] { }, ValueRole.SUBLAMP_COUNT) },
            { 0x39, new TagInfo(0x04, "SublampTable", new byte[] { }, ValueRole.LAMP_SUBLAMP_TABLE) },
            { 0x06, new TagInfo(0x01, "LampsEnabled", new byte[] { 0x00 }, ValueRole.BOOLEAN) },
            { 0x3C, new TagInfo(0x01, "Inverted", new byte[] { 0x00 }, ValueRole.BOOLEAN) },
            { 0x3B, new TagInfo(0x04, "Unknown3B", new byte[] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            // Note: For some reason 'reversed' does not get exported as a tag, basically meaning that the layout does not store this flag - an MFME bug
        },
            angle: AngleProfile.Rewrite(0),
            options: ExtendedTagParser.Options.WithBitmapTags(0x08, 0x07, 0x0A, 0x0E, 0x36),
            baseSection: BaseSectionProfile.SingleStream);
    }
}
