using System.IO;
using System.Text.Json;
using System.Windows.Media;

namespace OasisEditor;

internal static class SegmentDisplayDefinitionLoader
{
    private const string RelativePath = "Assets/SegmentDisplays/oasis_16_segment_display_definition.json";
    private static readonly Lazy<SegmentDisplayDefinition> LazyDefinition = new(LoadDefinition);

    public static SegmentDisplayDefinition GetDefinition() => LazyDefinition.Value;

    private static SegmentDisplayDefinition LoadDefinition()
    {
        var definitionPath = Path.Combine(AppContext.BaseDirectory, RelativePath);
        if (!File.Exists(definitionPath))
        {
            throw new InvalidOperationException($"Segment definition asset not found: {definitionPath}");
        }

        var json = File.ReadAllText(definitionPath);
        var definition = JsonSerializer.Deserialize<SegmentDisplayDefinition>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Validate(definition);
        return definition!;
    }

    private static void Validate(SegmentDisplayDefinition? definition)
    {
        if (definition?.Cell?.Size is null)
        {
            throw new InvalidOperationException("Segment definition is missing cell.size.");
        }

        var segments = definition.Cell.Segments;
        if (segments is null || segments.Count != 16)
        {
            throw new InvalidOperationException("Segment definition must contain exactly 16 segments.");
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
