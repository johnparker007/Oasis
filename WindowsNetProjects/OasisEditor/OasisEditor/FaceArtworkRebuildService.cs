using System.IO;
using SkiaSharp;

namespace OasisEditor;

/// <summary>Builds the disposable flattened texture from Face-owned artwork authoring state.</summary>
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

        var absolutePath = Path.IsPathRooted(generatedPath) ? generatedPath : Path.Combine(projectDirectory, generatedPath);
        using var rectified = SKBitmap.Decode(absolutePath);
        if (rectified is null) return generatedPath;
        using (var originalImage = SKImage.FromBitmap(rectified))
        using (var originalData = originalImage.Encode(SKEncodedImageFormat.Png, 100))
        using (var originalStream = File.Create(GetOriginalArtworkPath(absolutePath)))
        {
            originalData.SaveTo(originalStream);
        }
        WriteProcessedArtwork(rectified, artwork.ProcessingPipeline, absolutePath);
        return generatedPath;
    }

    public bool ApplyProcessing(FaceArtworkModel artwork, string projectDirectory)
    {
        ArgumentNullException.ThrowIfNull(artwork);
        if (string.IsNullOrWhiteSpace(artwork.GeneratedAssetPath)) return false;
        var generatedPath = Path.IsPathRooted(artwork.GeneratedAssetPath)
            ? artwork.GeneratedAssetPath
            : Path.Combine(projectDirectory, artwork.GeneratedAssetPath.Replace('/', Path.DirectorySeparatorChar));
        var originalPath = GetOriginalArtworkPath(generatedPath);
        if (!File.Exists(originalPath)) return false;
        using var original = SKBitmap.Decode(originalPath);
        if (original is null) return false;
        WriteProcessedArtwork(original, artwork.ProcessingPipeline, generatedPath);
        return true;
    }

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
