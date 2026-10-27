namespace OasisEditor.Features.LayoutImport;

internal sealed class LayoutImportResult
{
    public required IReadOnlyList<PanelElementModel> ImportedElements { get; init; }

    public required IReadOnlyList<string> CopiedAssetRelativePaths { get; init; }

    public required IReadOnlyList<InputDefinitionModel> InputDefinitions { get; init; }

    public required IReadOnlyList<string> UnsupportedComponentTypes { get; init; }

    public required IReadOnlyList<LayoutImportWarning> Warnings { get; init; }

    public required IReadOnlyList<string> Errors { get; init; }

    public IReadOnlyList<string> DebugDiagnostics { get; set; } = [];

    public bool Succeeded => Errors.Count == 0;
}
