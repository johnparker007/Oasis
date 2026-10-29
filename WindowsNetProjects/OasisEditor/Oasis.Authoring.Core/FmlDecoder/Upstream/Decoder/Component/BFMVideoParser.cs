using MfmeFmlDecoder.Decoder;
using MfmeFmlDecoder.Model;
using MfmeFmlDecoder.src.Model.Component;
using System.Collections.Generic;
using System;
using MfmeFmlDecoder.src.Decoder.Component.Core;
using MfmeFmlDecoder.src.Decoder.Component.Profiles;

namespace MfmeFmlDecoder.src.Decoder.Component
{
    internal sealed class BFMVideoParser : ComponentParserBase<BFMVideo>
    {
        private Dictionary<uint, string> videoModes = new Dictionary<uint, string>
        {
            { 0x0, "600 x 800 V"},
            { 0x1, "480 x 640 V"},
            { 0x2, "800 x 600 H"},
            { 0x3, "640 x 480 H"},
        };


        public BFMVideo Parse(ParsingContext context, long componentOffset, uint componentId, byte[] data)
        {
            ComponentProfile profile = ComponentProfileCatalog.Resolve(MFMEComponentType.BFMVideo, context.Version);
            // Normalize for MFME angle bug
            var (component, offset, parseData) = ParseBase(context, profile, componentOffset, componentId, data);

            DumpRemaining(componentOffset, parseData, offset);

                        var parseResult = ParseExtendedTags(component, profile, parseData, offset);

            if (parseResult.ValuesByTag.TryGetValue(0x01, out var videoModeDropdownIndex)) component.Strings.Add("VideoMode", videoModes[(uint)videoModeDropdownIndex]);

            return component;
        }
    }
}



