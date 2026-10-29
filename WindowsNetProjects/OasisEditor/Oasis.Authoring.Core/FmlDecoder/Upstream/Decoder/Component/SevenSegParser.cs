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
    internal sealed class SevenSegParser : ComponentParserBase<SevenSeg>
    {
        // Per-segment lamp numbers (0x39), emitted as a flat array on the component.
        private const uint SubLampTableTag = 0x39;

        // Display style selector (0x20): maps the raw style id to a friendly style name,
        // matching how SevenSegBlock resolves its SelectedTypeId.
        private const uint SelectedStyleTag = 0x20;
        private const string SelectedStyleIdAttribute = "SelectedStyleId";
        private static readonly IReadOnlyDictionary<byte, string> SelectedStyleOptions = new Dictionary<byte, string>
        {
            { 0x00, "Style 1" },
            { 0x01, "Style 2" },
            { 0x02, "Style 3" },
        };

        // Outer-section tags (before the 4C ?? 00 nested block); the rest live inside the nested block.


        public SevenSeg Parse(ParsingContext context, long componentOffset, uint componentId, byte[] data)
        {
            ComponentProfile profile = ComponentProfileCatalog.Resolve(MFMEComponentType.SevenSeg, context.Version);
            var (component, offset, parseData) = ParseBase(context, profile, componentOffset, componentId, data);


            var parseResult = ParseExtendedTags(component, profile, parseData, offset);

            ApplyExtendedTags(component, parseResult, profile.Tags);

            return component;
        }

        private void ApplyExtendedTags(SevenSeg component, ExtendedTagParser.ParseResult parseResult, ComponentTagMap componentTagMap)
        {

            ApplyExtendedTagsByReflection(component, parseResult, componentTagMap);
            ApplySelectedStyle(component, parseResult);
            ApplySubLampTable(component, parseResult);
        }

        // The SubLampTable (tag 0x39) lives in the outer section; expose its lamp numbers as a flat array.
        private static void ApplySubLampTable(SevenSeg component, ExtendedTagParser.ParseResult parseResult)
        {
            if (parseResult.ValuesByTag.TryGetValue(SubLampTableTag, out object value)
                && value is IReadOnlyList<LampSublampTableEntry> subLampTable
                && subLampTable.Count > 0)
            {
                component.SublampTable = subLampTable;
            }
        }

        // Resolve the raw SelectedStyleId byte to a friendly style name, dropping the raw id.
        private static void ApplySelectedStyle(SevenSeg component, ExtendedTagParser.ParseResult parseResult)
        {
            var nestedResult = parseResult.NestedTagBlockResult;
            IReadOnlyDictionary<uint, object> valuesByTag =
                nestedResult is not null ? nestedResult.ValuesByTag : parseResult.ValuesByTag;

            if (valuesByTag.TryGetValue(SelectedStyleTag, out object value)
                && value is byte styleId
                && SelectedStyleOptions.TryGetValue(styleId, out string styleName))
            {
                component.Strings["SelectedType"] = styleName;
            }

            component.Bytes.Remove(SelectedStyleIdAttribute);
        }
    }
}
