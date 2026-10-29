using MfmeFmlDecoder.Decoder;
using MfmeFmlDecoder.Model;
using MfmeFmlDecoder.src.Decoder.Component.Core;
using MfmeFmlDecoder.src.Decoder.Component.Profiles;
using MfmeFmlDecoder.src.Model.Component;
using System;
using System.Collections.Generic;

namespace MfmeFmlDecoder.src.Decoder.Component
{
    internal sealed class IGTVfdParser : ComponentParserBase<IGTVfd>
    {

        public IGTVfd Parse(ParsingContext context, long componentOffset, uint componentId, byte[] data)
        {
            ComponentProfile profile = ComponentProfileCatalog.Resolve(MFMEComponentType.IGTVfd, context.Version);
            // Normalize for MFME angle bug
            var (component, offset, parseData) = ParseBase(context, profile, componentOffset, componentId, data);

            DumpRemaining(componentOffset, parseData, offset);

                        var parseResult = ParseExtendedTags(component, profile, parseData, offset);

            return component;
        }
    }
}
