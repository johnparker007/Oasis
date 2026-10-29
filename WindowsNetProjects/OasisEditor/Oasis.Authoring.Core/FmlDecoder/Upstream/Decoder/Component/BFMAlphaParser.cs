using MfmeFmlDecoder.Decoder;
using MfmeFmlDecoder.Model;
using MfmeFmlDecoder.src.Model.Component;
using System.Collections.Generic;
using System;
using MfmeFmlDecoder.src.Decoder.Component.Core;
using MfmeFmlDecoder.src.Decoder.Component.Profiles;

namespace MfmeFmlDecoder.src.Decoder.Component
{
    internal sealed class BFMAlphaParser : ComponentParserBase<BFMAlpha>
    {

        public BFMAlpha Parse(ParsingContext context, long componentOffset, uint componentId, byte[] data)
        {
            ComponentProfile profile = ComponentProfileCatalog.Resolve(MFMEComponentType.BFMAlpha, context.Version);
            // Normalize for MFME angle bug
            var (component, offset, parseData) = ParseBase(context, profile, componentOffset, componentId, data);

            DumpRemaining(componentOffset, parseData, offset);

                        var parseResult = ParseExtendedTags(component, profile, parseData, offset);

            if (parseResult.ValuesByTag.TryGetValue(0x06, out object legacyReversed)
                && legacyReversed is bool reversed)
            {
                component.Booleans["Reversed"] = reversed;
            }

            return component;
        }
    }
}



