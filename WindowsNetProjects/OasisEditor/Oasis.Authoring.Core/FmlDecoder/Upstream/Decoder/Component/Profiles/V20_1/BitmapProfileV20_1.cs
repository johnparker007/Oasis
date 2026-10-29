using System.Collections.Generic;
using System;
using MfmeFmlDecoder.Decoder;
using MfmeFmlDecoder.Model;
using MfmeFmlDecoder.src.Decoder.Component.Core;

namespace MfmeFmlDecoder.src.Decoder.Component.Profiles.V20_1
{
    internal static class BitmapProfileV20_1
    {
        public static readonly ComponentProfile Profile = ComponentProfileFactory.Create(
            MFMEComponentType.Bitmap,
            MfmeVersion.V20_1,
            tags: new ComponentTagMap
        {
            { 0x01, new TagInfo(0x01, "Transparent", new byte[] { 0x00 }, ValueRole.BOOLEAN) },
            { 0x02, new TagInfo(0x00, "Image", new byte[] { }, ValueRole.BITMAP) },
            { 0x03, new TagInfo(0x04, "StretchMode Index", new byte[] { 0x00 }, ValueRole.UINT32) },
            { 0x08, new TagInfo(0x04, "Unknown 0x08", new byte[] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x36, new TagInfo(0x00, "Overlay Image", new byte[] { }, ValueRole.BITMAP) },
            { 0x33, new TagInfo(0x01, "Unknown 0x33", new byte[] { }, ValueRole.BOOLEAN) },
            { 0x05, new TagInfo(0x04, "Unknown 0x05", new byte[] { }, ValueRole.UINT32) },
            { 0x3B, new TagInfo(0x04, "Unknown 0x3B", new byte[] { 0x00 }, ValueRole.UINT32) },
        },
            angle: AngleProfile.Rewrite(0),
            options: ExtendedTagParser.Options .WithBitmapTags(0x02, 0x36),
            baseSection: BaseSectionProfile.SingleStream);
    }
}
