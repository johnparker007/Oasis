using System.Text.Json.Serialization;
using MfmeFmlDecoder.Model;
using MfmeFmlDecoder.src.Decoder.Component.Core;

namespace MfmeFmlDecoder.src.Model.Component;

internal abstract partial class BaseComponent
{
    // Defaults are also placed in the value dictionaries. Input mapping must
    // distinguish an absent button number from an explicitly stored zero.
    [JsonIgnore]
    public HashSet<string> PresentValueKeys { get; } = new(StringComparer.Ordinal);

    public bool WasValuePresent(string key) => PresentValueKeys.Contains(key);
}

internal static class OasisSourcePresence
{
    public static void Record(BaseComponent component, ComponentTagMap map, ExtendedTagParser.ParseResult result)
    {
        if (result.ExplicitTags is not null)
            foreach (var id in result.ExplicitTags)
                if (map.TryGetValue(id, out var tag)) component.PresentValueKeys.Add(tag.AttributeName);
        if (map.NestedTagBlockMap is not null && result.NestedTagBlockResult is not null)
            Record(component, map.NestedTagBlockMap, result.NestedTagBlockResult);
    }
}
