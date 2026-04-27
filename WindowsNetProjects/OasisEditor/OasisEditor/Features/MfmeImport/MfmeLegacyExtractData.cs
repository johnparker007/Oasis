namespace OasisEditor.Features.MfmeImport;

internal sealed class MfmeLegacyExtractData
{
    public required string ExtractRootPath { get; init; }

    public required string ManifestPath { get; init; }

    public required string LayoutName { get; init; }

    public required IReadOnlyList<MfmeLegacyComponentBase> Components { get; init; }
}
