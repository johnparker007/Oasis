using MfmeFmlDecoder.Decoder;
using MfmeFmlDecoder.Model;
using MfmeFmlDecoder.src.Decoder.Component.Core;
using MfmeFmlDecoder.src.Decoder.Component.Profiles;
using MfmeFmlDecoder.src.Model.Component;
using System;
using System.Collections.Generic;

namespace MfmeFmlDecoder.src.Decoder.Component
{
    internal sealed class FlipReelParser : ComponentParserBase<FlipReel>
    {


        public FlipReel Parse(ParsingContext context, long componentOffset, uint componentId, byte[] data)
        {
            ComponentProfile profile = ComponentProfileCatalog.Resolve(MFMEComponentType.FlipReel, context.Version);
            var (component, offset, parseData) = ParseBase(context, profile, componentOffset, componentId, data);

                        var parseResult = ParseExtendedTags(component, profile, parseData, offset);

            ApplyExtendedTags(component, parseResult, profile.Tags);

            return component;
        }

        private void ApplyExtendedTags(FlipReel component, ExtendedTagParser.ParseResult parseResult, ComponentTagMap componentTagMap)
        {

            ApplyExtendedTagsByReflection(component, parseResult, componentTagMap);

            if (parseResult.ValuesByTag.TryGetValue(0x39, out var sublampData) &&
                sublampData is List<LampSublampTableEntry> sublampTable)
            {
                foreach (var subLampTableEntry in ((List<LampSublampTableEntry>)sublampData))
                {
                    uint currentLampNumber = 0;
                    if (subLampTableEntry.SublampNumber > 0)
                    {
                        currentLampNumber = (uint)subLampTableEntry.SublampNumber;
                    }
                    switch (subLampTableEntry.SublampIndex)
                    {
                        case 1:
                            component.UInt32s.Add("SubLamp1Number", currentLampNumber);
                            break;
                        case 2:
                            component.UInt32s.Add("SubLamp2Number", currentLampNumber);
                            break;
                        case 3:
                            component.UInt32s.Add("SubLamp3Number", currentLampNumber);
                            break;
                    }
                }
            }
        }
    }
}
