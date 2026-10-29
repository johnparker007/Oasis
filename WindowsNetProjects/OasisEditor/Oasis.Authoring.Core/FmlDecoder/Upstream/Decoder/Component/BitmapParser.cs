using MfmeFmlDecoder.Decoder;
using MfmeFmlDecoder.Model;
using MfmeFmlDecoder.src.Model.Component;
using System.Collections.Generic;
using System;
using MfmeFmlDecoder.src.Decoder.Component.Core;
using MfmeFmlDecoder.src.Decoder.Component.Profiles;

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


        public Bitmap Parse(ParsingContext context, long componentOffset, uint componentId, byte[] data)
        {
            ComponentProfile profile = ComponentProfileCatalog.Resolve(MFMEComponentType.Bitmap, context.Version);
            // Normalize for MFME angle bug
            var (component, offset, parseData) = ParseBase(context, profile, componentOffset, componentId, data);

            DumpRemaining(componentOffset, parseData, offset);

                        var parseResult = ParseExtendedTags(component, profile, parseData, offset);

            if (parseResult.ValuesByTag.TryGetValue(0x03, out var stretchMode)) component.Strings.Add("StretchMode", stretchModes[(uint)stretchMode]);

            return component;
        }
    }
}



