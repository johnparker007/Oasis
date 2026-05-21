using System.IO;
using System.Text.Json;
using System.Windows.Media;

namespace OasisEditor;

internal static class SegmentDisplayDefinitionLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly IReadOnlyDictionary<string, SegmentDisplayAssetSpec> AssetSpecs = new Dictionary<string, SegmentDisplayAssetSpec>(StringComparer.OrdinalIgnoreCase)
    {
        ["led14seg"] = new("Assets/SegmentDisplays/oasis_14_segment_display_definition.json", 14, null, null),
        ["led14segsc"] = new("Assets/SegmentDisplays/oasis_14_segment_sc_display_definition.json", 14, 14, 15),
        ["led16seg"] = new("Assets/SegmentDisplays/oasis_16_segment_display_definition.json", 16, null, null),
        ["led16segsc"] = new("Assets/SegmentDisplays/oasis_16_segment_sc_display_definition.json", 16, 16, 17),
        ["7seg"] = new("Assets/SegmentDisplays/oasis_7_segment_display_definition.json", 7, null, null)
    };

    private static readonly Lazy<IReadOnlyDictionary<string, SegmentDisplayDefinition>> LazyDefinitions = new(LoadDefinitions);

    public static bool TryGetSixteenSegmentDefinition(out SegmentDisplayDefinition definition) => TryGetDefinitionByType("led16seg", out definition);
    public static bool TryGetSevenSegmentDefinition(out SegmentDisplayDefinition definition) => TryGetDefinitionByType("7seg", out definition);

    public static bool TryGetDefinitionByType(string displayType, out SegmentDisplayDefinition definition)
    {
        if (LazyDefinitions.Value.TryGetValue(displayType, out definition!))
        {
            return true;
        }

        definition = null!;
        return false;
    }

    private static IReadOnlyDictionary<string, SegmentDisplayDefinition> LoadDefinitions()
    {
        var definitions = new Dictionary<string, SegmentDisplayDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, spec) in AssetSpecs)
        {
            var definition = TryLoadDefinition(spec);
            if (definition is not null)
            {
                definitions[key] = definition;
            }
        }

        return definitions;
    }

    private static SegmentDisplayDefinition? TryLoadDefinition(SegmentDisplayAssetSpec spec)
    {
        var definitionPath = Path.Combine(AppContext.BaseDirectory, spec.RelativePath);
        if (!File.Exists(definitionPath)) return null;

        var definition = JsonSerializer.Deserialize<SegmentDisplayDefinition>(File.ReadAllText(definitionPath), JsonOptions);
        try
        {
            Validate(definition, spec);
            return definition;
        }
        catch
        {
            return null;
        }
    }

    private static void Validate(SegmentDisplayDefinition? definition, SegmentDisplayAssetSpec spec)
    {
        if (definition?.Cell?.Size is null) throw new InvalidOperationException("Segment definition is missing cell.size.");
        var segments = definition.Cell.Segments;
        if (segments is null || segments.Count != spec.ExpectedMainSegmentCount)
        {
            throw new InvalidOperationException($"Segment definition must contain exactly {spec.ExpectedMainSegmentCount} segments.");
        }

        foreach (var segment in segments)
        {
            if (string.IsNullOrWhiteSpace(segment.PathData)) throw new InvalidOperationException($"Segment {segment.Index} is missing pathData.");
            segment.Geometry = Geometry.Parse(segment.PathData);
            segment.Geometry.Freeze();
        }

        ParsePunctuation(definition.Cell.DecimalPoint, spec.RequiredDecimalPointBitIndex, "decimalPoint");
        ParsePunctuation(definition.Cell.CommaTail, spec.RequiredCommaTailBitIndex, "commaTail");
    }

    private static void ParsePunctuation(SegmentDisplayPunctuationDefinition? punctuation, int? requiredBitIndex, string fieldName)
    {
        if (requiredBitIndex.HasValue)
        {
            if (punctuation is null || string.IsNullOrWhiteSpace(punctuation.PathData))
            {
                throw new InvalidOperationException($"Segment definition must contain {fieldName} for this display type.");
            }

            if (punctuation.BitIndex.GetValueOrDefault(requiredBitIndex.Value) != requiredBitIndex.Value)
            {
                throw new InvalidOperationException($"Segment definition {fieldName}.bitIndex must be {requiredBitIndex.Value}.");
            }
        }

        if (punctuation is not null && !string.IsNullOrWhiteSpace(punctuation.PathData))
        {
            punctuation.Geometry = Geometry.Parse(punctuation.PathData);
            punctuation.Geometry.Freeze();
        }
    }

    private sealed record SegmentDisplayAssetSpec(string RelativePath, int ExpectedMainSegmentCount, int? RequiredDecimalPointBitIndex, int? RequiredCommaTailBitIndex);
}
