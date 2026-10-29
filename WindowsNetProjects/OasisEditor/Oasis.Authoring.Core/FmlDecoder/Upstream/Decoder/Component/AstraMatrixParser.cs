using MfmeFmlDecoder.Decoder;
using MfmeFmlDecoder.Model;
using MfmeFmlDecoder.src.Decoder.Component.Core;
using MfmeFmlDecoder.src.Decoder.Component.Profiles;
using MfmeFmlDecoder.src.Model.Component;
using System;
using System.Collections.Generic;
using static MfmeFmlDecoder.src.Decoder.Component.Core.ExtendedTagParser;

namespace MfmeFmlDecoder.src.Decoder.Component
{
    internal sealed class AstraMatrixParser : ComponentParserBase<AstraMatrix>
    {

        public AstraMatrix Parse(ParsingContext context, long componentOffset, uint componentId, byte[] data)
        {
            ComponentProfile profile = ComponentProfileCatalog.Resolve(MFMEComponentType.AstraMatrix, context.Version);
            var (component, offset, parseData) = ParseBase(context, profile, componentOffset, componentId, data);

                        var parseResult = ParseExtendedTags(component, profile, parseData, offset);

            ApplyExtendedTags(component, parseResult, profile.Tags);

            return component;
        }

        private void ApplyExtendedTags(AstraMatrix component, ExtendedTagParser.ParseResult parseResult, ComponentTagMap componentTagMap)
        {

            ApplyExtendedTagsByReflection(component, parseResult, componentTagMap);
        }
    }
}
