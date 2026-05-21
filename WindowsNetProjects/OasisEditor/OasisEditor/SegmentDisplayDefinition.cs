using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Media;

namespace OasisEditor;

internal sealed class SegmentDisplayDefinition
{
    [JsonPropertyName("schema")]
    public string? Schema { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("units")]
    public string? Units { get; set; }

    [JsonPropertyName("mameComponentType")]
    public string? MameComponentType { get; set; }

    [JsonPropertyName("cell")]
    public SegmentDisplayCellDefinition? Cell { get; set; }
}

internal sealed class SegmentDisplayCellDefinition
{
    [JsonPropertyName("size")]
    public SegmentDisplaySizeDefinition? Size { get; set; }

    [JsonPropertyName("recommendedPitch")]
    public double RecommendedPitch { get; set; }

    [JsonPropertyName("segments")]
    public List<SegmentDisplaySegmentDefinition>? Segments { get; set; }

    [JsonPropertyName("decimalPoint")]
    public SegmentDisplayPunctuationDefinition? DecimalPoint { get; set; }

    [JsonPropertyName("commaTail")]
    public SegmentDisplayPunctuationDefinition? CommaTail { get; set; }
}

internal sealed class SegmentDisplaySegmentDefinition
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("bitIndex")]
    public int? BitIndex { get; set; }

    [JsonPropertyName("pathData")]
    public string? PathData { get; set; }

    [JsonIgnore]
    public Geometry? Geometry { get; set; }
}

internal sealed class SegmentDisplayPunctuationDefinition
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("bitIndex")]
    public int? BitIndex { get; set; }

    [JsonPropertyName("pathData")]
    public string? PathData { get; set; }

    [JsonIgnore]
    public Geometry? Geometry { get; set; }
}

internal sealed class SegmentDisplaySizeDefinition
{
    [JsonPropertyName("width")]
    public double Width { get; set; }

    [JsonPropertyName("height")]
    public double Height { get; set; }

    [JsonIgnore]
    public Size AsSize => new(Math.Max(0d, Width), Math.Max(0d, Height));
}
