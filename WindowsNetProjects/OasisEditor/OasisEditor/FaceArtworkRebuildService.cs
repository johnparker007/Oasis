using System.IO;
using SkiaSharp;

namespace OasisEditor;

/// <summary>Builds the disposable flattened texture from Face-owned artwork authoring state.</summary>
internal sealed record FaceArtworkProcessingResult(bool Succeeded, string? ErrorMessage)
{
    public static FaceArtworkProcessingResult Success { get; } = new(true, null);
    public static FaceArtworkProcessingResult Failure(string message) => new(false, message);
}

internal sealed class FaceArtworkRebuildService
{
    public string? Rebuild(
        FaceArtworkModel artwork,
        Panel2DDocumentModel panel,
        PanelFaceSourceShapeModel shape,
        string? projectDirectory,
        string outputPath)
    {
        ArgumentNullException.ThrowIfNull(artwork);
        ArgumentNullException.ThrowIfNull(panel);
        ArgumentNullException.ThrowIfNull(shape);

        if (artwork.Source.Kind != FaceArtworkSourceKind.Panel2DFaceSourceShape)
        {
            throw new NotSupportedException("Independent artwork sources are authored state but are not rebuildable yet.");
        }

        var generatedPath = FaceSourceShapeTransformService.TryGenerateBackground(
            panel, shape, artwork.OutputWidth, artwork.OutputHeight, projectDirectory, outputPath);
        if (generatedPath is null || string.IsNullOrWhiteSpace(projectDirectory)) return generatedPath;

        var absolutePath = ResolveGeneratedArtworkPath(generatedPath, projectDirectory);
        using var rectified = SKBitmap.Decode(absolutePath);
        if (rectified is null) return null;
        using (var originalImage = SKImage.FromBitmap(rectified))
        using (var originalData = originalImage.Encode(SKEncodedImageFormat.Png, 100))
        using (var originalStream = File.Create(GetOriginalArtworkPath(absolutePath)))
        {
            originalData.SaveTo(originalStream);
        }
        WriteProcessedArtwork(rectified, artwork.ProcessingPipeline, absolutePath);
        return generatedPath;
    }

    public FaceArtworkProcessingResult ApplyProcessing(FaceArtworkModel artwork, string projectDirectory)
    {
        ArgumentNullException.ThrowIfNull(artwork);
        if (string.IsNullOrWhiteSpace(artwork.GeneratedAssetPath))
            return FaceArtworkProcessingResult.Failure("The Face has no generated artwork path. Regenerate the Face before applying artwork processing.");
        var generatedPath = ResolveGeneratedArtworkPath(artwork.GeneratedAssetPath, projectDirectory);
        if (!File.Exists(generatedPath))
            return FaceArtworkProcessingResult.Failure($"Generated artwork was not found at '{generatedPath}'. Regenerate the Face before applying artwork processing.");
        var originalPath = GetOriginalArtworkPath(generatedPath);
        if (!File.Exists(originalPath))
            return FaceArtworkProcessingResult.Failure($"Canonical original artwork was not found at '{originalPath}'. Regenerate the Face with the current Editor before applying artwork processing.");
        using var original = SKBitmap.Decode(originalPath);
        if (original is null)
            return FaceArtworkProcessingResult.Failure($"Canonical original artwork could not be decoded: '{originalPath}'.");
        try
        {
            WriteProcessedArtwork(original, artwork.ProcessingPipeline, generatedPath);
        }
        catch (Exception exception)
        {
            return FaceArtworkProcessingResult.Failure($"Artwork processing failed: {exception.Message}");
        }
        if (!File.Exists(generatedPath))
            return FaceArtworkProcessingResult.Failure($"Processed artwork could not be written to '{generatedPath}'.");
        using var verification = SKBitmap.Decode(generatedPath);
        return verification is null
            ? FaceArtworkProcessingResult.Failure($"Processed artwork was written but could not be read back from '{generatedPath}'.")
            : FaceArtworkProcessingResult.Success;
    }

    internal static string ResolveGeneratedArtworkPath(string generatedArtworkPath, string projectDirectory) =>
        Path.IsPathRooted(generatedArtworkPath)
            ? generatedArtworkPath
            : Path.Combine(projectDirectory, generatedArtworkPath.Replace('/', Path.DirectorySeparatorChar));

    private static void WriteProcessedArtwork(SKBitmap original, ImageProcessingPipelineModel pipeline, string outputPath)
    {
        using var processed = new FaceArtworkProcessingPipeline().Evaluate(original, pipeline);
        using var image = SKImage.FromBitmap(processed);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.Create(outputPath);
        data.SaveTo(stream);
    }

    internal static string GetOriginalArtworkPath(string generatedArtworkPath) =>
        Path.Combine(Path.GetDirectoryName(generatedArtworkPath) ?? string.Empty, $"{Path.GetFileNameWithoutExtension(generatedArtworkPath)}.original{Path.GetExtension(generatedArtworkPath)}");
}
