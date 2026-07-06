using MfmeFmlDecoder.Model;
using MfmeFmlDecoder.src.Model.Component;
using System.Collections.Generic;
using System;
using MfmeFmlDecoder.src.Decoder.Component.Core;

namespace MfmeFmlDecoder.src.Decoder.Component
{
    internal sealed class BitmapParser : ComponentParserBase<Bitmap>
    {
        private Dictionary<uint, string> stretchModes = new Dictionary<uint, string>
        {
            { 0x0, "Nearest"},
            { 0x1, "Draft"},
            { 0x2, "Linear"},
            { 0x3, "Cosine"},
            { 0x4, "Spline"},
            { 0x5, "Lanczos"},
            { 0x6, "Mirchell"},
        };

        private ComponentTagMap componentTagMap = new ComponentTagMap
        {
            { 0x01, new TagInfo(0x01, "Transparent", new byte[] { 0x00 }, ValueRole.BOOLEAN) },
            { 0x02, new TagInfo(0x00, "Image", new byte[] { }, ValueRole.BITMAP) },
            { 0x03, new TagInfo(0x04, "StretchMode Index", new byte[] { 0x00 }, ValueRole.UINT32) },
            { 0x36, new TagInfo(0x00, "Overlay Image", new byte[] { }, ValueRole.BITMAP) },
            { 0x33, new TagInfo(0x01, "Unknown 0x33", new byte[] { }, ValueRole.BOOLEAN) },
            { 0x05, new TagInfo(0x04, "Unknown 0x05", new byte[] { }, ValueRole.UINT32) },
            { 0x3B, new TagInfo(0x04, "Unknown 0x3B", new byte[] { 0x00 }, ValueRole.UINT32) },
        };

        public Bitmap Parse(long componentOffset, uint componentId, byte[] data)
        {
            // Normalize for MFME angle bug
            var (component, offset, parseData) = ParseBase(
                componentOffset,
                componentId,
                data,
                normalizationRule: new GeometryAngleNormalization.Rule(
                    RewriteTriggerOffsetDelta: 0,
                    ValidAngleOffsetDelta: 0));

            DumpRemaining(componentOffset, parseData, offset);

            var parseOptions = WithComponentTagLogging(
                ExtendedTagParser.Options
                .WithBitmapTags(0x02, 0x36)
            );
            var parseResult = ParseExtendedTags(component, componentTagMap, parseData, offset, parseOptions);

            if (parseResult.ValuesByTag.TryGetValue(0x03, out var stretchMode)) component.Strings.Add("StretchMode", stretchModes[(uint)stretchMode]);

            return component;
        }
    }
}



