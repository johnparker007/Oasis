using System.Collections.Generic;
using System;
using MfmeFmlDecoder.Decoder;
using MfmeFmlDecoder.Model;
using MfmeFmlDecoder.src.Decoder.Component.Core;

namespace MfmeFmlDecoder.src.Decoder.Component.Profiles.V20_1
{
    internal static class LabelProfileV20_1
    {
        public static readonly ComponentProfile Profile = ComponentProfileFactory.Create(
            MFMEComponentType.Label,
            MfmeVersion.V20_1,
            tags: new ComponentTagMap
        {
            { 0x3B, new TagInfo(0x04, "Unknown 0x3B", new byte[] { }, ValueRole.UINT32) },
            { 0x3F, new TagInfo(0x00, "Label", new byte[] { }, ValueRole.TEXT) },
            { 0x36, new TagInfo(0x00, "Overlay Image", new byte[] { }, ValueRole.BITMAP) },
            { 0x39, new TagInfo(0x04, "Lamp", new byte[] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.INT32) },
            { 0x01, new TagInfo(0x04, "BackgroundColour", new byte[] { 0xF0, 0xF0, 0xF0, 0xFF }, ValueRole.ARGB_COLOR) },
            { 0x02, new TagInfo(0x01, "Unknown 0x02", new byte[] { 0x00 }, ValueRole.BOOLEAN) },
            { 0x27, new TagInfo(0x00, "PrimaryFont", Array.Empty<byte>(), ValueRole.FONT) },
            { 0x38, new TagInfo(0x04, "Unknown 0x38", new byte[] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x1C, new TagInfo(0x04, "Unknown 0x1C", new byte[] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
        },
            angle: AngleProfile.RewriteWithBytes(new byte[] { 0x00, 0x01 }, 0),
            options: ExtendedTagParser.Options.Default,
            baseSection: BaseSectionProfile.SingleStream);
    }
}
