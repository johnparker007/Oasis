namespace OasisEditor.Features.MfmeImport;

internal sealed record MfmeImportResult
{
    public MfmeExtractDocument? ExtractDocument { get; init; }

    public IReadOnlyList<MfmeExtractComponentData> ImportedElements { get; init; } = [];

    public IReadOnlyList<string> CopiedAssets { get; init; } = [];

    public IReadOnlyList<MfmeExtractComponentData> SkippedComponents { get; init; } = [];

    public IReadOnlyList<MfmeImportWarning> Warnings { get; init; } = [];

    public IReadOnlyList<string> Errors { get; init; } = [];

    public bool HasErrors => Errors.Count > 0;
}
