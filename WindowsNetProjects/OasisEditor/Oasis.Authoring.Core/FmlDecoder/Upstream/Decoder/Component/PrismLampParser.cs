using MfmeFmlDecoder.Decoder;
using MfmeFmlDecoder.Model;
using MfmeFmlDecoder.src.Decoder.Component.Core;
using MfmeFmlDecoder.src.Decoder.Component.Profiles;
using MfmeFmlDecoder.src.Model.Component;
using System;
using System.Collections.Generic;

namespace MfmeFmlDecoder.src.Decoder.Component
{
    internal sealed class PrismLampParser : ComponentParserBase<PrismLamp>
    {


        public PrismLamp Parse(ParsingContext context, long componentOffset, uint componentId, byte[] data)
        {
            ComponentProfile profile = ComponentProfileCatalog.Resolve(MFMEComponentType.PrismLamp, context.Version);
            var (component, offset, parseData) = ParseBase(context, profile, componentOffset, componentId, data);

                        var parseResult = ParseExtendedTags(component, profile, parseData, offset);

            ApplyExtendedTags(component, parseResult, profile.Tags);
            if (profile.Defaults.MoveIsHorizontalToProperty
                && component.Booleans.TryGetValue("IsHorizontal", out bool isHorizontal)
                && isHorizontal)
            {
                component.Booleans.Remove("IsHorizontal");
            }

            if (parseResult.ValuesByTag.TryGetValue(0x08, out object orientationValue)
                && orientationValue is byte orientationFlag)
            {
                component.Orientation = orientationFlag == 0 ? "Vertical" : "Horizontal";
            }

            if (profile.Defaults?.SynthesizeOffImage == true
                && !component.Images.ContainsKey("off_image"))
            {
                int width = checked((int)component.Width);
                int height = checked((int)component.Height);
                component.Images["off_image"] = new BitmapEntry(
                    width,
                    height,
                    32,
                    new byte[checked(width * height * 4)])
                {
                    Purpose = "off_image",
                };
            }

            return component;
        }

        private void ApplyExtendedTags(PrismLamp component, ExtendedTagParser.ParseResult parseResult, ComponentTagMap componentTagMap)
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
                    }
                }
            }
        }
    }
}
