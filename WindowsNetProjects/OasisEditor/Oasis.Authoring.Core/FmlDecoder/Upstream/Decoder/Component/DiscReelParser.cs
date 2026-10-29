using MfmeFmlDecoder.Decoder;
using MfmeFmlDecoder.Model;
using MfmeFmlDecoder.src.Decoder.Component.Core;
using MfmeFmlDecoder.src.Decoder.Component.Profiles;
using MfmeFmlDecoder.src.Model.Component;
using System;
using System.Collections.Generic;
using System.Xml.Linq;

namespace MfmeFmlDecoder.src.Decoder.Component
{
    internal sealed class DiscReelParser : ComponentParserBase<DiscReel>
    {

        public DiscReel Parse(ParsingContext context, long componentOffset, uint componentId, byte[] data)
        {
            ComponentProfile profile = ComponentProfileCatalog.Resolve(MFMEComponentType.DiscReel, context.Version);
            // Normalize for MFME angle bug
            var (component, offset, parseData) = ParseBase(context, profile, componentOffset, componentId, data);

            DumpRemaining(componentOffset, parseData, offset);


            var parseResult = ParseExtendedTags(component, profile, parseData, offset);

            ApplyExtendedTags(component, parseResult, profile.Tags);

            if (profile.Defaults?.DiscReelLampCountFromStops == true
                && parseResult.ExplicitTags?.Contains(0x98) != true
                && parseResult.BaseSectionExplicitTags?.Contains(0x98) != true
                && component.UInt32s.TryGetValue("Stops", out uint stops))
            {
                component.UInt32s["NumberOfLamps"] = stops;
            }

            return component;
        }

        private void ApplyExtendedTags(DiscReel component, ExtendedTagParser.ParseResult parseResult, ComponentTagMap componentTagMap)
        {
            ApplyExtendedTagsByReflection(component, parseResult, componentTagMap);

            if (parseResult.ValuesByTag.TryGetValue(0x39, out var sublampTableValue)
                && sublampTableValue is IReadOnlyList<LampSublampTableEntry> sublampTable)
            {
                component.SublampTable = sublampTable;
            }

            bool hasExplicit3C = parseResult.BaseSectionExplicitTags?.Contains(0x3C) == true
                || parseResult.ExplicitTags?.Contains(0x3C) == true;
            bool hasExplicit04 = parseResult.ExplicitTags?.Contains(0x04) == true;
            if (hasExplicit04
                && parseResult.ValuesByTag.TryGetValue(0x04, out object legacyInverted)
                && legacyInverted is bool inverted
                && !hasExplicit3C)
            {
                component.Booleans["Inverted"] = inverted;
            }

            bool hasExplicit12 = parseResult.BaseSectionExplicitTags?.Contains(0x12) == true
                || parseResult.ExplicitTags?.Contains(0x12) == true;
            if (hasExplicit12
                && parseResult.ValuesByTag.TryGetValue(0x12, out object legacyReversed)
                && legacyReversed is bool reversed
                && reversed
                && !hasExplicit3C)
            {
                component.Booleans["Reversed"] = true;
            }
        }
    }
}
