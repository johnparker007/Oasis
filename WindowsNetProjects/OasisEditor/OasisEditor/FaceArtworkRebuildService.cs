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
    public const int MaximumOutputDimension = 16384;
    public const long MaximumOutputPixels = 268_435_456;
    /// <summary>Rectifies and sharpens source artwork, without applying any processing operations.</summary>
    public string? RebuildCorrectionInput(FaceArtworkModel artwork, Panel2DDocumentModel panel,
        PanelFaceSourceShapeModel shape, string? projectDirectory, string correctionInputPath,
        FaceGenerationSettingsModel? generationSettings = null)
    {
        ArgumentNullException.ThrowIfNull(artwork);
        if (artwork.Source.Kind != FaceArtworkSourceKind.Panel2DFaceSourceShape)
            throw new InvalidOperationException("This overload requires Panel2D Face Source Shape artwork.");
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

    /// <summary>Rectifies an authored image once through the shared quality-first rasterizer, then sharpens it.</summary>
    public string? RebuildImageCorrectionInput(FaceArtworkModel artwork, string projectDirectory,
        string correctionInputPath, FaceGenerationSettingsModel? generationSettings = null)
    {
        if (artwork.Source.Kind != FaceArtworkSourceKind.Image || string.IsNullOrWhiteSpace(artwork.Source.AssetPath)) return null;
        var registration = artwork.Geometry.PerspectiveRegistration.Normalize();
        if (!registration.IsValid()) return null;
        var sourcePath = FaceArtworkGeneratedPathService.Resolve(artwork.Source.AssetPath, projectDirectory);
        if (!File.Exists(sourcePath)) return null;
        using var source = SKBitmap.Decode(sourcePath);
        if (source is null) return null;
        var size = FaceSourceShapeTransformService.EstimateRegisteredImageOutputSize(source.Width, source.Height, registration);
        var quad = new[] { registration.TopLeft, registration.TopRight, registration.BottomRight, registration.BottomLeft }
            .Select(point => new FacePointModel { X = point.X * source.Width, Y = point.Y * source.Height }).ToArray();
        using var rectified = PerspectiveRasterizer.Rectify(source, quad, size.Width, size.Height);
        using var sharpened = FaceArtworkSharpeningService.Apply(rectified, generationSettings ?? FaceGenerationSettingsModel.Default);
        var output = FaceArtworkGeneratedPathService.Resolve(correctionInputPath, projectDirectory);
        WriteVerified(sharpened, output);
        return FaceArtworkGeneratedPathService.ToProjectRelative(output, projectDirectory);
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
            var artworkOverride = artwork.Override;
            if (artworkOverride is not { Enabled: true }) { WriteVerified(bitmap, outputPath); return FaceArtworkProcessingResult.Success; }
            if (!artworkOverride.IsValid()) return FaceArtworkProcessingResult.Failure("The enabled Artwork Override recipe is invalid.");
            var overridePath = FaceArtworkGeneratedPathService.Resolve(artworkOverride.AssetPath, projectDirectory);
            using var rawOverlay = SKBitmap.Decode(overridePath);
            if (rawOverlay is null) return FaceArtworkProcessingResult.Failure($"Artwork Override could not be decoded: '{overridePath}'.");
            using var overlay = RectifyOverride(rawOverlay, artworkOverride.PerspectiveRegistration);
            var size = DetermineOutputSize(bitmap.Width, bitmap.Height, artworkOverride, overlay.Width, overlay.Height);
            using var output = new SKBitmap(new SKImageInfo(size.Width, size.Height, SKColorType.Bgra8888, SKAlphaType.Premul));
            using (var canvas = new SKCanvas(output))
            using (var paint = new SKPaint { IsAntialias = true, FilterQuality = SKFilterQuality.High })
            {
                canvas.Clear(SKColors.Transparent);
                canvas.DrawBitmap(bitmap, new SKRect(0, 0, size.Width, size.Height), paint);
                canvas.DrawBitmap(overlay, new SKRect((float)(artworkOverride.X * size.Width), (float)(artworkOverride.Y * size.Height),
                    (float)((artworkOverride.X + artworkOverride.Width) * size.Width),
                    (float)((artworkOverride.Y + artworkOverride.Height) * size.Height)), paint);
                canvas.Flush();
            }
            WriteVerified(output, outputPath);
            return FaceArtworkProcessingResult.Success;
        }
        catch (Exception exception) { return FaceArtworkProcessingResult.Failure($"Artwork output failed: {exception.Message}"); }
    }

    public static (int Width, int Height) DetermineOutputSize(int baseWidth, int baseHeight,
        FaceArtworkOverrideModel artworkOverride, int overrideWidth, int overrideHeight)
    {
        var width = Math.Max(baseWidth, (int)Math.Ceiling(overrideWidth / artworkOverride.Width));
        var height = Math.Max(baseHeight, (int)Math.Ceiling(overrideHeight / artworkOverride.Height));
        if (width > MaximumOutputDimension || height > MaximumOutputDimension || (long)width * height > MaximumOutputPixels)
            throw new InvalidOperationException($"Artwork Output size {width} x {height} exceeds the safe generation limit.");
        return (width, height);
    }

    /// <summary>Rectifies only the useful registered region; override calibration remains downstream and independent of Base.</summary>
    internal static SKBitmap RectifyOverride(SKBitmap source, FacePerspectiveRegistrationModel registration)
    {
        var normalized = registration.Normalize();
        if (!normalized.IsValid()) throw new InvalidOperationException("The Artwork Override perspective registration is invalid.");
        var size = FaceSourceShapeTransformService.EstimateRegisteredImageOutputSize(source.Width, source.Height, normalized);
        if (size.Width > MaximumOutputDimension || size.Height > MaximumOutputDimension || (long)size.Width * size.Height > MaximumOutputPixels)
            throw new InvalidOperationException($"Rectified Artwork Override size {size.Width} x {size.Height} exceeds the safe generation limit.");
        var quad = new[] { normalized.TopLeft, normalized.TopRight, normalized.BottomRight, normalized.BottomLeft }
            .Select(point => new FacePointModel { X = point.X * source.Width, Y = point.Y * source.Height }).ToArray();
        return PerspectiveRasterizer.Rectify(source, quad, size.Width, size.Height);
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
