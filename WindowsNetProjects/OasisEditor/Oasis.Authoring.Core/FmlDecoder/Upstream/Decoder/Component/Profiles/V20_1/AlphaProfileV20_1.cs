using System.Collections.Generic;
using System;
using MfmeFmlDecoder.Decoder;
using MfmeFmlDecoder.Model;
using MfmeFmlDecoder.src.Decoder.Component.Core;

namespace MfmeFmlDecoder.src.Decoder.Component.Profiles.V20_1
{
    internal static class AlphaProfileV20_1
    {
        public static readonly ComponentProfile Profile = ComponentProfileFactory.Create(
            MFMEComponentType.Alpha,
            MfmeVersion.V20_1,
            tags: new ComponentTagMap
        {
            { 0x01, new TagInfo(0x04, "Colour", Array.Empty<byte>(), ValueRole.ARGB_COLOR) },
            { 0x02, new TagInfo(0x04, "DigitWidth", new byte[] { 0x11, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x03, new TagInfo(0x04, "Columns", new byte[] { 0x10, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x04, new TagInfo(0x00, "Alpha Character Bitmap", Array.Empty<byte>(), ValueRole.BITMAP) },
            { 0x08, new TagInfo(0x04, "Unknown 0x08", new byte[] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x36, new TagInfo(0x00, "Overlay Image", Array.Empty<byte>(), ValueRole.BITMAP) },
            { 0x3B, new TagInfo(0x04, "Unknown3B", new byte[] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x40, new TagInfo(0x01, "Reversed", new byte[] { 0x00 }, ValueRole.BOOLEAN) },
        },
            angle: AngleProfile.Default,
            options: ExtendedTagParser.Options.WithBitmapTags(0x04, 0x36),
            baseSection: BaseSectionProfile.SingleStream);
    }
}
