namespace OasisEditor.Features.MfmeImport;

internal sealed class MfmeImportResult
{
    public required IReadOnlyList<PanelElementModel> ImportedElements { get; init; }

    public required IReadOnlyList<string> CopiedAssetRelativePaths { get; init; }

    public required IReadOnlyList<InputDefinitionModel> InputDefinitions { get; init; }

    public required IReadOnlyList<string> SkippedLegacyComponentTypes { get; init; }

    public required IReadOnlyList<MfmeImportWarning> Warnings { get; init; }

    public required IReadOnlyList<string> Errors { get; init; }

    public IReadOnlyList<string> DebugDiagnostics { get; set; } = [];

    public bool Succeeded => Errors.Count == 0;
}
