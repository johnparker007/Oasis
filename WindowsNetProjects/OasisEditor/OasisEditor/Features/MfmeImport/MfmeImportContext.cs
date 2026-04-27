namespace OasisEditor.Features.MfmeImport;

internal sealed record MfmeImportContext
{
    public required string SourceExtractPath { get; init; }

    public required string ProjectRootPath { get; init; }

    public required string AssetsRootPath { get; init; }

    public bool CopyAssetsToProject { get; init; } = true;

    public string? LayoutDisplayName { get; init; }
}
