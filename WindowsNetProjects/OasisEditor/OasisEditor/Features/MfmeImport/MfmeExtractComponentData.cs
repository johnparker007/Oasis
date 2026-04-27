namespace OasisEditor.Features.MfmeImport;

internal sealed record MfmeExtractComponentData
{
    public required string SourceType { get; init; }

    public string? DisplayName { get; init; }

    public double X { get; init; }

    public double Y { get; init; }

    public double Width { get; init; }

    public double Height { get; init; }

    public string RawJson { get; init; } = string.Empty;
}
