namespace OasisEditor.Features.MfmeImport;

internal sealed class MfmeExtractReadResult
{
    public MfmeLegacyExtractData? Extract { get; init; }

    public required IReadOnlyList<MfmeImportWarning> Warnings { get; init; }

    public required IReadOnlyList<string> Errors { get; init; }

    public bool Succeeded => Errors.Count == 0;
}
