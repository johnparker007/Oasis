using System.Collections.Generic;
using System;
using MfmeFmlDecoder.Decoder;
using MfmeFmlDecoder.Model;
using MfmeFmlDecoder.src.Decoder.Component.Core;

namespace MfmeFmlDecoder.src.Decoder.Component.Profiles.V20_1
{
    internal static class BorderProfileV20_1
    {
        public static readonly ComponentProfile Profile = ComponentProfileFactory.Create(
            MFMEComponentType.Border,
            MfmeVersion.V20_1,
            tags: new ComponentTagMap
        {
            { 0x0A, new TagInfo(0x01, "Unknown 0x0A", new byte[] { 0x00 }, ValueRole.BOOLEAN) },
            { 0x0B, new TagInfo(0x01, "Unknown 0x0B", new byte[] { 0x00 }, ValueRole.BOOLEAN) },
            { 0x37, new TagInfo(0x04, "Unknown 0x37", new byte[] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x36, new TagInfo(0x00, "Overlay Image", Array.Empty<byte>(), ValueRole.BITMAP) },
            { 0x3B, new TagInfo(0x04, "Unknown 0x3B", new byte[] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x3F, new TagInfo(0x00, "Label", Array.Empty<byte>(), ValueRole.TEXT) },
        }.WithNestedTagBlock(new ComponentTagMap
        {
            { 0x01, new TagInfo(0x04, "OuterColour", new byte[] { 0x00, 0x00, 0xFF, 0xFF }, ValueRole.ARGB_COLOR) },
            { 0x02, new TagInfo(0x04, "BorderWidth", new byte[] { 0x08, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x03, new TagInfo(0x00, "Border Image", Array.Empty<byte>(), ValueRole.BITMAP) },
            { 0x04, new TagInfo(0x04, "Unknown 0x04", new byte[] { 0x00, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
            { 0x05, new TagInfo(0x04, "InnerColour", new byte[] { 0x00, 0xFF, 0x00, 0xFF }, ValueRole.ARGB_COLOR) },
            // Four LE uint32s: BoxX, BoxY (always 0), BoxWidth, BoxHeight.
            { 0x06, new TagInfo(0x10, "BoundingBox", Array.Empty<byte>(), ValueRole.RAW) },
            // Four LE uint32s: LeftWidth, TopHeight, RightWidth, BottomHeight (2×2 pane split).
            { 0x07, new TagInfo(0x10, "PaneSplit", Array.Empty<byte>(), ValueRole.RAW) },
            { 0x08, new TagInfo(0x04, "Spacing", new byte[] { 0x09, 0x00, 0x00, 0x00 }, ValueRole.UINT32) },
        }),
            angle: AngleProfile.Rewrite(0),
            options: ExtendedTagParser.Options.WithBitmapTags(0x03, 0x36),
            baseSection: BaseSectionProfile.SingleStream);
    }
}
