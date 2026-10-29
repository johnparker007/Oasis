using MfmeFmlDecoder.Decoder;
using MfmeFmlDecoder.Model;
using MfmeFmlDecoder.src.Decoder.Component.Core;
using MfmeFmlDecoder.src.Decoder.Component.Profiles;
using MfmeFmlDecoder.src.Model.Component;
using System;
using System.Collections.Generic;

namespace MfmeFmlDecoder.src.Decoder.Component
{
    internal sealed class DotMatrixParser : ComponentParserBase<DotMatrix>
    {

        public DotMatrix Parse(ParsingContext context, long componentOffset, uint componentId, byte[] data)
        {
            ComponentProfile profile = ComponentProfileCatalog.Resolve(MFMEComponentType.DotMatrix, context.Version);
            // Normalize for MFME angle bug
            var (component, offset, parseData) = ParseBase(context, profile, componentOffset, componentId, data);

            DumpRemaining(componentOffset, parseData, offset);

                        var parseResult = ParseExtendedTags(component, profile, parseData, offset);

            if (profile.Defaults?.DotMatrixAutoSize == true)
            {
                uint size = component.UInt32s.TryGetValue("Size", out uint value) ? value : 7;
                component.Width = size * 0x41 + 1;
                component.Height = size * 0x15 + 1;
            }

            return component;
        }
    }
}
