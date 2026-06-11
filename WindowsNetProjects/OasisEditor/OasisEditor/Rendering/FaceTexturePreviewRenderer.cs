using System.Diagnostics;
using System.IO;
using SkiaSharp;

namespace OasisEditor.Rendering;

public interface IFaceTexturePreviewRenderer
{
    FaceTexturePreviewRenderResult Render(FaceDocumentModel faceDocument, MachineRuntimeState runtimeState);
}

public sealed class FaceTexturePreviewRenderer : IFaceTexturePreviewRenderer, IDisposable
{
    private readonly Func<string?, string?> _assetPathResolver;
    private readonly FaceTexturePreviewSettings _settings;
    private readonly object _syncRoot = new();
    private FaceTexturePreviewRenderCache? _cache;
    private FaceTexturePreviewDiagnostics _lastDiagnostics = FaceTexturePreviewDiagnostics.Empty;

    public FaceTexturePreviewRenderer(Func<string?, string?> assetPathResolver)
        : this(assetPathResolver, FaceTexturePreviewSettings.Default)
    {
    }

    public FaceTexturePreviewRenderer(Func<string?, string?> assetPathResolver, FaceTexturePreviewSettings settings)
    {
        _assetPathResolver = assetPathResolver ?? throw new ArgumentNullException(nameof(assetPathResolver));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    public FaceTexturePreviewDiagnostics LastDiagnostics
    {
        get
        {
            lock (_syncRoot)
            {
                return _lastDiagnostics;
            }
        }
    }

    public FaceTexturePreviewRenderResult Render(FaceDocumentModel faceDocument, MachineRuntimeState runtimeState)
    {
        ArgumentNullException.ThrowIfNull(faceDocument);
        ArgumentNullException.ThrowIfNull(runtimeState);

        var stopwatch = Stopwatch.StartNew();
        lock (_syncRoot)
        {
            if (!TryGetOrCreateCache(faceDocument.RuntimeRenderAssets, out var cache, out var fallbackReason, out var loadMilliseconds, out var precomputeMilliseconds))
            {
                _lastDiagnostics = new FaceTexturePreviewDiagnostics(false, false, false, fallbackReason, loadMilliseconds, precomputeMilliseconds, 0d, 0d);
                TraceDiagnostics(_lastDiagnostics);
                return FaceTexturePreviewRenderResult.Fallback(fallbackReason);
            }

            var lampLookup = BuildLampLookup(runtimeState);
            var lampSignature = ComputeLampSignature(lampLookup);
            var reusedComposition = cache.HasComposedFrame && lampSignature == cache.LampSignature;
            var composeMilliseconds = 0d;
            if (!reusedComposition)
            {
                var composeStopwatch = Stopwatch.StartNew();
                ComposeFrame(cache, lampLookup, lampSignature);
                composeStopwatch.Stop();
                composeMilliseconds = composeStopwatch.Elapsed.TotalMilliseconds;
            }

            stopwatch.Stop();
            _lastDiagnostics = new FaceTexturePreviewDiagnostics(true, cache.WasReusedForLastRequest, reusedComposition, null, loadMilliseconds, precomputeMilliseconds, composeMilliseconds, stopwatch.Elapsed.TotalMilliseconds);
            TraceDiagnostics(_lastDiagnostics);
            return FaceTexturePreviewRenderResult.FromCachedBitmap(cache.OutputBitmap);
        }
    }

    private bool TryGetOrCreateCache(
        FaceRuntimeRenderAssetsModel? assets,
        out FaceTexturePreviewRenderCache cache,
        out string fallbackReason,
        out double loadMilliseconds,
        out double precomputeMilliseconds)
    {
        cache = default!;
        loadMilliseconds = 0d;
        precomputeMilliseconds = 0d;
        if (!TryCreateCacheKey(assets, out var cacheKey, out fallbackReason))
        {
            DisposeCache();
            return false;
        }

        if (_cache is not null && _cache.Key.Equals(cacheKey))
        {
            _cache.WasReusedForLastRequest = true;
            cache = _cache;
            fallbackReason = string.Empty;
            return true;
        }

        DisposeCache();
        var loadStopwatch = Stopwatch.StartNew();
        if (!TryLoadTextures(cacheKey, out var textures, out fallbackReason))
        {
            loadStopwatch.Stop();
            loadMilliseconds = loadStopwatch.Elapsed.TotalMilliseconds;
            return false;
        }

        loadStopwatch.Stop();
        loadMilliseconds = loadStopwatch.Elapsed.TotalMilliseconds;

        using (textures)
        {
            var precomputeStopwatch = Stopwatch.StartNew();
            if (!TryPrecompute(cacheKey, textures, out cache, out fallbackReason))
            {
                precomputeStopwatch.Stop();
                precomputeMilliseconds = precomputeStopwatch.Elapsed.TotalMilliseconds;
                cache?.Dispose();
                cache = default!;
                return false;
            }

            precomputeStopwatch.Stop();
            precomputeMilliseconds = precomputeStopwatch.Elapsed.TotalMilliseconds;
        }

        cache.WasReusedForLastRequest = false;
        _cache = cache;
        return true;
    }

    private bool TryCreateCacheKey(FaceRuntimeRenderAssetsModel? assets, out FaceTexturePreviewCacheKey cacheKey, out string fallbackReason)
    {
        cacheKey = default;
        if (assets is null)
        {
            fallbackReason = "missing runtime render assets";
            return false;
        }

        if (assets.Width <= 0 || assets.Height <= 0)
        {
            fallbackReason = $"invalid runtime texture dimensions: {assets.Width}x{assets.Height}";
            return false;
        }

        if (!TryCreateTextureStamp(assets.ArtworkPath, "artwork", out var artwork, out fallbackReason)
            || !TryCreateTextureStamp(assets.MaskPath, "mask", out var mask, out fallbackReason)
            || !TryCreateTextureStamp(assets.TrayIdPath, "trayId", out var trayId, out fallbackReason)
            || !TryCreateTextureStamp(assets.LampIds0Path, "lampIds0", out var lampIds0, out fallbackReason)
            || !TryCreateTextureStamp(assets.LampWeights0Path, "lampWeights0", out var lampWeights0, out fallbackReason))
        {
            return false;
        }

        cacheKey = new FaceTexturePreviewCacheKey(
            assets.Width,
            assets.Height,
            artwork,
            mask,
            trayId,
            lampIds0,
            lampWeights0,
            Math.Clamp(_settings.AmbientStrength, 0d, 4d),
            Math.Clamp(_settings.EmissionStrength, 0d, 8d),
            Math.Clamp(_settings.MaskStrength, 0d, 4d),
            Math.Clamp(_settings.LampIds0ChannelCount, 1, 4));
        fallbackReason = string.Empty;
        return true;
    }

    private bool TryCreateTextureStamp(string? assetPath, string textureName, out FaceTextureStamp stamp, out string fallbackReason)
    {
        stamp = default;
        if (string.IsNullOrWhiteSpace(assetPath))
        {
            fallbackReason = $"missing texture path: {textureName}";
            return false;
        }

        var resolvedPath = _assetPathResolver(assetPath);
        if (string.IsNullOrWhiteSpace(resolvedPath))
        {
            fallbackReason = $"missing texture path: {textureName} ({assetPath})";
            return false;
        }

        if (!File.Exists(resolvedPath))
        {
            fallbackReason = $"missing texture file: {textureName} ({resolvedPath})";
            return false;
        }

        var fileInfo = new FileInfo(resolvedPath);
        stamp = new FaceTextureStamp(assetPath, Path.GetFullPath(resolvedPath), fileInfo.Length, fileInfo.LastWriteTimeUtc.Ticks);
        fallbackReason = string.Empty;
        return true;
    }

    private static bool TryLoadTextures(FaceTexturePreviewCacheKey cacheKey, out FaceTexturePreviewTextures textures, out string fallbackReason)
    {
        textures = default!;
        if (!TryLoadTexture(cacheKey.Artwork.ResolvedPath, "artwork", out var artwork, out fallbackReason))
        {
            return false;
        }

        if (!TryLoadTexture(cacheKey.Mask.ResolvedPath, "mask", out var mask, out fallbackReason))
        {
            artwork.Dispose();
            return false;
        }

        if (!TryLoadTexture(cacheKey.TrayId.ResolvedPath, "trayId", out var trayId, out fallbackReason))
        {
            DisposeAll(artwork, mask);
            return false;
        }

        if (!TryLoadTexture(cacheKey.LampIds0.ResolvedPath, "lampIds0", out var lampIds0, out fallbackReason))
        {
            DisposeAll(artwork, mask, trayId);
            return false;
        }

        if (!TryLoadTexture(cacheKey.LampWeights0.ResolvedPath, "lampWeights0", out var lampWeights0, out fallbackReason))
        {
            DisposeAll(artwork, mask, trayId, lampIds0);
            return false;
        }

        if (artwork.Width != cacheKey.Width || artwork.Height != cacheKey.Height)
        {
            fallbackReason = $"dimension mismatch: artwork is {artwork.Width}x{artwork.Height}, runtime assets declare {cacheKey.Width}x{cacheKey.Height}";
            DisposeAll(artwork, mask, trayId, lampIds0, lampWeights0);
            return false;
        }

        if (!HaveMatchingDimensions(artwork, mask, trayId, lampIds0, lampWeights0))
        {
            fallbackReason = $"dimension mismatch: artwork={FormatDimensions(artwork)}, mask={FormatDimensions(mask)}, trayId={FormatDimensions(trayId)}, lampIds0={FormatDimensions(lampIds0)}, lampWeights0={FormatDimensions(lampWeights0)}";
            DisposeAll(artwork, mask, trayId, lampIds0, lampWeights0);
            return false;
        }

        textures = new FaceTexturePreviewTextures(artwork, mask, trayId, lampIds0, lampWeights0);
        fallbackReason = string.Empty;
        return true;
    }

    private static bool TryLoadTexture(string resolvedPath, string textureName, out SKBitmap bitmap, out string fallbackReason)
    {
        bitmap = SKBitmap.Decode(resolvedPath);
        if (bitmap is null)
        {
            fallbackReason = $"failed texture load: {textureName} ({resolvedPath})";
            return false;
        }

        if (bitmap.Width <= 0 || bitmap.Height <= 0 || bitmap.ColorType == SKColorType.Unknown)
        {
            bitmap.Dispose();
            bitmap = default!;
            fallbackReason = $"unsupported or invalid texture format: {textureName} ({resolvedPath})";
            return false;
        }

        fallbackReason = string.Empty;
        return true;
    }

    private bool TryPrecompute(FaceTexturePreviewCacheKey cacheKey, FaceTexturePreviewTextures textures, out FaceTexturePreviewRenderCache cache, out string fallbackReason)
    {
        var pixelCount = textures.Width * textures.Height;
        var ambientPixels = new byte[pixelCount * 4];
        var artworkRgb = new byte[pixelCount * 3];
        var maskValues = new byte[pixelCount];
        var lampIds = new byte[pixelCount * cacheKey.LampIds0ChannelCount];
        var lampWeights = new byte[pixelCount * cacheKey.LampIds0ChannelCount];

        for (var y = 0; y < textures.Height; y++)
        {
            for (var x = 0; x < textures.Width; x++)
            {
                var pixelIndex = (y * textures.Width) + x;
                var rgbaIndex = pixelIndex * 4;
                var rgbIndex = pixelIndex * 3;
                var artwork = textures.Artwork.GetPixel(x, y);
                artworkRgb[rgbIndex] = artwork.Red;
                artworkRgb[rgbIndex + 1] = artwork.Green;
                artworkRgb[rgbIndex + 2] = artwork.Blue;
                ambientPixels[rgbaIndex] = ScaleChannel(artwork.Red, cacheKey.AmbientStrength);
                ambientPixels[rgbaIndex + 1] = ScaleChannel(artwork.Green, cacheKey.AmbientStrength);
                ambientPixels[rgbaIndex + 2] = ScaleChannel(artwork.Blue, cacheKey.AmbientStrength);
                ambientPixels[rgbaIndex + 3] = artwork.Alpha;
                maskValues[pixelIndex] = ResolveMaskByte(textures.Mask.GetPixel(x, y), cacheKey.MaskStrength);

                var ids = textures.LampIds0.GetPixel(x, y);
                var weights = textures.LampWeights0.GetPixel(x, y);
                WriteChannels(lampIds, pixelIndex, cacheKey.LampIds0ChannelCount, ids);
                WriteChannels(lampWeights, pixelIndex, cacheKey.LampIds0ChannelCount, weights);
            }
        }

        cache = new FaceTexturePreviewRenderCache(cacheKey, textures.Width, textures.Height, ambientPixels, artworkRgb, maskValues, lampIds, lampWeights);
        fallbackReason = string.Empty;
        return true;
    }

    private static void WriteChannels(byte[] target, int pixelIndex, int channelCount, SKColor color)
    {
        var offset = pixelIndex * channelCount;
        target[offset] = color.Red;
        if (channelCount >= 2)
        {
            target[offset + 1] = color.Green;
        }

        if (channelCount >= 3)
        {
            target[offset + 2] = color.Blue;
        }

        if (channelCount >= 4)
        {
            target[offset + 3] = color.Alpha;
        }
    }

    private static double[] BuildLampLookup(MachineRuntimeState runtimeState)
    {
        var lookup = new double[256];
        foreach (var entry in runtimeState.LampIntensityByMachineObjectId)
        {
            if (!MachineObjectReference.TryParse(entry.Key, out var reference)
                || reference.Kind != MachineObjectKind.Lamp
                || !int.TryParse(reference.Id, out var lampId)
                || lampId <= 0
                || lampId > 255)
            {
                continue;
            }

            lookup[lampId] = Math.Clamp(entry.Value, 0d, 1d);
        }

        return lookup;
    }

    private static long ComputeLampSignature(double[] lampLookup)
    {
        const long fnvOffsetBasis = unchecked((long)1469598103934665603UL);
        const long fnvPrime = 1099511628211L;
        var hash = fnvOffsetBasis;
        for (var lampId = 1; lampId < lampLookup.Length; lampId++)
        {
            var quantized = (byte)Math.Clamp(Math.Round(lampLookup[lampId] * 255d, MidpointRounding.AwayFromZero), 0d, 255d);
            hash ^= ((long)lampId << 8) | quantized;
            hash *= fnvPrime;
        }

        return hash;
    }

    private static unsafe void ComposeFrame(FaceTexturePreviewRenderCache cache, double[] lampLookup, long lampSignature)
    {
        var output = cache.OutputBitmap;
        var pixels = (byte*)output.GetPixels().ToPointer();
        var rowBytes = output.RowBytes;
        var channelCount = cache.Key.LampIds0ChannelCount;
        for (var y = 0; y < cache.Height; y++)
        {
            var row = pixels + (y * rowBytes);
            for (var x = 0; x < cache.Width; x++)
            {
                var pixelIndex = (y * cache.Width) + x;
                var rgbaIndex = pixelIndex * 4;
                var rgbIndex = pixelIndex * 3;
                var visibleLight = 0d;
                var lampOffset = pixelIndex * channelCount;
                for (var channel = 0; channel < channelCount; channel++)
                {
                    var lampId = cache.LampIds[lampOffset + channel];
                    var weight = cache.LampWeights[lampOffset + channel];
                    if (lampId == 0 || weight == 0)
                    {
                        continue;
                    }

                    visibleLight += lampLookup[lampId] * (weight / 255d);
                }

                var light = (cache.MaskValues[pixelIndex] / 255d) * visibleLight * cache.Key.EmissionStrength;
                var target = row + (x * 4);
                if (light <= 0.000001d)
                {
                    target[0] = cache.AmbientPixels[rgbaIndex];
                    target[1] = cache.AmbientPixels[rgbaIndex + 1];
                    target[2] = cache.AmbientPixels[rgbaIndex + 2];
                    target[3] = cache.AmbientPixels[rgbaIndex + 3];
                    continue;
                }

                var multiplier = cache.Key.AmbientStrength + light;
                target[0] = ScaleChannel(cache.ArtworkRgb[rgbIndex], multiplier);
                target[1] = ScaleChannel(cache.ArtworkRgb[rgbIndex + 1], multiplier);
                target[2] = ScaleChannel(cache.ArtworkRgb[rgbIndex + 2], multiplier);
                target[3] = cache.AmbientPixels[rgbaIndex + 3];
            }
        }

        cache.LampSignature = lampSignature;
        cache.HasComposedFrame = true;
    }

    private static byte ResolveMaskByte(SKColor maskPixel, double maskStrength)
    {
        var grayscale = Math.Max(maskPixel.Red, Math.Max(maskPixel.Green, maskPixel.Blue)) / 255d;
        var value = grayscale * (maskPixel.Alpha / 255d) * maskStrength;
        return (byte)Math.Clamp(Math.Round(value * 255d, MidpointRounding.AwayFromZero), 0d, 255d);
    }

    private static byte ScaleChannel(byte channel, double multiplier)
    {
        return (byte)Math.Clamp(Math.Round(channel * multiplier, MidpointRounding.AwayFromZero), 0d, 255d);
    }

    private static bool HaveMatchingDimensions(SKBitmap first, params SKBitmap[] rest)
    {
        return rest.All(bitmap => bitmap.Width == first.Width && bitmap.Height == first.Height);
    }

    private static string FormatDimensions(SKBitmap bitmap)
    {
        return $"{bitmap.Width}x{bitmap.Height}";
    }

    private static void DisposeAll(params SKBitmap?[] bitmaps)
    {
        foreach (var bitmap in bitmaps)
        {
            bitmap?.Dispose();
        }
    }

    private void TraceDiagnostics(FaceTexturePreviewDiagnostics diagnostics)
    {
        if (!_settings.EnableDiagnostics)
        {
            return;
        }

        if (!diagnostics.Rendered)
        {
            Trace.WriteLine($"Face texture preview fallback after {diagnostics.TotalMilliseconds:0.00} ms: {diagnostics.FallbackReason}");
            return;
        }

        Trace.WriteLine(
            $"Face texture preview timings: textureCacheLoad={diagnostics.TextureCacheLoadMilliseconds:0.00} ms, "
            + $"precompute={diagnostics.PrecomputeMilliseconds:0.00} ms, "
            + $"compose={diagnostics.ComposeMilliseconds:0.00} ms, "
            + $"drawCachedImage=pending-canvas-draw, "
            + $"reusedTextures={diagnostics.ReusedTextureCache}, "
            + $"reusedComposition={diagnostics.ReusedComposition}");
    }

    private void DisposeCache()
    {
        _cache?.Dispose();
        _cache = null;
    }

    public void Dispose()
    {
        lock (_syncRoot)
        {
            DisposeCache();
        }
    }

    private sealed class FaceTexturePreviewTextures : IDisposable
    {
        public FaceTexturePreviewTextures(SKBitmap artwork, SKBitmap mask, SKBitmap trayId, SKBitmap lampIds0, SKBitmap lampWeights0)
        {
            Artwork = artwork;
            Mask = mask;
            TrayId = trayId;
            LampIds0 = lampIds0;
            LampWeights0 = lampWeights0;
        }

        public SKBitmap Artwork { get; }
        public SKBitmap Mask { get; }
        public SKBitmap TrayId { get; }
        public SKBitmap LampIds0 { get; }
        public SKBitmap LampWeights0 { get; }
        public int Width => Artwork.Width;
        public int Height => Artwork.Height;

        public void Dispose()
        {
            Artwork.Dispose();
            Mask.Dispose();
            TrayId.Dispose();
            LampIds0.Dispose();
            LampWeights0.Dispose();
        }
    }

    private sealed class FaceTexturePreviewRenderCache : IDisposable
    {
        public FaceTexturePreviewRenderCache(FaceTexturePreviewCacheKey key, int width, int height, byte[] ambientPixels, byte[] artworkRgb, byte[] maskValues, byte[] lampIds, byte[] lampWeights)
        {
            Key = key;
            Width = width;
            Height = height;
            AmbientPixels = ambientPixels;
            ArtworkRgb = artworkRgb;
            MaskValues = maskValues;
            LampIds = lampIds;
            LampWeights = lampWeights;
            OutputBitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        }

        public FaceTexturePreviewCacheKey Key { get; }
        public int Width { get; }
        public int Height { get; }
        public byte[] AmbientPixels { get; }
        public byte[] ArtworkRgb { get; }
        public byte[] MaskValues { get; }
        public byte[] LampIds { get; }
        public byte[] LampWeights { get; }
        public SKBitmap OutputBitmap { get; }
        public bool WasReusedForLastRequest { get; set; }
        public bool HasComposedFrame { get; set; }
        public long LampSignature { get; set; }

        public void Dispose()
        {
            OutputBitmap.Dispose();
        }
    }
}

public sealed class FaceTexturePreviewSettings
{
    public static FaceTexturePreviewSettings Default { get; } = new();

    public double AmbientStrength { get; init; } = 1d;
    public double EmissionStrength { get; init; } = 1.15d;
    public double MaskStrength { get; init; } = 1d;
    public int LampIds0ChannelCount { get; init; } = 1;
    public bool EnableDiagnostics { get; init; }
}

public sealed class FaceTexturePreviewRenderResult : IDisposable
{
    private FaceTexturePreviewRenderResult(bool rendered, SKBitmap? bitmap, string? fallbackReason, bool ownsBitmap)
    {
        Rendered = rendered;
        Bitmap = bitmap;
        FallbackReason = fallbackReason;
        _ownsBitmap = ownsBitmap;
    }

    private readonly bool _ownsBitmap;

    public bool Rendered { get; }
    public SKBitmap? Bitmap { get; }
    public string? FallbackReason { get; }

    public static FaceTexturePreviewRenderResult FromBitmap(SKBitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        return new FaceTexturePreviewRenderResult(true, bitmap, null, true);
    }

    public static FaceTexturePreviewRenderResult FromCachedBitmap(SKBitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        return new FaceTexturePreviewRenderResult(true, bitmap, null, false);
    }

    public static FaceTexturePreviewRenderResult Fallback(string fallbackReason)
    {
        return new FaceTexturePreviewRenderResult(false, null, fallbackReason, false);
    }

    public void Dispose()
    {
        if (_ownsBitmap)
        {
            Bitmap?.Dispose();
        }
    }
}

public readonly record struct FaceTexturePreviewDiagnostics(
    bool Rendered,
    bool ReusedTextureCache,
    bool ReusedComposition,
    string? FallbackReason,
    double TextureCacheLoadMilliseconds,
    double PrecomputeMilliseconds,
    double ComposeMilliseconds,
    double TotalMilliseconds)
{
    public static FaceTexturePreviewDiagnostics Empty { get; } = new(false, false, false, null, 0d, 0d, 0d, 0d);
}

internal readonly record struct FaceTextureStamp(string AssetPath, string ResolvedPath, long Length, long LastWriteTicks);

internal readonly record struct FaceTexturePreviewCacheKey(
    int Width,
    int Height,
    FaceTextureStamp Artwork,
    FaceTextureStamp Mask,
    FaceTextureStamp TrayId,
    FaceTextureStamp LampIds0,
    FaceTextureStamp LampWeights0,
    double AmbientStrength,
    double EmissionStrength,
    double MaskStrength,
    int LampIds0ChannelCount);
