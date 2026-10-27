using SkiaSharp;

namespace OasisEditor;

internal sealed record FaceArtworkProcessingResult(bool Succeeded, string? ErrorMessage)
{
    public static FaceArtworkProcessingResult Success { get; } = new(true, null);
    public static FaceArtworkProcessingResult Failure(string message) => new(false, message);
}

/// <summary>WPF compatibility adapter for the shared Core correction, calibration and override stages.</summary>
internal sealed class FaceArtworkRebuildService
{
    public const int MaximumOutputDimension = FacePipeline.FaceArtworkRebuildService.MaximumOutputDimension;
    public const long MaximumOutputPixels = FacePipeline.FaceArtworkRebuildService.MaximumOutputPixels;

    public string? RebuildCorrectionInput(FaceArtworkModel artwork, Panel2DDocumentModel panel,
        PanelFaceSourceShapeModel shape, string? projectDirectory, string correctionInputPath,
        FaceGenerationSettingsModel? generationSettings = null)
        => new FacePipeline.FaceArtworkRebuildService().RebuildCorrectionInput(artwork, panel, shape,
            projectDirectory, correctionInputPath, generationSettings);

    public string? RebuildImageCorrectionInput(FaceArtworkModel artwork, string projectDirectory,
        string correctionInputPath, FaceGenerationSettingsModel? generationSettings = null)
        => new FacePipeline.FaceArtworkRebuildService().RebuildImageCorrectionInput(artwork, projectDirectory,
            correctionInputPath, generationSettings);

    public FaceArtworkProcessingResult BuildBaseFromCorrectionInput(FaceArtworkModel artwork, string projectDirectory)
        => Map(new FacePipeline.FaceArtworkRebuildService().BuildBaseFromCorrectionInput(artwork, projectDirectory));

    public FaceArtworkProcessingResult FinalizeOutput(FaceArtworkModel artwork, string projectDirectory)
        => Map(new FacePipeline.FaceArtworkRebuildService().FinalizeOutput(artwork, projectDirectory));

    internal static void ApplyAlpha(SKBitmap colour, SKBitmap alpha)
        => FacePipeline.FaceArtworkRebuildService.ApplyAlpha(colour, alpha);

    public static (int Width, int Height) DetermineOutputSize(int baseWidth, int baseHeight,
        FaceArtworkOverrideModel artworkOverride, int overrideWidth, int overrideHeight)
        => FacePipeline.FaceArtworkRebuildService.DetermineOutputSize(baseWidth, baseHeight,
            artworkOverride, overrideWidth, overrideHeight);

    internal static SKBitmap RectifyOverride(SKBitmap source, FacePerspectiveRegistrationModel registration)
        => FacePipeline.FaceArtworkRebuildService.RectifyOverride(source, registration);

    private static FaceArtworkProcessingResult Map(FacePipeline.FaceArtworkProcessingResult value)
        => value.Succeeded ? FaceArtworkProcessingResult.Success : FaceArtworkProcessingResult.Failure(value.ErrorMessage ?? "Face artwork processing failed.");
}
