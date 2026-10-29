using System.Collections.Generic;
using System;
using MfmeFmlDecoder.Decoder;
using MfmeFmlDecoder.Model;
using MfmeFmlDecoder.src.Decoder.Component.Core;

namespace MfmeFmlDecoder.src.Decoder.Component.Profiles.V20_1
{
    internal static class MaygayVideoProfileV20_1
    {
        public static readonly ComponentProfile Profile = ComponentProfileFactory.Create(
            MFMEComponentType.MaygayVideo,
            MfmeVersion.V20_1,
            tags: new ComponentTagMap
        {
            { 0x01, new TagInfo(0x04, "QualitySelectedValue", new byte[] { 0x00 }, ValueRole.UINT32) },
            { 0x02, new TagInfo(0x01, "Vertical", new byte[] { 0x00 }, ValueRole.BOOLEAN) },
            { 0x36, new TagInfo(0x00, "Overlay Image", Array.Empty<byte>(), ValueRole.BITMAP) },
            { 0x3B, new TagInfo(0x04, "Unknown 0x3B", new byte[] { 0x00 }, ValueRole.UINT32) },
        },
            angle: AngleProfile.Rewrite(-4),
            options: ExtendedTagParser.Options.Default,
            baseSection: BaseSectionProfile.SingleStream);
    }
}
