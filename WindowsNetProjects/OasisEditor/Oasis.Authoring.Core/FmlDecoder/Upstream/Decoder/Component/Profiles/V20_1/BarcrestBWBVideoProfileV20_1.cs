using System.Collections.Generic;
using System;
using MfmeFmlDecoder.Decoder;
using MfmeFmlDecoder.Model;
using MfmeFmlDecoder.src.Decoder.Component.Core;

namespace MfmeFmlDecoder.src.Decoder.Component.Profiles.V20_1
{
    internal static class BarcrestBWBVideoProfileV20_1
    {
        public static readonly ComponentProfile Profile = ComponentProfileFactory.Create(
            MFMEComponentType.BarcrestBWBVideo,
            MfmeVersion.V20_1,
            tags: new ComponentTagMap
        {
            { 0x01, new TagInfo(0x01, "Unknown 0x01", new byte[] { }, ValueRole.BOOLEAN) },
            { 0x36, new TagInfo(0x00, "Overlay Image", Array.Empty<byte>(), ValueRole.BITMAP) },
            { 0x05, new TagInfo(0x04, "LeftSkew", Array.Empty<byte>(), ValueRole.UINT32) },
            { 0x06, new TagInfo(0x04, "RightSkew", Array.Empty<byte>(), ValueRole.INT32) }, // This can be negative
            { 0x3B, new TagInfo(0x04, "Unknown 0x3B", new byte[] { 0x00 }, ValueRole.UINT32) },
        },
            angle: AngleProfile.Rewrite(-4),
            options: ExtendedTagParser.Options.Default,
            baseSection: BaseSectionProfile.SingleStream);
    }
}
