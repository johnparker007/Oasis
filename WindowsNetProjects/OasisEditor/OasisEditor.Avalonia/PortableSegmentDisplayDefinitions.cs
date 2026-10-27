using System.Text.Json;

namespace OasisEditor;

internal sealed class SegmentDisplayDefinition
{
    public SegmentDisplayCellDefinition? Cell { get; set; }
}
internal sealed class SegmentDisplayCellDefinition
{
    public SegmentDisplaySizeDefinition? Size { get; set; }
    public double RecommendedPitch { get; set; }
    public List<SegmentDisplaySegmentDefinition>? Segments { get; set; }
    public SegmentDisplayPunctuationDefinition? DecimalPoint { get; set; }
    public SegmentDisplayPunctuationDefinition? CommaTail { get; set; }
}
internal sealed class SegmentDisplaySegmentDefinition
{
    public int Index { get; set; }
    public int? BitIndex { get; set; }
    public string? PathData { get; set; }
}
internal sealed class SegmentDisplayPunctuationDefinition
{
    public int? BitIndex { get; set; }
    public string? PathData { get; set; }
}
internal sealed class SegmentDisplaySizeDefinition
{
    public double Width { get; set; }
    public double Height { get; set; }
}

internal static class SegmentDisplayDefinitionLoader
{
    private static readonly IReadOnlyDictionary<string, string> Assets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["led14seg"] = "oasis_14_segment_display_definition.json",
        ["led14segsc"] = "oasis_14_segment_sc_display_definition.json",
        ["led16seg"] = "oasis_16_segment_display_definition.json",
        ["led16segsc"] = "oasis_16_segment_sc_display_definition.json",
        ["7seg"] = "oasis_7_segment_display_definition.json"
    };
    private static readonly IReadOnlyDictionary<string, SegmentDisplayDefinition> Definitions = Load();

    public static bool TryGetDefinitionByType(string type, out SegmentDisplayDefinition definition)
        => Definitions.TryGetValue(type, out definition!);

    public static bool TryGetSixteenSegmentDefinition(out SegmentDisplayDefinition definition) => TryGetDefinitionByType("led16seg", out definition);

    private static IReadOnlyDictionary<string, SegmentDisplayDefinition> Load()
    {
        var result = new Dictionary<string, SegmentDisplayDefinition>(StringComparer.OrdinalIgnoreCase);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        foreach (var (type, file) in Assets)
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Assets", "SegmentDisplays", file);
            if (!File.Exists(path)) continue;
            try
            {
                var definition = JsonSerializer.Deserialize<SegmentDisplayDefinition>(File.ReadAllText(path), options);
                if (definition?.Cell?.Size is { Width: > 0, Height: > 0 } && definition.Cell.Segments is { Count: > 0 }) result[type] = definition;
            }
            catch (JsonException) { }
        }
        return result;
    }
}
