using MfmeFmlDecoder.Decoder;
using MfmeFmlDecoder.Model;
using MfmeFmlDecoder.src.Model.Component;
using System.Collections.Generic;
using System;
using MfmeFmlDecoder.src.Decoder.Component.Core;
using MfmeFmlDecoder.src.Decoder.Component.Profiles;

namespace MfmeFmlDecoder.src.Decoder.Component
{
    internal sealed class BFMLEDParser : ComponentParserBase<BFMLED>
    {

        public BFMLED Parse(ParsingContext context, long componentOffset, uint componentId, byte[] data)
        {
            ComponentProfile profile = ComponentProfileCatalog.Resolve(MFMEComponentType.BFMLED, context.Version);
            // Normalize for MFME angle bug
            var (component, offset, parseData) = ParseBase(context, profile, componentOffset, componentId, data);

            DumpRemaining(componentOffset, parseData, offset);

                        var parseResult = ParseExtendedTags(component, profile, parseData, offset);

            if (profile.Defaults?.BfmLedSizeFromXSize == true
                && parseResult.ExplicitTags?.Contains(0x07) != true
                && parseResult.BaseSectionExplicitTags?.Contains(0x07) != true
                && component.UInt32s.TryGetValue("XSize", out uint xSize))
            {
                component.UInt32s["LedSize"] = (xSize >= 5u ? xSize : 7u);
            }

            return component;
        }
    }
}



