using System.IO;
using SkiaSharp;

namespace OasisEditor;

internal sealed record FaceArtworkProcessingResult(bool Succeeded, string? ErrorMessage)
{
    public static FaceArtworkProcessingResult Success { get; } = new(true, null);
    public static FaceArtworkProcessingResult Failure(string message) => new(false, message);
}

/// <summary>Builds the generated correction input, Base and Output stages.</summary>
internal sealed class FaceArtworkRebuildService
{
    /// <summary>Rectifies and sharpens source artwork, without applying any processing operations.</summary>
    public string? RebuildCorrectionInput(FaceArtworkModel artwork, Panel2DDocumentModel panel,
        PanelFaceSourceShapeModel shape, string? projectDirectory, string correctionInputPath,
        FaceGenerationSettingsModel? generationSettings = null)
    {
        ArgumentNullException.ThrowIfNull(artwork);
        if (artwork.Source.Kind != FaceArtworkSourceKind.Panel2DFaceSourceShape)
            throw new NotSupportedException("Independent artwork sources are not rebuildable until Phase 5.");
        if (string.IsNullOrWhiteSpace(projectDirectory)) return null;

        var absoluteInput = FaceArtworkGeneratedPathService.Resolve(correctionInputPath, projectDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(absoluteInput)!);
        var geometryPath = Path.Combine(Path.GetDirectoryName(absoluteInput)!, $".geometry-{Guid.NewGuid():N}.png");
        try
        {
            var generated = FaceSourceShapeTransformService.TryGenerateBackground(panel, shape, artwork.OutputWidth,
                artwork.OutputHeight, projectDirectory, geometryPath);
            if (generated is null) return null;
            using var geometry = SKBitmap.Decode(geometryPath);
            if (geometry is null) return null;
            using var sharpened = FaceArtworkSharpeningService.Apply(geometry,
                generationSettings ?? FaceGenerationSettingsModel.Default);
            WriteVerified(sharpened, absoluteInput);
            return FaceArtworkGeneratedPathService.ToProjectRelative(absoluteInput, projectDirectory);
        }
        finally { if (File.Exists(geometryPath)) File.Delete(geometryPath); }
    }

    /// <summary>Applies the authored processing stack to the cached correction input.</summary>
    public FaceArtworkProcessingResult BuildBaseFromCorrectionInput(FaceArtworkModel artwork, string projectDirectory)
    {
        if (string.IsNullOrWhiteSpace(artwork.CorrectionInputAssetPath) || string.IsNullOrWhiteSpace(artwork.BaseAssetPath))
            return FaceArtworkProcessingResult.Failure("The Face has no generated Correction Input/Base artwork paths.");
        var inputPath = FaceArtworkGeneratedPathService.Resolve(artwork.CorrectionInputAssetPath, projectDirectory);
        var basePath = FaceArtworkGeneratedPathService.Resolve(artwork.BaseAssetPath, projectDirectory);
        if (!File.Exists(inputPath)) return FaceArtworkProcessingResult.Failure($"Correction input was not found at '{inputPath}'.");
        try
        {
            using var input = SKBitmap.Decode(inputPath);
            if (input is null) return FaceArtworkProcessingResult.Failure($"Correction input could not be decoded: '{inputPath}'.");
            using var corrected = new FaceArtworkProcessingPipeline().Evaluate(input, artwork.ProcessingPipeline);
            WriteVerified(corrected, basePath);
            return FaceArtworkProcessingResult.Success;
        }
        catch (Exception exception) { return FaceArtworkProcessingResult.Failure($"Base artwork failed: {exception.Message}"); }
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
