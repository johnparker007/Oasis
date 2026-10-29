using System.Collections.Generic;
using System;
using MfmeFmlDecoder.Decoder;
using MfmeFmlDecoder.Model;
using MfmeFmlDecoder.src.Decoder.Component.Core;

namespace MfmeFmlDecoder.src.Decoder.Component.Profiles.V20_1
{
    internal static class MatrixAlphaProfileV20_1
    {
        public static readonly ComponentProfile Profile = ComponentProfileFactory.Create(
            MFMEComponentType.MatrixAlpha,
            MfmeVersion.V20_1,
            tags: new ComponentTagMap
        {
            { 0x04, new TagInfo(0x04, "OnColour", new byte[] { 0xFF, 0xFF, 0x00, 0xFF }, ValueRole.ARGB_COLOR) },
            { 0x05, new TagInfo(0x04, "OffColour", new byte[] { 0x2C, 0x2C, 0x2C, 0xFF}, ValueRole.ARGB_COLOR) },
            { 0x06, new TagInfo(0x04, "BackgroundColour", new byte[] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.ARGB_COLOR) },
            { 0x01, new TagInfo(0x04, "XSize", new byte[] { 0x02 }, ValueRole.UINT32) },
            { 0x02, new TagInfo(0x04, "YSize", new byte[] { 0x02 }, ValueRole.UINT32) },
            { 0x07, new TagInfo(0x04, "DotSpacing", new byte[] { 0x01 }, ValueRole.UINT32) },
            { 0x03, new TagInfo(0x04, "Unknown 0x03", new byte[] { 0x01 }, ValueRole.UINT32) },
            { 0x08, new TagInfo(0x04, "Unknown 0x08", new byte[] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x0A, new TagInfo(0x01, "Unknown 0x0A", new byte[] { 0x00 }, ValueRole.BOOLEAN) },

            { 0x36, new TagInfo(0x04, "Overlay Image", new byte[] { }, ValueRole.BITMAP) },
            { 0x3B, new TagInfo(0x04, "Unknown 0x3B", new byte[] { 0x00 }, ValueRole.UINT32) },
        },
            angle: AngleProfile.Rewrite(0),
            options: ExtendedTagParser.Options.WithBitmapTags(0x36),
            baseSection: BaseSectionProfile.SingleStream);
    }
}
