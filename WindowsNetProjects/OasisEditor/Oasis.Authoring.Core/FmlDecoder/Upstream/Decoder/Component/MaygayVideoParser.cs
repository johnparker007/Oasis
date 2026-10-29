using MfmeFmlDecoder.Decoder;
using MfmeFmlDecoder.Model;
using MfmeFmlDecoder.src.Decoder.Component.Core;
using MfmeFmlDecoder.src.Decoder.Component.Profiles;
using MfmeFmlDecoder.src.Model.Component;
using System;
using System.Collections.Generic;

namespace MfmeFmlDecoder.src.Decoder.Component
{
    internal sealed class MaygayVideoParser : ComponentParserBase<MaygayVideo>
    {
        private Dictionary<uint, string> qualityOptions = new Dictionary<uint, string>
        {
            { 0x0, "Normal"},
            { 0x1, "Better"},
            { 0x2, "Best"},
        };


        public MaygayVideo Parse(ParsingContext context, long componentOffset, uint componentId, byte[] data)
        {
            ComponentProfile profile = ComponentProfileCatalog.Resolve(MFMEComponentType.MaygayVideo, context.Version);
            // Normalize for MFME angle bug
            var (component, offset, parseData) = ParseBase(context, profile, componentOffset, componentId, data);

            byte[] extendedData = profile.Defaults?.UseNormalizedExtendedData == true ? parseData : data;
            DumpRemaining(componentOffset, extendedData, offset);

                        var parseResult = ParseExtendedTags(component, profile, extendedData, offset);

            ApplyExtendedTags(component, parseResult.ValuesByTag, qualityOptions);

            return component;
        }

        private static void ApplyExtendedTags(MaygayVideo component, System.Collections.Generic.IReadOnlyDictionary<uint, object> valuesByTag, Dictionary<uint, string> qualityOptions)
        {
            if (valuesByTag.TryGetValue(0x01, out var selectedQuality)) component.Strings.Add("Quality", qualityOptions[(uint)selectedQuality]);
            //if (valuesByTag.TryGetValue(0x01, out var vertical)) component.Booleans.Add("Vertical", (uint)vertical > 0);
        }
    }
}
