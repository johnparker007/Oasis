using System.Collections.Generic;
using System;
using MfmeFmlDecoder.Decoder;
using MfmeFmlDecoder.Model;
using MfmeFmlDecoder.src.Decoder.Component.Core;

namespace MfmeFmlDecoder.src.Decoder.Component.Profiles.V20_1
{
    internal static class PrismLampProfileV20_1
    {
        public static readonly ComponentProfile Profile = ComponentProfileFactory.Create(
            MFMEComponentType.PrismLamp,
            MfmeVersion.V20_1,
            tags: new ComponentTagMap
        {
            { 0x01, new TagInfo(0x00, "Lamp 1 Image", Array.Empty<byte>(), ValueRole.BITMAP) },
            { 0x02, new TagInfo(0x00, "Lamp 2 Image", Array.Empty<byte>(), ValueRole.BITMAP) },
            { 0x04, new TagInfo(0x00, "Lamp 1 Mask Image", Array.Empty<byte>(), ValueRole.BITMAP) },
            { 0x05, new TagInfo(0x00, "Lamp 2 Mask Image", Array.Empty<byte>(), ValueRole.BITMAP) },
            { 0x03, new TagInfo(0x00, "Off Image", Array.Empty<byte>(), ValueRole.BITMAP) },
            { 0x36, new TagInfo(0x00, "Overlay Image", Array.Empty<byte>(), ValueRole.BITMAP) },
            { 0x3B, new TagInfo(0x04, "Unknown3B", new byte[] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x1C, new TagInfo(0x04, "NumberOfDefinedLampNumbers", new byte[] { }, ValueRole.UINT32) },
            { 0x38, new TagInfo(0x04, "SublampCount", new byte[] { }, ValueRole.SUBLAMP_COUNT) },
            { 0x39, new TagInfo(0x04, "SublampTable", new byte[] { }, ValueRole.LAMP_SUBLAMP_TABLE) },
            { 0x06, new TagInfo(0x04, "HorizontalSpacing", new byte[] { 0x02, 0x00, 0x00, 0x00  }, ValueRole.UINT32) },
            { 0x0B, new TagInfo(0x04, "VerticalSpacing", new byte[] { 0x02, 0x00, 0x00, 0x00  }, ValueRole.UINT32) },
            { 0x09, new TagInfo(0x04, "Tilt", new byte[] { 0x02, 0x00, 0x00, 0x00  }, ValueRole.UINT32) },
            { 0x07, new TagInfo(0x04, "Style", new byte[] { 0x01, 0x00, 0x00, 0x00  }, ValueRole.BOOLEAN) },
            { 0x4C, new TagInfo(0x02, "IsHorizontal", new byte[] { 0x01, 0x00  }, ValueRole.BOOLEAN) },
            { 0x0A, new TagInfo(0x01, "CenterLine", new byte[] { 0x01 }, ValueRole.BOOLEAN) },

        },
            angle: AngleProfile.Rewrite(0),
            options: ExtendedTagParser.Options.WithBitmapTags(0x01, 0x02, 0x03, 0x04, 0x05, 0x36),
            baseSection: BaseSectionProfile.SingleStream);
    }
}
