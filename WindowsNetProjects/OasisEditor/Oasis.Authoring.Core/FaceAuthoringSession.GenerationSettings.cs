namespace OasisEditor;

public sealed partial class FaceAuthoringSession
{
    /// <summary>Set native Face generation settings as one undoable document edit.</summary>
    public bool UpdateGenerationSettings(FaceGenerationSettingsModel settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var document = RequireDocument();
        ValidateGenerationSettings(settings);
        var normalized = settings.Normalize();
        var nativeSettings = new FaceGenerationSettingsFile
        {
            MaskExtractionThreshold = normalized.MaskExtractionThreshold,
            TrayBoundsInflationPercent = normalized.TrayBoundsInflationPercent,
            TrayBoundsPaddingPixels = normalized.TrayBoundsPaddingPixels,
            ClampTrayBoundsToLampWindow = normalized.ClampTrayBoundsToLampWindow,
            PostWarpSharpeningEnabled = normalized.PostWarpSharpeningEnabled,
            PostWarpSharpeningAmount = normalized.PostWarpSharpeningAmount,
            PostWarpSharpeningRadiusPixels = normalized.PostWarpSharpeningRadiusPixels,
            PostWarpSharpeningThreshold = normalized.PostWarpSharpeningThreshold
        };
        if (Equals(document.GenerationSettings ?? new FaceGenerationSettingsFile(), nativeSettings)) return false;
        var buildState = CloneBuildState(document.BuildState);
        InvalidateConfigured(buildState, FaceGeneratedProduct.BaseArtwork, FaceGeneratedProduct.ArtworkOutput, FaceGeneratedProduct.RuntimeAssets,
            FaceGeneratedProduct.LampMask, FaceGeneratedProduct.Trays);
        var next = document with { GenerationSettings = nativeSettings, BuildState = buildState };
        var serialized = FaceDocumentStorage.Serialize(next);
        if (!FaceDocumentStorage.TryReadValidated(serialized, out _, out var error)) throw new InvalidDataException(error);
        Commit(next);
        return true;
    }

    private static void ValidateGenerationSettings(FaceGenerationSettingsModel settings)
    {
        if (!double.IsFinite(settings.TrayBoundsInflationPercent) || settings.TrayBoundsInflationPercent < 0)
            throw new ArgumentOutOfRangeException(nameof(settings), "Tray inflation percent must be finite and non-negative.");
        if (!double.IsFinite(settings.TrayBoundsPaddingPixels) || settings.TrayBoundsPaddingPixels < 0)
            throw new ArgumentOutOfRangeException(nameof(settings), "Tray padding pixels must be finite and non-negative.");
        if (!double.IsFinite(settings.PostWarpSharpeningAmount) || settings.PostWarpSharpeningAmount is < 0 or > 2)
            throw new ArgumentOutOfRangeException(nameof(settings), "Sharpen amount must be finite and from 0 to 2.");
        if (!double.IsFinite(settings.PostWarpSharpeningRadiusPixels) || settings.PostWarpSharpeningRadiusPixels is < 0.1 or > 3)
            throw new ArgumentOutOfRangeException(nameof(settings), "Sharpen radius must be finite and from 0.1 to 3 pixels.");
        if (settings.PostWarpSharpeningThreshold is < 0 or > 255)
            throw new ArgumentOutOfRangeException(nameof(settings), "Sharpen threshold must be from 0 to 255.");
    }

    private static FaceBuildStateModel CloneBuildState(FaceBuildStateModel source)
        => new()
        {
            Products = source.Products.ToDictionary(pair => pair.Key, pair => new FaceGeneratedProductStateModel
            { Status = pair.Value.Status, ErrorMessage = pair.Value.ErrorMessage })
        };

    private static void InvalidateConfigured(FaceBuildStateModel state, params FaceGeneratedProduct[] products)
    {
        foreach (var product in products)
        {
            var node = state.Get(product);
            if (node.Status == FaceBuildStatus.NotConfigured) continue;
            node.Status = FaceBuildStatus.Stale;
            node.ErrorMessage = null;
        }
    }

}
