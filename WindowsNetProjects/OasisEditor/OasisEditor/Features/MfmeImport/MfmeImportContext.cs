namespace OasisEditor.Features.MfmeImport;

internal sealed class MfmeImportContext
{
    public required string SourceExtractPath { get; init; }

    public required string ProjectRootPath { get; init; }

    public required string ProjectAssetsPath { get; init; }

    public bool CopyAssets { get; init; } = true;

    public string? LayoutDisplayName { get; init; }
}
