using MfmeFmlDecoder.Decoder;
using MfmeFmlDecoder.Model;
using MfmeFmlDecoder.src.Decoder.Component.Core;
using MfmeFmlDecoder.src.Decoder.Component.Profiles;
using MfmeFmlDecoder.src.Model.Component;
using MfmeFmlDecoder.Utilities;
using System;
using System.Collections.Generic;

namespace MfmeFmlDecoder.src.Decoder.Component
{
    internal sealed class AlphaParser : ComponentParserBase<Alpha>
    {
        /// <summary>
        /// Synthetic tag for the merged Alpha character bitmap exposed under Images["alpha_character_bitmap"].
        /// </summary>
        private const uint AlphaCharacterBitmapSyntheticTag = 0xACBC01u;



        public Alpha Parse(ParsingContext context, long componentOffset, uint componentId, byte[] data)
        {
            ComponentProfile profile = ComponentProfileCatalog.Resolve(MFMEComponentType.Alpha, context.Version);
            var (component, offset, parseData) = ParseBase(context, profile, componentOffset, componentId, data);

                        var parseResult = ParseExtendedTags(component, profile, parseData, offset);

            ApplyExtendedTags(component, parseResult, profile.Tags);

            return component;
        }

        private void ApplyExtendedTags(Alpha component, ExtendedTagParser.ParseResult parseResult, ComponentTagMap componentTagMap)
        {

            ApplyExtendedTagsByReflection(component, parseResult, componentTagMap);

            if (parseResult.ValuesByTag.TryGetValue(0x05, out object legacyReversed)
                && legacyReversed is bool reversed)
            {
                component.Booleans["Reversed"] = reversed;
            }
        }
    }
}
