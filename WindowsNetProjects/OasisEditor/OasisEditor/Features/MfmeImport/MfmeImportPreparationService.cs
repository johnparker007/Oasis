using System;
using System.Collections.Generic;

namespace OasisEditor.Features.MfmeImport;

internal sealed record MfmePreparedImportResult
{
    public string? LayoutName { get; init; }

    public IReadOnlyList<PanelElementModel> Elements { get; init; } = [];

    public IReadOnlyList<string> CopiedAssets { get; init; } = [];

    public IReadOnlyList<MfmeExtractComponentData> SkippedComponents { get; init; } = [];

    public IReadOnlyList<MfmeImportWarning> Warnings { get; init; } = [];

    public IReadOnlyList<string> Errors { get; init; } = [];

    public bool HasErrors => Errors.Count > 0;
}

internal sealed class MfmeImportPreparationService
{
    private readonly IMfmeExtractReader _extractReader;
    private readonly MfmeComponentMapper _componentMapper;
    private readonly MfmeImportAssetCopyService _assetCopyService;

    public MfmeImportPreparationService(
        IMfmeExtractReader extractReader,
        MfmeComponentMapper componentMapper,
        MfmeImportAssetCopyService assetCopyService)
    {
        _extractReader = extractReader ?? throw new ArgumentNullException(nameof(extractReader));
        _componentMapper = componentMapper ?? throw new ArgumentNullException(nameof(componentMapper));
        _assetCopyService = assetCopyService ?? throw new ArgumentNullException(nameof(assetCopyService));
    }

    public MfmePreparedImportResult Prepare(MfmeImportContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var readResult = _extractReader.Read(context);
        var warnings = new List<MfmeImportWarning>(readResult.Warnings);
        var errors = new List<string>(readResult.Errors);
        var skipped = new List<MfmeExtractComponentData>(readResult.SkippedComponents);
        var copiedAssets = new List<string>();

        if (errors.Count > 0 || readResult.ExtractDocument is null)
        {
            if (errors.Count == 0)
            {
                errors.Add("MFME extract reader did not return a parsed extract document.");
            }

            return new MfmePreparedImportResult
            {
                LayoutName = readResult.ExtractDocument?.LayoutName,
                Elements = [],
                CopiedAssets = copiedAssets,
                SkippedComponents = skipped,
                Warnings = warnings,
                Errors = errors
            };
        }

        var mapping = _componentMapper.Map(readResult.ImportedElements);
        warnings.AddRange(mapping.Warnings);
        skipped.AddRange(mapping.SkippedComponents);

        var elements = mapping.Elements;

        if (context.CopyAssetsToProject && elements.Count > 0)
        {
            var copyResult = _assetCopyService.CopyAssets(context, readResult.ExtractDocument.LayoutName, elements);
            elements = copyResult.Elements;
            copiedAssets.AddRange(copyResult.CopiedAssets);
            warnings.AddRange(copyResult.Warnings);
        }

        return new MfmePreparedImportResult
        {
            LayoutName = readResult.ExtractDocument.LayoutName,
            Elements = elements,
            CopiedAssets = copiedAssets,
            SkippedComponents = skipped,
            Warnings = warnings,
            Errors = errors
        };
    }
}
