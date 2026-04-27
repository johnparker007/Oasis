namespace OasisEditor.Features.MfmeImport;

internal sealed record MfmeExtractDocument
{
    public required string SourceExtractPath { get; init; }

    public required string ManifestPath { get; init; }

    public required string LayoutName { get; init; }

    public IReadOnlyList<MfmeExtractComponentData> Components { get; init; } = [];
}
