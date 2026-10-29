using System.Collections.Generic;
using System;
using MfmeFmlDecoder.Decoder;
using MfmeFmlDecoder.Model;
using MfmeFmlDecoder.src.Decoder.Component.Core;

namespace MfmeFmlDecoder.src.Decoder.Component.Profiles.V20_1
{
    internal static class ReelBonusReelProfileV20_1
    {
        public static readonly ComponentProfile Profile = ComponentProfileFactory.Create(
            MFMEComponentType.ReelBonusReel,
            MfmeVersion.V20_1,
            tags: new ComponentTagMap
        {
            { 0x1C, new TagInfo(0x04, "NumberOfDefinedLampNumbers", new byte[] { }, ValueRole.UINT32) },
            { 0x38, new TagInfo(0x04, "SublampCount", new byte[] { }, ValueRole.SUBLAMP_COUNT) },
            { 0x39, new TagInfo(0x04, "SublampTable", new byte[] { }, ValueRole.LAMP_SUBLAMP_TABLE) },
            { 0x02, new TagInfo(0x04, "SymbolPos", new byte[] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x03, new TagInfo(0x00, "Lamp 1 On Image", new byte[] { }, ValueRole.BITMAP) },
            { 0x04, new TagInfo(0x00, "Lamp 2 On Image", new byte[] { }, ValueRole.BITMAP) },
            { 0x05, new TagInfo(0x00, "Lamp 3 On Image", new byte[] { }, ValueRole.BITMAP) },
            { 0x06, new TagInfo(0x00, "Lamp 4 On Image", new byte[] { }, ValueRole.BITMAP) },
            { 0x07, new TagInfo(0x00, "Mask Image", new byte[] { 0x00, 0xFF, 0xFF, 0xFF }, ValueRole.BITMAP) },
            { 0x08, new TagInfo(0x00, "Background Image", new byte[] { }, ValueRole.BITMAP) },
            { 0x36, new TagInfo(0x00, "Overlay Image", new byte[] { }, ValueRole.BITMAP) },
            { 0x3B, new TagInfo(0x04, "Unknown 0x3B", new byte[] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
        },
            angle: AngleProfile.Rewrite(4),
            options: ExtendedTagParser.Options.WithBitmapTags(0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x36),
            baseSection: BaseSectionProfile.SingleStream);
    }
}
