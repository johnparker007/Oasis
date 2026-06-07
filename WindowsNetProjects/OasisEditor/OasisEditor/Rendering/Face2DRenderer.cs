using System.IO;
using SkiaSharp;

namespace OasisEditor.Rendering;

public interface IFace2DRenderer
{
    void Render(SKCanvas canvas, FaceDocumentModel faceDocument, MachineRuntimeState runtimeState, PanelViewportTransform viewportTransform);
}

public sealed class Face2DRenderer : IFace2DRenderer
{
    private readonly IFaceRuntimeStateResolver _runtimeStateResolver;
    private readonly Func<string?, string?> _assetPathResolver;
    private readonly Dictionary<string, SKImage?> _cachedArtworkImages = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SKImage?> _cachedMaskImages = new(StringComparer.OrdinalIgnoreCase);

    public Face2DRenderer()
        : this(FaceRuntimeStateResolver.Instance, ResolveDefaultAssetPath)
    {
    }

    internal Face2DRenderer(IFaceRuntimeStateResolver runtimeStateResolver, Func<string?, string?> assetPathResolver)
    {
        _runtimeStateResolver = runtimeStateResolver ?? throw new ArgumentNullException(nameof(runtimeStateResolver));
        _assetPathResolver = assetPathResolver ?? throw new ArgumentNullException(nameof(assetPathResolver));
    }

    public void Render(SKCanvas canvas, FaceDocumentModel faceDocument, MachineRuntimeState runtimeState, PanelViewportTransform viewportTransform)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(faceDocument);
        ArgumentNullException.ThrowIfNull(runtimeState);

        var elements = faceDocument.Elements;

        foreach (var artwork in elements.OfType<FaceArtworkElement>())
        {
            DrawArtwork(canvas, artwork, viewportTransform);
        }

        DrawLampIllumination(canvas, faceDocument.MaskLayer, elements.OfType<FaceLampWindowElement>(), runtimeState);

        foreach (var reelDisplay in elements.OfType<FaceReelDisplayElement>())
        {
            DrawReelDisplay(canvas, reelDisplay, runtimeState, viewportTransform);
        }

        foreach (var sevenSegmentDisplay in elements.OfType<FaceSevenSegmentDisplayElement>())
        {
            DrawSevenSegmentDisplay(canvas, sevenSegmentDisplay, runtimeState, viewportTransform);
        }

        foreach (var alphaDisplay in elements.OfType<FaceAlphaDisplayElement>())
        {
            DrawAlphaDisplay(canvas, alphaDisplay, runtimeState, viewportTransform);
        }

        foreach (var button in elements.OfType<FaceButtonElement>())
        {
            DrawButton(canvas, button, viewportTransform);
        }
    }

    private void DrawArtwork(SKCanvas canvas, FaceArtworkElement element, PanelViewportTransform viewport)
    {
        var destination = ToRect(element);
        if (destination.Width <= 0f || destination.Height <= 0f)
        {
            return;
        }

        if (!element.IsVisible)
        {
            return;
        }

        if (TryGetArtworkImage(element.AssetPath, out var image))
        {
            canvas.DrawImage(image, ResolveArtworkSourceRect(element, image), destination);
            return;
        }

        using var fillPaint = new SKPaint { Style = SKPaintStyle.Fill, Color = new SKColor(0x33, 0x33, 0x33), IsAntialias = true };
        using var strokePaint = new SKPaint { Style = SKPaintStyle.Stroke, Color = new SKColor(0x66, 0x66, 0x66), StrokeWidth = (float)(1d / viewport.NormalizedZoom), IsAntialias = true };
        canvas.DrawRect(destination, fillPaint);
        canvas.DrawRect(destination, strokePaint);
    }

    private void DrawLampIllumination(SKCanvas canvas, FaceMaskLayerModel? maskLayer, IEnumerable<FaceLampWindowElement> lampWindows, MachineRuntimeState runtimeState)
    {
        if (maskLayer is null || !TryGetMaskImage(maskLayer.AssetPath, out var maskImage))
        {
            return;
        }

        var maskRect = ResolveMaskDestinationRect(maskLayer, maskImage);
        if (maskRect.Width <= 0f || maskRect.Height <= 0f)
        {
            return;
        }

        foreach (var element in lampWindows)
        {
            if (!element.IsVisible || element.Width <= 0d || element.Height <= 0d)
            {
                continue;
            }

            var intensity = Math.Clamp(_runtimeStateResolver.GetLampIntensity(element, runtimeState), 0d, 1d);
            if (intensity <= 0.0001d)
            {
                continue;
            }

            DrawSingleLampIllumination(canvas, maskImage, maskRect, element, intensity);
        }
    }

    private static void DrawSingleLampIllumination(SKCanvas canvas, SKImage maskImage, SKRect maskRect, FaceLampWindowElement element, double intensity)
    {
        // Lamp window bounds define the light source origin only; the mask alpha defines the visible shape.
        var center = new SKPoint((float)(element.X + (element.Width / 2d)), (float)(element.Y + (element.Height / 2d)));
        var radius = ResolveLampIlluminationRadius(maskRect, center);
        if (radius <= 0f)
        {
            return;
        }

        var alpha = (byte)Math.Clamp(28 + (int)(156 * intensity), 0, 184);
        var inner = new SKColor(0xFF, 0xE8, 0x7A, alpha);
        var middle = new SKColor(0xFF, 0xC9, 0x3D, (byte)Math.Clamp(alpha * 0.58d, 0d, 255d));
        var outer = new SKColor(0xFF, 0xA0, 0x20, 0);

        using var shader = SKShader.CreateRadialGradient(
            center,
            radius,
            [inner, middle, outer],
            [0f, 0.38f, 1f],
            SKShaderTileMode.Clamp);
        using var illuminationPaint = new SKPaint
        {
            Style = SKPaintStyle.Fill,
            Shader = shader,
            IsAntialias = true
        };
        using var maskPaint = new SKPaint
        {
            BlendMode = SKBlendMode.DstIn,
            IsAntialias = true,
            FilterQuality = SKFilterQuality.Linear
        };

        canvas.SaveLayer(maskRect, null);
        canvas.DrawRect(maskRect, illuminationPaint);
        canvas.DrawImage(maskImage, SKRect.Create(0f, 0f, maskImage.Width, maskImage.Height), maskRect, maskPaint);
        canvas.Restore();
    }

    private static float ResolveLampIlluminationRadius(SKRect maskRect, SKPoint center)
    {
        var farthestX = Math.Max(Math.Abs(center.X - maskRect.Left), Math.Abs(center.X - maskRect.Right));
        var farthestY = Math.Max(Math.Abs(center.Y - maskRect.Top), Math.Abs(center.Y - maskRect.Bottom));
        var faceRadius = Math.Sqrt((farthestX * farthestX) + (farthestY * farthestY));
        return (float)Math.Max(1d, faceRadius * 0.45d);
    }

    private void DrawReelDisplay(SKCanvas canvas, FaceReelDisplayElement element, MachineRuntimeState runtimeState, PanelViewportTransform viewport)
    {
        var rect = ToRect(element);
        if (rect.Width <= 0f || rect.Height <= 0f)
        {
            return;
        }

        if (!element.IsVisible)
        {
            using var hiddenPaint = new SKPaint { Style = SKPaintStyle.Stroke, Color = new SKColor(0x80, 0x80, 0x80), StrokeWidth = (float)(1d / viewport.NormalizedZoom), IsAntialias = true };
            canvas.DrawRect(rect, hiddenPaint);
            return;
        }

        var position = _runtimeStateResolver.GetReelPosition(element, runtimeState);
        ReelElementRenderer.RenderReelDisplay(canvas, rect, element.AssetPath, position, element.Stops.GetValueOrDefault(1), element.VisibleScale);
    }

    private void DrawSevenSegmentDisplay(SKCanvas canvas, FaceSevenSegmentDisplayElement element, MachineRuntimeState runtimeState, PanelViewportTransform viewport)
    {
        var rect = ToRect(element);
        if (rect.Width <= 0f || rect.Height <= 0f)
        {
            return;
        }

        if (!element.IsVisible)
        {
            using var hiddenPaint = new SKPaint { Style = SKPaintStyle.Stroke, Color = new SKColor(0x80, 0x80, 0x80), StrokeWidth = (float)(1d / viewport.NormalizedZoom), IsAntialias = true };
            canvas.DrawRect(rect, hiddenPaint);
            return;
        }

        var masks = _runtimeStateResolver.GetSevenSegmentCellMasks(element, runtimeState);
        var brightness = _runtimeStateResolver.GetSevenSegmentCellBrightness(element, runtimeState);
        SevenSegmentElementRenderer.RenderSegmentDisplay(canvas, rect, masks, brightness, element.OnColorHex, element.OffColorHex);
    }

    private void DrawAlphaDisplay(SKCanvas canvas, FaceAlphaDisplayElement element, MachineRuntimeState runtimeState, PanelViewportTransform viewport)
    {
        var rect = ToRect(element);
        if (rect.Width <= 0f || rect.Height <= 0f)
        {
            return;
        }

        if (!element.IsVisible)
        {
            using var hiddenPaint = new SKPaint { Style = SKPaintStyle.Stroke, Color = new SKColor(0x80, 0x80, 0x80), StrokeWidth = (float)(1d / viewport.NormalizedZoom), IsAntialias = true };
            canvas.DrawRect(rect, hiddenPaint);
            return;
        }

        var masks = _runtimeStateResolver.GetAlphaCellMasks(element, runtimeState);
        var brightness = _runtimeStateResolver.GetAlphaCellBrightness(element, runtimeState);
        AlphaElementRenderer.RenderAlphaDisplay(
            canvas,
            rect,
            masks,
            brightness,
            element.SegmentDisplayType,
            element.OnColorHex,
            element.OffColorHex,
            element.ShowDecimalPoint,
            element.ShowCommaTail,
            element.IsReversed);
    }

    private static void DrawButton(SKCanvas canvas, FaceButtonElement element, PanelViewportTransform viewport)
    {
        var rect = ToRect(element);
        if (rect.Width <= 0f || rect.Height <= 0f)
        {
            return;
        }

        if (!element.IsVisible)
        {
            using var hiddenPaint = new SKPaint { Style = SKPaintStyle.Stroke, Color = new SKColor(0x80, 0x80, 0x80), StrokeWidth = (float)(1d / viewport.NormalizedZoom), IsAntialias = true };
            canvas.DrawRoundRect(rect, 6f, 6f, hiddenPaint);
            return;
        }

        var hasInput = FaceInputTargetResolver.TryGetInputReference(element, out _);
        var fillColor = hasInput
            ? new SKColor(0x2E, 0x7D, 0x32, 0xD8)
            : new SKColor(0x55, 0x55, 0x55, 0xC8);
        var strokeColor = element.IsLocked
            ? new SKColor(0x9E, 0x9E, 0x9E, 0xF0)
            : hasInput
                ? new SKColor(0x81, 0xC7, 0x84, 0xFF)
                : new SKColor(0xCC, 0xCC, 0xCC, 0xE8);

        using var fillPaint = new SKPaint { Style = SKPaintStyle.Fill, Color = fillColor, IsAntialias = true };
        using var strokePaint = new SKPaint { Style = SKPaintStyle.Stroke, Color = strokeColor, StrokeWidth = (float)(1.5d / viewport.NormalizedZoom), IsAntialias = true };
        canvas.DrawRoundRect(rect, 6f, 6f, fillPaint);
        canvas.DrawRoundRect(rect, 6f, 6f, strokePaint);
    }

    private bool TryGetArtworkImage(string? assetPath, out SKImage image)
    {
        image = default!;
        var resolvedPath = _assetPathResolver(assetPath);
        if (string.IsNullOrWhiteSpace(resolvedPath))
        {
            return false;
        }

        if (!_cachedArtworkImages.TryGetValue(resolvedPath, out var cached))
        {
            cached = LoadImage(resolvedPath);
            _cachedArtworkImages[resolvedPath] = cached;
        }

        if (cached is null)
        {
            return false;
        }

        image = cached;
        return true;
    }

    private bool TryGetMaskImage(string? assetPath, out SKImage image)
    {
        image = default!;
        var resolvedPath = _assetPathResolver(assetPath);
        if (string.IsNullOrWhiteSpace(resolvedPath))
        {
            return false;
        }

        if (!_cachedMaskImages.TryGetValue(resolvedPath, out var cached))
        {
            cached = LoadMaskAlphaImage(resolvedPath);
            _cachedMaskImages[resolvedPath] = cached;
        }

        if (cached is null)
        {
            return false;
        }

        image = cached;
        return true;
    }

    private static SKImage? LoadImage(string resolvedPath)
    {
        if (!File.Exists(resolvedPath))
        {
            return null;
        }

        using var codec = SKCodec.Create(resolvedPath);
        if (codec is null)
        {
            return null;
        }

        using var bitmap = SKBitmap.Decode(codec);
        return bitmap is null ? null : SKImage.FromBitmap(bitmap);
    }

    private static SKImage? LoadMaskAlphaImage(string resolvedPath)
    {
        if (!File.Exists(resolvedPath))
        {
            return null;
        }

        using var source = SKBitmap.Decode(resolvedPath);
        if (source is null)
        {
            return null;
        }

        using var alphaMask = new SKBitmap(source.Width, source.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        for (var y = 0; y < source.Height; y++)
        {
            for (var x = 0; x < source.Width; x++)
            {
                var pixel = source.GetPixel(x, y);
                var alpha = (byte)Math.Clamp(Math.Max(pixel.Red, Math.Max(pixel.Green, pixel.Blue)) * (pixel.Alpha / 255d), 0d, 255d);
                alphaMask.SetPixel(x, y, new SKColor(0xFF, 0xFF, 0xFF, alpha));
            }
        }

        return SKImage.FromBitmap(alphaMask);
    }

    private static string? ResolveDefaultAssetPath(string? assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath))
        {
            return null;
        }

        var candidate = assetPath.Trim();
        if (Path.IsPathRooted(candidate))
        {
            return candidate;
        }

        if (string.IsNullOrWhiteSpace(PanelElementFactory.ProjectDirectoryPath))
        {
            return null;
        }

        var relativePath = candidate
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);
        return Path.GetFullPath(Path.Combine(PanelElementFactory.ProjectDirectoryPath, relativePath));
    }

    private static SKRect ResolveMaskDestinationRect(FaceMaskLayerModel maskLayer, SKImage maskImage)
    {
        var width = maskLayer.Width > 0 ? maskLayer.Width : maskImage.Width;
        var height = maskLayer.Height > 0 ? maskLayer.Height : maskImage.Height;
        return SKRect.Create(0f, 0f, width, height);
    }

    private static SKRect ResolveArtworkSourceRect(FaceArtworkElement element, SKImage image)
    {
        var sourceRegion = element.SourceRegion;
        var sourceBounds = element.Provenance?.SourceElementBounds;
        if (sourceRegion is null || sourceBounds is null || sourceBounds.Width <= 0d || sourceBounds.Height <= 0d)
        {
            return SKRect.Create(0f, 0f, image.Width, image.Height);
        }

        var scaleX = image.Width / sourceBounds.Width;
        var scaleY = image.Height / sourceBounds.Height;
        var x = (sourceRegion.X - sourceBounds.X) * scaleX;
        var y = (sourceRegion.Y - sourceBounds.Y) * scaleY;
        var width = sourceRegion.Width * scaleX;
        var height = sourceRegion.Height * scaleY;
        var left = (float)Math.Clamp(x, 0d, image.Width);
        var top = (float)Math.Clamp(y, 0d, image.Height);
        var right = (float)Math.Clamp(x + width, left, image.Width);
        var bottom = (float)Math.Clamp(y + height, top, image.Height);
        return new SKRect(left, top, right, bottom);
    }

    private static SKRect ToRect(FaceElementModel element)
    {
        return SKRect.Create((float)element.X, (float)element.Y, (float)Math.Max(0d, element.Width), (float)Math.Max(0d, element.Height));
    }
}
