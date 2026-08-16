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
        using var processed = new FaceArtworkProcessingPipeline().Evaluate(rectified, artwork.ProcessingPipeline);
        using var image = SKImage.FromBitmap(processed);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.Create(absolutePath);
        data.SaveTo(stream);
        return generatedPath;
    }
}
