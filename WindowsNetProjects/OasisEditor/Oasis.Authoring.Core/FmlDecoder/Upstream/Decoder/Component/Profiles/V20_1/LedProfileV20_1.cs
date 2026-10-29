using System.Collections.Generic;
using System;
using MfmeFmlDecoder.Decoder;
using MfmeFmlDecoder.Model;
using MfmeFmlDecoder.src.Decoder.Component.Core;

namespace MfmeFmlDecoder.src.Decoder.Component.Profiles.V20_1
{
    internal static class LedProfileV20_1
    {
        public static readonly ComponentProfile Profile = ComponentProfileFactory.Create(
            MFMEComponentType.Led,
            MfmeVersion.V20_1,
            tags: new ComponentTagMap
        {
            { 0x1C, new TagInfo(0x04, "Unknown 0x1C", new byte[] { }, ValueRole.UINT32) },
            { 0x38, new TagInfo(0x04, "Unknown 0x38", new byte[] { }, ValueRole.UINT32) },
            { 0x39, new TagInfo(0x04, "Unknown 0x39", new byte[] { }, ValueRole.UINT32) },
            { 0x3B, new TagInfo(0x04, "Unknown 0x3B", new byte[] { }, ValueRole.UINT32) },
            { 0x01, new TagInfo(0x04, "OnColour", new byte[] { }, ValueRole.ARGB_COLOR) },
            { 0x02, new TagInfo(0x04, "OffColour", new byte[] { }, ValueRole.ARGB_COLOR) },
            { 0x03, new TagInfo(0x04, "SelectedStyleIndex", new byte[] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x04, new TagInfo(0x04, "SelectedSegmentIndex", new byte[] { 0xFF, 0xFF, 0xFF, 0xFF }, ValueRole.INT32) },
            { 0x05, new TagInfo(0x01, "Led", new byte[] { 0x00 }, ValueRole.BOOLEAN) },
            { 0x06, new TagInfo(0x01, "NoOutline", new byte[] { 0x00 }, ValueRole.BOOLEAN) },
            { 0x07, new TagInfo(0x01, "NoShadow", new byte[] { 0x00 }, ValueRole.BOOLEAN) },
            { 0x36, new TagInfo(0x00, "Overlay Image", Array.Empty<byte>(), ValueRole.BITMAP) },
        },
            angle: AngleProfile.Rewrite(5),
            options: ExtendedTagParser.Options .WithBitmapTags(0x36),
            baseSection: BaseSectionProfile.SingleStream);
    }
}
