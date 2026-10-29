using MfmeFmlDecoder.Decoder;
using MfmeFmlDecoder.Model;
using MfmeFmlDecoder.src.Decoder.Component.Core;
using MfmeFmlDecoder.src.Decoder.Component.Profiles;
using MfmeFmlDecoder.src.Model.Component;
using System;

namespace MfmeFmlDecoder.src.Decoder.Component
{
    internal sealed class AlphaNewParser : ComponentParserBase<AlphaNew>
    {

        public AlphaNew Parse(ParsingContext context, long componentOffset, uint componentId, byte[] data)
        {
            ComponentProfile profile = ComponentProfileCatalog.Resolve(MFMEComponentType.AlphaNew, context.Version);
            var (component, offset, parseData) = ParseBase(context, profile, componentOffset, componentId, data);

                        var parseResult = ParseExtendedTags(component, profile, parseData, offset);

            ApplyExtendedTags(component, parseResult, profile.Tags);
            return component;
        }

        private void ApplyExtendedTags(AlphaNew component, ExtendedTagParser.ParseResult parseResult, ComponentTagMap componentTagMap)
        {

            ApplyExtendedTagsByReflection(component, parseResult, componentTagMap);

            if (parseResult.ValuesByTag.TryGetValue(0x05, out var legacyReversed)
                && legacyReversed is bool reversed)
            {
                component.Booleans["Reversed"] = reversed;
            }

            if (parseResult.ValuesByTag.TryGetValue(0x0C, out var charset)) component.Charset = MapCharset((byte)charset);
        }

        private static AlphaNewCharset MapCharset(byte value)
        {
            return value switch
            {
                0 => AlphaNewCharset.OKI_1937,
                1 => AlphaNewCharset.OLD_Charset,
                2 => AlphaNewCharset.BFM_Charset,
                _ => throw new InvalidOperationException(
                    $"Unknown AlphaNew charset tag value 0x{value:X2} (expected 0, 1, or 2)."),
            };
        }
    }
}
