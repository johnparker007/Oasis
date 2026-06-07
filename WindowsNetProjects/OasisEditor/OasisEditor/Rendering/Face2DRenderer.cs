using System.IO;
using SkiaSharp;

namespace OasisEditor.Rendering;

public interface IFace2DRenderer
{
    void Render(SKCanvas canvas, IReadOnlyList<FaceElementModel> elements, MachineRuntimeState runtimeState, PanelViewportTransform viewportTransform);
}

public sealed class Face2DRenderer : IFace2DRenderer
{
    private readonly IFaceRuntimeStateResolver _runtimeStateResolver;
    private readonly Func<string?, string?> _assetPathResolver;
    private readonly Dictionary<string, SKImage?> _cachedArtworkImages = new(StringComparer.OrdinalIgnoreCase);

    public Face2DRenderer()
        : this(FaceRuntimeStateResolver.Instance, ResolveDefaultAssetPath)
    {
    }

    internal Face2DRenderer(IFaceRuntimeStateResolver runtimeStateResolver, Func<string?, string?> assetPathResolver)
    {
        _runtimeStateResolver = runtimeStateResolver ?? throw new ArgumentNullException(nameof(runtimeStateResolver));
        _assetPathResolver = assetPathResolver ?? throw new ArgumentNullException(nameof(assetPathResolver));
    }

    public void Render(SKCanvas canvas, IReadOnlyList<FaceElementModel> elements, MachineRuntimeState runtimeState, PanelViewportTransform viewportTransform)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(elements);
        ArgumentNullException.ThrowIfNull(runtimeState);

        foreach (var artwork in elements.OfType<FaceArtworkElement>())
        {
            DrawArtwork(canvas, artwork, viewportTransform);
        }

        foreach (var lampWindow in elements.OfType<FaceLampWindowElement>())
        {
            DrawLampWindow(canvas, lampWindow, runtimeState, viewportTransform);
        }

        foreach (var sevenSegmentDisplay in elements.OfType<FaceSevenSegmentDisplayElement>())
        {
            DrawSevenSegmentDisplay(canvas, sevenSegmentDisplay, runtimeState, viewportTransform);
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

    private void DrawLampWindow(SKCanvas canvas, FaceLampWindowElement element, MachineRuntimeState runtimeState, PanelViewportTransform viewport)
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

        var intensity = Math.Clamp(_runtimeStateResolver.GetLampIntensity(element, runtimeState), 0d, 1d);
        var fillColor = intensity <= 0.0001d
            ? new SKColor(0x20, 0x20, 0x20, 0xA8)
            : new SKColor(0xFF, 0xD5, 0x4F, (byte)Math.Clamp(96 + (int)(159 * intensity), 0, 255));
        var strokeColor = intensity <= 0.0001d
            ? new SKColor(0xB0, 0xB0, 0xB0, 0xD0)
            : new SKColor(0xFF, 0xF1, 0x76, 0xFF);

        using var fillPaint = new SKPaint { Style = SKPaintStyle.Fill, Color = fillColor, IsAntialias = true };
        using var strokePaint = new SKPaint { Style = SKPaintStyle.Stroke, Color = strokeColor, StrokeWidth = (float)(1.5d / viewport.NormalizedZoom), IsAntialias = true };
        canvas.DrawRect(rect, fillPaint);
        canvas.DrawRect(rect, strokePaint);
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
            cached = LoadArtworkImage(resolvedPath);
            _cachedArtworkImages[resolvedPath] = cached;
        }

        if (cached is null)
        {
            return false;
        }

        image = cached;
        return true;
    }

    private static SKImage? LoadArtworkImage(string resolvedPath)
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
