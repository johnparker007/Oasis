using MfmeFmlDecoder.Decoder;
using MfmeFmlDecoder.Model;
using MfmeFmlDecoder.src.Model.Component;
using System.Collections.Generic;
using System;
using MfmeFmlDecoder.src.Decoder.Component.Core;
using MfmeFmlDecoder.src.Decoder.Component.Profiles;

namespace MfmeFmlDecoder.src.Decoder.Component
{
    internal sealed class ReelParser : ComponentParserBase<Reel>
    {
        private static readonly Dictionary<byte, string> offColourOptions = new Dictionary<byte, string>
        {
            { 0x0, "Mask 1" },
            { 0x1, "Mask 2" },
            { 0x2, "Mask 3" },
            { 0x3, "White" },
        };



        public Reel Parse(ParsingContext context, long componentOffset, uint componentId, byte[] data)
        {
            ComponentProfile profile = ComponentProfileCatalog.Resolve(MFMEComponentType.Reel, context.Version);
            var (component, offset, parseData) = ParseBase(context, profile, componentOffset, componentId, data);

                        var parseResult = ParseExtendedTags(component, profile, parseData, offset);

            ApplyExtendedTags(component, parseResult);

            return component;
        }

        private static void ApplyExtendedTags(Reel component, ExtendedTagParser.ParseResult parseResult)
        {
            if (parseResult.ValuesByTag.TryGetValue(0x39, out var sublampTableValue)
                && sublampTableValue is IReadOnlyList<LampSublampTableEntry> sublampTable)
            {
                component.SublampTable = sublampTable;
            }

            if (component.Int32s.TryGetValue("WidthDiff", out int widthDiff))
            {
                component.Int32s["WidthDiff"] = widthDiff * 2;
            }

            if (component.Bytes.TryGetValue("SelectedOffColourId", out byte colourId))
            {
                var offColourName = offColourOptions.TryGetValue(colourId, out var name) ? name : "Mask 1";
                component.Strings["OffColour"] = offColourName;
            }

            if (parseResult.ValuesByTag.TryGetValue(0x12, out object orientationValue)
                && orientationValue is byte orientationFlag)
            {
                component.Orientation = orientationFlag == 0 ? "Vertical" : "Horizontal";
            }

            if (parseResult.ValuesByTag.TryGetValue(0x13, out object legacyReversed)
                && legacyReversed is bool reversed)
            {
                component.Booleans["Reversed"] = reversed;
            }

            ApplyReelMode(component);
        }

        private static void ApplyReelMode(Reel component)
        {
            uint reelMode = component.UInt32s.TryGetValue("ReelMode", out uint value) ? value : 0;
            component.UInt32s.Remove("ReelMode");

            switch (reelMode)
            {
                case 0:
                    component.Booleans["DblSymbols"] = false;
                    component.Booleans["IGTReel"] = false;
                    break;
                case 1:
                    component.Booleans["DblSymbols"] = false;
                    component.Booleans["IGTReel"] = true;
                    break;
                case 2:
                    component.Booleans["DblSymbols"] = true;
                    component.Booleans["IGTReel"] = false;
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported ReelMode value {reelMode}.");
            }
        }
    }
}
