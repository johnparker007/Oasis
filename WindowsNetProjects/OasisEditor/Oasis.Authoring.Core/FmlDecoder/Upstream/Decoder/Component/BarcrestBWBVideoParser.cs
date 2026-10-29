using MfmeFmlDecoder.Decoder;
using MfmeFmlDecoder.Model;
using MfmeFmlDecoder.src.Decoder.Component.Core;
using MfmeFmlDecoder.src.Decoder.Component.Profiles;
using MfmeFmlDecoder.src.Model.Component;
using System;

namespace MfmeFmlDecoder.src.Decoder.Component
{
    internal sealed class BarcrestBWBVideoParser : ComponentParserBase<BarcrestBWBVideo>
    {

        public BarcrestBWBVideo Parse(ParsingContext context, long componentOffset, uint componentId, byte[] data)
        {
            ComponentProfile profile = ComponentProfileCatalog.Resolve(MFMEComponentType.BarcrestBWBVideo, context.Version);
            // Normalize for MFME angle bug
            var (component, offset, parseData) = ParseBase(context, profile, componentOffset, componentId, data);

            byte[] extendedData = profile.Defaults?.UseNormalizedExtendedData == true ? parseData : data;
            DumpRemaining(componentOffset, extendedData, offset);

                        var parseResult = ParseExtendedTags(component, profile, extendedData, offset);

            if (profile.Defaults?.LegacyScaledSkew == true)
            {
                ApplyLegacySkew(component, "LegacyLeftSkew", "LeftSkew");
                ApplyLegacySkew(component, "LegacyRightSkew", "RightSkew");
                RemoveZero(component, "LeftSkew");
                RemoveZero(component, "RightSkew");
            }

            return component;
        }

        private static void RemoveZero(BarcrestBWBVideo component, string key)
        {
            if (component.Int32s.TryGetValue(key, out int value) && value == 0)
            {
                component.Int32s.Remove(key);
            }
        }

        private static void ApplyLegacySkew(BarcrestBWBVideo component, string legacyKey, string key)
        {
            if (!component.Int32s.TryGetValue(legacyKey, out int value))
            {
                return;
            }

            component.Int32s.Remove(legacyKey);
            component.Int32s[key] = component.Width == 0
                ? value
                : (int)((long)value * 0x1F8 / component.Width);
        }
    }
}
