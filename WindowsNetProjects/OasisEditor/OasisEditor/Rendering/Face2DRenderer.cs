using System.IO;
using SkiaSharp;

namespace OasisEditor.Rendering;

public interface IFace2DRenderer
{
    void Render(SKCanvas canvas, FaceDocumentModel document, MachineRuntimeState runtimeState, PanelViewportTransform viewportTransform);
}

public sealed class Face2DRenderer : IFace2DRenderer
{
    private readonly IFaceRuntimeStateResolver _runtimeStateResolver;
    private readonly Func<string?, string?> _assetPathResolver;
    private readonly Dictionary<string, SKImage?> _cachedArtworkImages = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, MaskRenderImages?> _cachedMaskImages = new(StringComparer.OrdinalIgnoreCase);

    public Face2DRenderer()
        : this(FaceRuntimeStateResolver.Instance, ResolveDefaultAssetPath)
    {
    }

    internal Face2DRenderer(IFaceRuntimeStateResolver runtimeStateResolver, Func<string?, string?> assetPathResolver)
    {
        _runtimeStateResolver = runtimeStateResolver ?? throw new ArgumentNullException(nameof(runtimeStateResolver));
        _assetPathResolver = assetPathResolver ?? throw new ArgumentNullException(nameof(assetPathResolver));
    }

    public void Render(SKCanvas canvas, FaceDocumentModel document, MachineRuntimeState runtimeState, PanelViewportTransform viewportTransform)
    {
        ArgumentNullException.ThrowIfNull(document);
        Render(canvas, document.Elements, document.MaskLayer, runtimeState, viewportTransform);
    }

    internal void Render(SKCanvas canvas, IReadOnlyList<FaceElementModel> elements, FaceMaskLayerModel? maskLayer, MachineRuntimeState runtimeState, PanelViewportTransform viewportTransform)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(elements);
        ArgumentNullException.ThrowIfNull(runtimeState);

        foreach (var artwork in elements.OfType<FaceArtworkElement>())
        {
            DrawArtwork(canvas, artwork, viewportTransform);
        }

        DrawMaskLayer(canvas, maskLayer);

        DrawLampIllumination(canvas, elements.OfType<FaceLampWindowElement>(), maskLayer, runtimeState, viewportTransform);

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


    private void DrawMaskLayer(SKCanvas canvas, FaceMaskLayerModel? maskLayer)
    {
        if (!TryGetMaskImages(maskLayer, out var maskImages))
        {
            return;
        }

        var bounds = ResolveMaskBounds(maskLayer, maskImages.AlphaMask);
        if (bounds.Width <= 0f || bounds.Height <= 0f)
        {
            return;
        }

        using var paint = new SKPaint
        {
            BlendMode = SKBlendMode.SrcOver,
            IsAntialias = false,
            FilterQuality = SKFilterQuality.None
        };
        canvas.DrawImage(maskImages.PrintedOverlay, bounds, paint);
    }

    private void DrawLampIllumination(SKCanvas canvas, IEnumerable<FaceLampWindowElement> lampWindows, FaceMaskLayerModel? maskLayer, MachineRuntimeState runtimeState, PanelViewportTransform viewport)
    {
        var lamps = lampWindows.ToArray();
        if (lamps.Length == 0)
        {
            return;
        }

        if (!TryGetMaskImages(maskLayer, out var maskImages))
        {
            foreach (var lampWindow in lamps)
            {
                DrawLampWindow(canvas, lampWindow, runtimeState, viewport);
            }

            return;
        }

        var bounds = ResolveMaskBounds(maskLayer, maskImages.AlphaMask);
        if (bounds.Width <= 0f || bounds.Height <= 0f)
        {
            foreach (var lampWindow in lamps)
            {
                DrawLampWindow(canvas, lampWindow, runtimeState, viewport);
            }

            return;
        }

        using var layerPaint = new SKPaint();
        canvas.SaveLayer(layerPaint);
        foreach (var lampWindow in lamps)
        {
            if (!lampWindow.IsVisible)
            {
                continue;
            }

            DrawLampWindow(canvas, lampWindow, runtimeState, viewport, drawStroke: false);
        }

        using (var maskPaint = new SKPaint { BlendMode = SKBlendMode.DstIn, IsAntialias = false, FilterQuality = SKFilterQuality.None })
        {
            canvas.DrawImage(maskImages.AlphaMask, bounds, maskPaint);
        }

        canvas.Restore();

        foreach (var lampWindow in lamps)
        {
            DrawLampWindowOutline(canvas, lampWindow, runtimeState, viewport);
        }
    }

    private void DrawLampWindow(SKCanvas canvas, FaceLampWindowElement element, MachineRuntimeState runtimeState, PanelViewportTransform viewport, bool drawStroke = true)
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
        canvas.DrawRect(rect, fillPaint);
        if (drawStroke)
        {
            using var strokePaint = new SKPaint { Style = SKPaintStyle.Stroke, Color = strokeColor, StrokeWidth = (float)(1.5d / viewport.NormalizedZoom), IsAntialias = true };
            canvas.DrawRect(rect, strokePaint);
        }
    }

    private void DrawLampWindowOutline(SKCanvas canvas, FaceLampWindowElement element, MachineRuntimeState runtimeState, PanelViewportTransform viewport)
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
        var strokeColor = intensity <= 0.0001d
            ? new SKColor(0xB0, 0xB0, 0xB0, 0xD0)
            : new SKColor(0xFF, 0xF1, 0x76, 0xFF);
        using var strokePaint = new SKPaint { Style = SKPaintStyle.Stroke, Color = strokeColor, StrokeWidth = (float)(1.5d / viewport.NormalizedZoom), IsAntialias = true };
        canvas.DrawRect(rect, strokePaint);
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


    private bool TryGetMaskImages(FaceMaskLayerModel? maskLayer, out MaskRenderImages maskImages)
    {
        maskImages = default!;
        var resolvedPath = _assetPathResolver(maskLayer?.AssetPath);
        if (string.IsNullOrWhiteSpace(resolvedPath))
        {
            return false;
        }

        if (!_cachedMaskImages.TryGetValue(resolvedPath, out var cached))
        {
            cached = LoadMaskImages(resolvedPath);
            _cachedMaskImages[resolvedPath] = cached;
        }

        if (cached is null)
        {
            return false;
        }

        maskImages = cached;
        return true;
    }

    private static MaskRenderImages? LoadMaskImages(string resolvedPath)
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

        using var source = SKBitmap.Decode(codec);
        if (source is null || source.Width <= 0 || source.Height <= 0)
        {
            return null;
        }

        using var alphaMaskBitmap = new SKBitmap(source.Width, source.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var printedOverlayBitmap = new SKBitmap(source.Width, source.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        for (var y = 0; y < source.Height; y++)
        {
            for (var x = 0; x < source.Width; x++)
            {
                var pixel = source.GetPixel(x, y);
                var luminance = (byte)Math.Clamp((int)Math.Round((0.2126 * pixel.Red) + (0.7152 * pixel.Green) + (0.0722 * pixel.Blue)), 0, 255);
                var escapeAlpha = (byte)Math.Clamp((luminance * pixel.Alpha) / 255, 0, 255);
                var printedAlpha = (byte)Math.Clamp(((255 - luminance) * pixel.Alpha * 104) / (255 * 255), 0, 104);
                alphaMaskBitmap.SetPixel(x, y, new SKColor(255, 255, 255, escapeAlpha));
                printedOverlayBitmap.SetPixel(x, y, new SKColor(0, 0, 0, printedAlpha));
            }
        }

        return new MaskRenderImages(SKImage.FromBitmap(alphaMaskBitmap), SKImage.FromBitmap(printedOverlayBitmap));
    }

    private static SKRect ResolveMaskBounds(FaceMaskLayerModel? maskLayer, SKImage maskImage)
    {
        var width = maskLayer is not null && maskLayer.Width > 0 ? maskLayer.Width : maskImage.Width;
        var height = maskLayer is not null && maskLayer.Height > 0 ? maskLayer.Height : maskImage.Height;
        return SKRect.Create(0f, 0f, width, height);
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

    private sealed record MaskRenderImages(SKImage AlphaMask, SKImage PrintedOverlay);
}
