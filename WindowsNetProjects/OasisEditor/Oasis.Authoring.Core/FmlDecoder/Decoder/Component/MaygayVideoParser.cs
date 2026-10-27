using MfmeFmlDecoder.Model;
using MfmeFmlDecoder.src.Decoder.Component.Core;
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

        private ComponentTagMap componentTagMap = new ComponentTagMap
        {
            { 0x01, new TagInfo(0x04, "QualitySelectedValue", new byte[] { 0x00 }, ValueRole.UINT32) },
            { 0x02, new TagInfo(0x01, "Vertical", new byte[] { 0x00 }, ValueRole.BOOLEAN) },
            { 0x36, new TagInfo(0x00, "Overlay Image", Array.Empty<byte>(), ValueRole.BITMAP) },
            { 0x3B, new TagInfo(0x04, "Unknown 0x3B", new byte[] { 0x00 }, ValueRole.UINT32) },
        };

        public MaygayVideo Parse(long componentOffset, uint componentId, byte[] data)
        {
            // Normalize for MFME angle bug
            var (component, offset, parseData) = ParseBase(
                componentOffset,
                componentId,
                data,
                normalizationRule: new GeometryAngleNormalization.Rule(
                    RewriteTriggerOffsetDelta: -4,
                    ValidAngleOffsetDelta: 0));

            DumpRemaining(componentOffset, data, offset);

            var parseOptions = WithComponentTagLogging(ExtendedTagParser.Options.Default);
            var parseResult = ParseExtendedTags(component, componentTagMap, data, offset, parseOptions);

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
