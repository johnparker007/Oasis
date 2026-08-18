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
    public FaceArtworkProcessingResult RebuildRegisteredImage(
        FaceArtworkModel artwork, string projectDirectory, string outputPath,
        double? targetAspectRatio, out FaceSourceShapeOutputSize outputSize)
    {
        outputSize = default;
        ArgumentNullException.ThrowIfNull(artwork);
        if (artwork.Source.Kind != FaceArtworkSourceKind.RegisteredImage)
            return FaceArtworkProcessingResult.Failure("The artwork source is not a Registered Image.");
        if (string.IsNullOrWhiteSpace(artwork.Source.AssetPath) || Path.IsPathRooted(artwork.Source.AssetPath))
            return FaceArtworkProcessingResult.Failure("Registered image paths must be project-relative authored asset paths.");
        var sourcePath = Path.Combine(projectDirectory, artwork.Source.AssetPath.Replace('/', Path.DirectorySeparatorChar));
        var assetsRoot = Path.GetFullPath(Path.Combine(projectDirectory, "Assets")) + Path.DirectorySeparatorChar;
        if (!Path.GetFullPath(sourcePath).StartsWith(assetsRoot, StringComparison.OrdinalIgnoreCase))
            return FaceArtworkProcessingResult.Failure("Registered images must be stored under the project Assets directory.");
        if (!File.Exists(sourcePath)) return FaceArtworkProcessingResult.Failure($"Registered image was not found at '{sourcePath}'.");
        using var source = SKBitmap.Decode(sourcePath);
        if (source is null) return FaceArtworkProcessingResult.Failure("The registered image could not be decoded.");
        var q = artwork.Source.RegistrationQuad.Normalize();
        FacePointModel Pixel(NormalizedFacePointModel p) => new() { X = p.X * (source.Width - 1), Y = p.Y * (source.Height - 1) };
        var quad = new[] { Pixel(q.TopLeft), Pixel(q.TopRight), Pixel(q.BottomRight), Pixel(q.BottomLeft) };
        outputSize = PerspectiveRectificationService.EstimateOutputSize(quad, targetAspectRatio);
        try
        {
            using var rectified = PerspectiveRectificationService.Rectify(source, quad, outputSize.Width, outputSize.Height);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            WriteBitmap(rectified, GetOriginalArtworkPath(outputPath));
            WriteProcessedArtwork(rectified, artwork.ProcessingPipeline, outputPath);
            return FaceArtworkProcessingResult.Success;
        }
        catch (Exception exception) { return FaceArtworkProcessingResult.Failure($"Registered image rectification failed: {exception.Message}"); }
    }

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

    private static void WriteBitmap(SKBitmap bitmap, string path)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.Create(path);
        data.SaveTo(stream);
    }

    internal static string GetOriginalArtworkPath(string generatedArtworkPath) =>
        Path.Combine(Path.GetDirectoryName(generatedArtworkPath) ?? string.Empty, $"original{Path.GetExtension(generatedArtworkPath)}");
}
