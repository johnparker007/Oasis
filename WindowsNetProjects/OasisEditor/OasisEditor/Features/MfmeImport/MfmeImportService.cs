namespace OasisEditor.Features.MfmeImport;

internal sealed class MfmeImportService
{
    private readonly IMfmeExtractReader _extractReader;
    private readonly MfmeToOasisComponentMapper _componentMapper;
    private readonly MfmeImportAssetCopier _assetCopier;

    public MfmeImportService(
        IMfmeExtractReader? extractReader = null,
        MfmeToOasisComponentMapper? componentMapper = null,
        MfmeImportAssetCopier? assetCopier = null)
    {
        _extractReader = extractReader ?? new FileSystemMfmeExtractReader();
        _componentMapper = componentMapper ?? new MfmeToOasisComponentMapper();
        _assetCopier = assetCopier ?? new MfmeImportAssetCopier();
    }

    public MfmeImportResult Import(MfmeImportContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var readResult = _extractReader.Read(context);
        var warnings = new List<MfmeImportWarning>(readResult.Warnings);
        var errors = new List<string>(readResult.Errors);

        if (!readResult.Succeeded || readResult.Extract is null)
        {
            if (readResult.Extract is null && errors.Count == 0)
            {
                errors.Add("MFME extract could not be read.");
            }

            return new MfmeImportResult
            {
                ImportedElements = [],
                CopiedAssetRelativePaths = [],
                InputDefinitions = [],
                SkippedLegacyComponentTypes = [],
                Warnings = warnings,
                Errors = errors
            };
        }

        var mapResult = _componentMapper.Map(readResult.Extract);
        warnings.AddRange(mapResult.Warnings);

        var copyResult = _assetCopier.CopyAssets(context, readResult.Extract, mapResult.Elements);
        warnings.AddRange(copyResult.Warnings);
        errors.AddRange(copyResult.Errors);

        return new MfmeImportResult
        {
            ImportedElements = copyResult.Elements,
            CopiedAssetRelativePaths = copyResult.CopiedAssetRelativePaths,
            InputDefinitions = mapResult.InputDefinitions,
            SkippedLegacyComponentTypes = mapResult.SkippedLegacyComponentTypes,
            Warnings = warnings,
            Errors = errors
        };
    }
}
