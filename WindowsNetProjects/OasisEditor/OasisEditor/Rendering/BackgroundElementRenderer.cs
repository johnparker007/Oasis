using SkiaSharp;
using System.Collections.Concurrent;
using System.IO;

namespace OasisEditor.Rendering;

internal sealed class BackgroundElementRenderer : IPanelElementRenderer
{
    private static readonly ConcurrentDictionary<string, SKImage?> CachedBackgroundImages = new(StringComparer.OrdinalIgnoreCase);

    public PanelElementKind Kind => PanelElementKind.Background;

    public void Render(in PanelElementRenderContext context, PanelElementModel element)
    {
        var bounds = SKRect.Create((float)element.X, (float)element.Y, (float)element.Width, (float)element.Height);
        if (bounds.Width <= 0f || bounds.Height <= 0f)
        {
            return;
        }

        if (TryGetBackgroundImage(element.AssetPath, out var backgroundImage))
        {
            context.Canvas.DrawImage(backgroundImage, bounds);
            return;
        }

        using var paint = new SKPaint
        {
            Color = SkiaColorParser.ParseOrDefault(element.OnColorHex, new SKColor(0x2B, 0x2B, 0x2B)),
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        context.Canvas.DrawRect(bounds, paint);
    }

    private static bool TryGetBackgroundImage(string? assetPath, out SKImage image)
    {
        image = default!;
        if (!TryResolveAssetPath(assetPath, out var resolvedPath))
        {
            return false;
        }

        var cached = CachedBackgroundImages.GetOrAdd(resolvedPath, LoadImage);
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

    private static bool TryResolveAssetPath(string? assetPath, out string resolvedPath)
    {
        resolvedPath = string.Empty;
        if (string.IsNullOrWhiteSpace(assetPath))
        {
            return false;
        }

        var candidate = assetPath.Trim();
        if (Path.IsPathRooted(candidate))
        {
            resolvedPath = candidate;
            return true;
        }

        if (string.IsNullOrWhiteSpace(PanelElementFactory.ProjectDirectoryPath))
        {
            return false;
        }

        var relativePath = candidate
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);
        resolvedPath = Path.GetFullPath(Path.Combine(PanelElementFactory.ProjectDirectoryPath, relativePath));
        return true;
    }
}
