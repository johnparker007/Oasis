using System.Collections.Generic;
using System;
using MfmeFmlDecoder.Decoder;
using MfmeFmlDecoder.Model;
using MfmeFmlDecoder.src.Decoder.Component.Core;

namespace MfmeFmlDecoder.src.Decoder.Component.Profiles.V20_1
{
    internal static class RGBLedProfileV20_1
    {
        public static readonly ComponentProfile Profile = ComponentProfileFactory.Create(
            MFMEComponentType.RGBLed,
            MfmeVersion.V20_1,
            tags: new ComponentTagMap
        {
            { 0x1C, new TagInfo(0x04, "Unknown 0x1C", new byte[] { 0x00 }, ValueRole.UINT32) },
            { 0x06, new TagInfo(0x01, "MaxLED", new byte[] { 0x01 }, ValueRole.BOOLEAN) },
            { 0x07, new TagInfo(0x01, "NoOutline", new byte[] { 0x00 }, ValueRole.BOOLEAN) },
            { 0x08, new TagInfo(0x01, "NoShadow", new byte[] { 0x00 }, ValueRole.BOOLEAN) },
            { 0x03, new TagInfo(0x04, "SelectedStyle", new byte[] { 0x00 }, ValueRole.UINT32) },

            { 0x02, new TagInfo(0x04, "AdjustedOff", new byte[] { 0x00, 0x00, 0x00, 0xFF }, ValueRole.ARGB_COLOR) },
            { 0x09, new TagInfo(0x04, "AdjustedRed", new byte[] { 0x00, 0x00, 0x00, 0xFF }, ValueRole.ARGB_COLOR) },
            { 0x0A, new TagInfo(0x04, "AdjustedGreen", new byte[] { 0x00, 0x00, 0x00, 0xFF }, ValueRole.ARGB_COLOR) },
            { 0x0B, new TagInfo(0x04, "AdjustedRedGreen", new byte[] { 0x00, 0x00, 0x00, 0xFF }, ValueRole.ARGB_COLOR) },
            { 0x0C, new TagInfo(0x04, "AdjustedBlue", new byte[] { 0x00, 0x00, 0x00, 0xFF }, ValueRole.ARGB_COLOR) },
            { 0x0D, new TagInfo(0x04, "AdjustedRedBlue", new byte[] { 0x00, 0x00, 0x00, 0xFF }, ValueRole.ARGB_COLOR) },
            { 0x0E, new TagInfo(0x04, "AdjustedRedGreen2", new byte[] { 0x00, 0x00, 0x00, 0xFF }, ValueRole.ARGB_COLOR) },
            { 0x0F, new TagInfo(0x04, "AdjustedRedGreenBlue", new byte[] { 0x00, 0x00, 0x00, 0xFF }, ValueRole.ARGB_COLOR) },

            { 0x38, new TagInfo(0x04, "SublampCount", new byte[] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.SUBLAMP_COUNT) },
            { 0x39, new TagInfo(0x00, "LampSublampTable", Array.Empty<byte>(), ValueRole.LAMP_SUBLAMP_TABLE) },

            { 0x36, new TagInfo(0x00, "Overlay Image", Array.Empty<byte>(), ValueRole.BITMAP) },
            { 0x3B, new TagInfo(0x04, "Unknown 0x3B", new byte[] { 0x00 }, ValueRole.UINT32) },
        },
            angle: AngleProfile.Rewrite(0),
            options: ExtendedTagParser.Options.Default,
            baseSection: BaseSectionProfile.SingleStream);
    }
}
