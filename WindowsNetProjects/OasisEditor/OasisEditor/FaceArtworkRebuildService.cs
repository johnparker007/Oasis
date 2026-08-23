using System.IO;
using SkiaSharp;

namespace OasisEditor;

internal sealed record FaceArtworkProcessingResult(bool Succeeded, string? ErrorMessage)
{
    public static FaceArtworkProcessingResult Success { get; } = new(true, null);
    public static FaceArtworkProcessingResult Failure(string message) => new(false, message);
}

/// <summary>Builds Base from the authoritative Panel2D recipe, then finalizes Output from Base.</summary>
internal sealed class FaceArtworkRebuildService
{
    public string? RebuildBase(FaceArtworkModel artwork, Panel2DDocumentModel panel, PanelFaceSourceShapeModel shape,
        string? projectDirectory, string basePath, FaceGenerationSettingsModel? generationSettings = null)
    {
        ArgumentNullException.ThrowIfNull(artwork);
        if (artwork.Source.Kind != FaceArtworkSourceKind.Panel2DFaceSourceShape)
            throw new NotSupportedException("Independent artwork sources are not rebuildable until Phase 5.");
        if (string.IsNullOrWhiteSpace(projectDirectory)) return null;

        var absoluteBase = FaceArtworkGeneratedPathService.Resolve(basePath, projectDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(absoluteBase)!);
        var geometryPath = Path.Combine(Path.GetDirectoryName(absoluteBase)!, $".geometry-{Guid.NewGuid():N}.png");
        try
        {
            var generated = FaceSourceShapeTransformService.TryGenerateBackground(panel, shape, artwork.OutputWidth,
                artwork.OutputHeight, projectDirectory, geometryPath);
            if (generated is null) return null;
            using var geometry = SKBitmap.Decode(geometryPath);
            if (geometry is null) return null;
            using var sharpened = FaceArtworkSharpeningService.Apply(geometry,
                generationSettings ?? FaceGenerationSettingsModel.Default);
            using var corrected = new FaceArtworkProcessingPipeline().Evaluate(sharpened, artwork.ProcessingPipeline);
            WriteVerified(corrected, absoluteBase);
            return FaceArtworkGeneratedPathService.ToProjectRelative(absoluteBase, projectDirectory);
        }
        finally { if (File.Exists(geometryPath)) File.Delete(geometryPath); }
    }

    public FaceArtworkProcessingResult FinalizeOutput(FaceArtworkModel artwork, string projectDirectory)
    {
        if (string.IsNullOrWhiteSpace(artwork.BaseAssetPath) || string.IsNullOrWhiteSpace(artwork.OutputAssetPath))
            return FaceArtworkProcessingResult.Failure("The Face has no generated Base/Output artwork paths.");
        var basePath = FaceArtworkGeneratedPathService.Resolve(artwork.BaseAssetPath, projectDirectory);
        var outputPath = FaceArtworkGeneratedPathService.Resolve(artwork.OutputAssetPath, projectDirectory);
        if (!File.Exists(basePath)) return FaceArtworkProcessingResult.Failure($"Base artwork was not found at '{basePath}'.");
        try
        {
            using var bitmap = SKBitmap.Decode(basePath);
            if (bitmap is null) return FaceArtworkProcessingResult.Failure($"Base artwork could not be decoded: '{basePath}'.");
            WriteVerified(bitmap, outputPath);
            return FaceArtworkProcessingResult.Success;
        }
        catch (Exception exception) { return FaceArtworkProcessingResult.Failure($"Artwork output failed: {exception.Message}"); }
    }

    internal static SKBitmap? BuildCorrectionInput(FaceArtworkModel artwork, Panel2DDocumentModel panel,
        PanelFaceSourceShapeModel shape, string projectDirectory, FaceGenerationSettingsModel settings)
    {
        var temp = Path.Combine(Path.GetTempPath(), $"oasis-geometry-{Guid.NewGuid():N}.png");
        try
        {
            if (FaceSourceShapeTransformService.TryGenerateBackground(panel, shape, artwork.OutputWidth,
                    artwork.OutputHeight, projectDirectory, temp) is null) return null;
            using var geometry = SKBitmap.Decode(temp);
            return geometry is null ? null : FaceArtworkSharpeningService.Apply(geometry, settings);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    private static void WriteVerified(SKBitmap bitmap, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = $"{path}.{Guid.NewGuid():N}.tmp";
        using (var image = SKImage.FromBitmap(bitmap))
        using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
        using (var stream = File.Create(temporary)) data.SaveTo(stream);
        using (var verification = SKBitmap.Decode(temporary))
            if (verification is null) throw new InvalidDataException($"Generated PNG could not be read back: '{path}'.");
        File.Move(temporary, path, true);
    }
}
