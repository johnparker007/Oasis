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

    private static readonly Lazy<SegmentDisplayDefinition?> LazySixteenSegmentDefinition =
        new(() => TryLoadDefinition("Assets/SegmentDisplays/oasis_16_segment_display_definition.json", 16));

    private static readonly Lazy<SegmentDisplayDefinition?> LazySevenSegmentDefinition =
        new(() => TryLoadDefinition("Assets/SegmentDisplays/oasis_7_segment_display_definition.json", 7));

    public static bool TryGetSixteenSegmentDefinition(out SegmentDisplayDefinition definition)
    {
        definition = LazySixteenSegmentDefinition.Value!;
        return definition is not null;
    }

    public static bool TryGetSevenSegmentDefinition(out SegmentDisplayDefinition definition)
    {
        definition = LazySevenSegmentDefinition.Value!;
        return definition is not null;
    }

    private static SegmentDisplayDefinition? TryLoadDefinition(string relativePath, int expectedSegmentCount)
    {
        var definitionPath = Path.Combine(AppContext.BaseDirectory, relativePath);
        if (!File.Exists(definitionPath))
        {
            return null;
        }

        var json = File.ReadAllText(definitionPath);
        var definition = JsonSerializer.Deserialize<SegmentDisplayDefinition>(json, JsonOptions);

        try
        {
            Validate(definition, expectedSegmentCount);
            return definition!;
        }
        catch
        {
            return null;
        }
    }

    private static void Validate(SegmentDisplayDefinition? definition, int expectedSegmentCount)
    {
        if (definition?.Cell?.Size is null)
        {
            throw new InvalidOperationException("Segment definition is missing cell.size.");
        }

        var segments = definition.Cell.Segments;
        if (segments is null || segments.Count != expectedSegmentCount)
        {
            throw new InvalidOperationException($"Segment definition must contain exactly {expectedSegmentCount} segments.");
        }

        foreach (var segment in segments)
        {
            if (string.IsNullOrWhiteSpace(segment.PathData))
            {
                throw new InvalidOperationException($"Segment {segment.Index} is missing pathData.");
            }

            segment.Geometry = Geometry.Parse(segment.PathData);
            segment.Geometry.Freeze();
        }

        if (!string.IsNullOrWhiteSpace(definition.Cell.DecimalPoint?.PathData))
        {
            definition.Cell.DecimalPoint.Geometry = Geometry.Parse(definition.Cell.DecimalPoint.PathData);
            definition.Cell.DecimalPoint.Geometry.Freeze();
        }
    }
}
