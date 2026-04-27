namespace OasisEditor.Features.MfmeImport;

internal sealed class MfmeImportResult
{
    public required IReadOnlyList<PanelElementModel> ImportedElements { get; init; }

    public required IReadOnlyList<string> CopiedAssetRelativePaths { get; init; }

    public required IReadOnlyList<string> SkippedLegacyComponentTypes { get; init; }

    public required IReadOnlyList<MfmeImportWarning> Warnings { get; init; }

    public required IReadOnlyList<string> Errors { get; init; }

    public bool Succeeded => Errors.Count == 0;
}
