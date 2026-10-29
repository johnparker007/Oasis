using MfmeFmlDecoder.Decoder;
using MfmeFmlDecoder.Model;
using MfmeFmlDecoder.src.Decoder.Component.Core;
using MfmeFmlDecoder.src.Decoder.Component.Profiles;
using MfmeFmlDecoder.src.Model.Component;
using System;
using System.Collections.Generic;

namespace MfmeFmlDecoder.src.Decoder.Component
{
    internal sealed class DotAlphaParser : ComponentParserBase<DotAlpha>
    {

        public DotAlpha Parse(ParsingContext context, long componentOffset, uint componentId, byte[] data)
        {
            ComponentProfile profile = ComponentProfileCatalog.Resolve(MFMEComponentType.DotAlpha, context.Version);
            // Normalize for MFME angle bug
            var (component, offset, parseData) = ParseBase(context, profile, componentOffset, componentId, data);

            DumpRemaining(componentOffset, parseData, offset);

                        var parseResult = ParseExtendedTags(component, profile, parseData, offset);

            if (profile.Defaults?.DotAlphaAutoSize == true)
            {
                uint xSize = GetUInt32(component, "XSize", 2);
                uint ySize = GetUInt32(component, "YSize", 2);
                uint digitSpacing = GetUInt32(component, "DigitSpacing", 2);
                uint dotSpacing = GetUInt32(component, "DotSpacing", 1);
                component.Width = (uint)(((xSize + dotSpacing) * 6 + digitSpacing) * 16 + 3);
                component.Height = (uint)((ySize + dotSpacing) * 7 + 6);
            }

            return component;
        }

        private static uint GetUInt32(DotAlpha component, string key, uint fallback)
        {
            return component.UInt32s.TryGetValue(key, out uint value) ? value : fallback;
        }
    }
}
