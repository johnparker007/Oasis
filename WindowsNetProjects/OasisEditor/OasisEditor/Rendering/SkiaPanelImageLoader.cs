using SkiaSharp;
using System.Collections.Concurrent;
using System.IO;

namespace OasisEditor.Rendering;

internal static class SkiaPanelImageLoader
{
    private static readonly ConcurrentDictionary<string, CachedPanelImage> CachedPanelImages = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object CacheGate = new();

    public static bool TryGetImage(string? assetPath, out SKImage image)
    {
        image = default!;
        if (!TryResolveAssetPath(assetPath, out var resolvedPath))
        {
            return false;
        }

        var stamp = GetFileStamp(resolvedPath);
        if (!CachedPanelImages.TryGetValue(resolvedPath, out var cached) || cached.Stamp != stamp)
        {
            lock (CacheGate)
            {
                if (!CachedPanelImages.TryGetValue(resolvedPath, out cached) || cached.Stamp != stamp)
                {
                    cached = new CachedPanelImage(stamp, LoadImage(resolvedPath));
                    CachedPanelImages[resolvedPath] = cached;
                }
            }
        }

        if (cached.Image is null) return false;
        image = cached.Image;
        return true;
    }

    private static FileStamp GetFileStamp(string path)
    {
        try
        {
            var info = new FileInfo(path);
            return info.Exists ? new FileStamp(info.Length, info.LastWriteTimeUtc.Ticks) : new FileStamp(-1, 0);
        }
        catch (IOException) { return new FileStamp(-2, 0); }
        catch (UnauthorizedAccessException) { return new FileStamp(-2, 0); }
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

    private readonly record struct FileStamp(long Length, long LastWriteTicks);
    private sealed record CachedPanelImage(FileStamp Stamp, SKImage? Image);
}
