using SkiaSharp;
using System.Collections.Concurrent;
using System.IO;

namespace OasisEditor.Rendering;

internal static class SkiaPanelImageLoader
{
    private static readonly ConcurrentDictionary<string, SKImage?> CachedPanelImages = new(StringComparer.OrdinalIgnoreCase);

    public static bool TryGetImage(string? assetPath, out SKImage image)
    {
        image = default!;
        if (!TryResolveAssetPath(assetPath, out var resolvedPath))
        {
            return false;
        }

        var cached = CachedPanelImages.GetOrAdd(resolvedPath, LoadImage);
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
        return ProjectAssetPathResolver.TryResolveAssetPath(assetPath, out resolvedPath);
    }
}
