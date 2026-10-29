using System.Collections.Generic;
using System;
using MfmeFmlDecoder.Decoder;
using MfmeFmlDecoder.Model;
using MfmeFmlDecoder.src.Decoder.Component.Core;

namespace MfmeFmlDecoder.src.Decoder.Component.Profiles.V20_1
{
    internal static class EpochDotAlphaProfileV20_1
    {
        public static readonly ComponentProfile Profile = ComponentProfileFactory.Create(
            MFMEComponentType.EpochDotAlpha,
            MfmeVersion.V20_1,
            tags: new ComponentTagMap
        {
            { 0x05, new TagInfo(0x04, "OnColour", new byte[] { 0x00, 0x00, 0xFF, 0xFF }, ValueRole.ARGB_COLOR) },
            { 0x06, new TagInfo(0x04, "OffColour", new byte[] { 0x00, 0x00, 0x00, 0xFF }, ValueRole.ARGB_COLOR) },
            { 0x07, new TagInfo(0x04, "BackgroundColour", new byte[] { 0x00, 0x00, 0x00, 0xFF }, ValueRole.ARGB_COLOR) },
            { 0x01, new TagInfo(0x04, "XSize", new byte[] { 0x02 }, ValueRole.UINT32) },
            { 0x02, new TagInfo(0x04, "YSize", new byte[] { 0x02 }, ValueRole.UINT32) },
            { 0x03, new TagInfo(0x04, "Unknown 0x03", new byte[] { 0x01 }, ValueRole.UINT32) },
            { 0x09, new TagInfo(0x04, "Unknown 0x09", new byte[] { 0x01 }, ValueRole.UINT32) },
            { 0x08, new TagInfo(0x04, "DotSpacing", new byte[] { 0x01 }, ValueRole.UINT32) },
            { 0x04, new TagInfo(0x04, "DigitSpacing", new byte[] { 0x02 }, ValueRole.UINT32) },
            { 0x36, new TagInfo(0x00, "Overlay Image", new byte[] { }, ValueRole.BITMAP) },
            { 0x3B, new TagInfo(0x04, "Unknown 0x3B", new byte[] { 0x00 }, ValueRole.UINT32) },
        },
            angle: AngleProfile.Rewrite(0),
            options: ExtendedTagParser.Options .WithBitmapTags(0x36),
            baseSection: BaseSectionProfile.SingleStream);
    }
}
