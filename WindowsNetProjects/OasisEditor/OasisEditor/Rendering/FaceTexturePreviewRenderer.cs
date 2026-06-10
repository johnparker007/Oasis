using System.IO;
using SkiaSharp;

namespace OasisEditor.Rendering;

public interface IFaceTexturePreviewRenderer
{
    FaceTexturePreviewRenderResult Render(FaceDocumentModel faceDocument, MachineRuntimeState runtimeState);
}

public sealed class FaceTexturePreviewRenderer : IFaceTexturePreviewRenderer
{
    private readonly Func<string?, string?> _assetPathResolver;
    private readonly FaceTexturePreviewSettings _settings;

    public FaceTexturePreviewRenderer(Func<string?, string?> assetPathResolver)
        : this(assetPathResolver, FaceTexturePreviewSettings.Default)
    {
    }

    public FaceTexturePreviewRenderer(Func<string?, string?> assetPathResolver, FaceTexturePreviewSettings settings)
    {
        _assetPathResolver = assetPathResolver ?? throw new ArgumentNullException(nameof(assetPathResolver));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    public FaceTexturePreviewRenderResult Render(FaceDocumentModel faceDocument, MachineRuntimeState runtimeState)
    {
        ArgumentNullException.ThrowIfNull(faceDocument);
        ArgumentNullException.ThrowIfNull(runtimeState);

        if (!TryLoadTextures(faceDocument.RuntimeRenderAssets, out var textures, out var fallbackReason))
        {
            return FaceTexturePreviewRenderResult.Fallback(fallbackReason);
        }

        using (textures)
        {
            var output = new SKBitmap(textures.Width, textures.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
            RenderPixels(output, textures, runtimeState);
            return FaceTexturePreviewRenderResult.FromBitmap(output);
        }
    }

    private void RenderPixels(SKBitmap output, FaceTexturePreviewTextures textures, MachineRuntimeState runtimeState)
    {
        var ambient = Math.Clamp(_settings.AmbientStrength, 0d, 4d);
        var emission = Math.Clamp(_settings.EmissionStrength, 0d, 8d);
        var maskStrength = Math.Clamp(_settings.MaskStrength, 0d, 4d);

        for (var y = 0; y < textures.Height; y++)
        {
            for (var x = 0; x < textures.Width; x++)
            {
                var artwork = textures.Artwork.GetPixel(x, y);
                var mask = ResolveMaskValue(textures.Mask.GetPixel(x, y)) * maskStrength;
                var visibleLight = mask * ResolveLampContribution(textures.LampIds0.GetPixel(x, y), textures.LampWeights0.GetPixel(x, y), runtimeState, _settings.LampIds0ChannelCount);
                var litMultiplier = ambient + (visibleLight * emission);

                output.SetPixel(
                    x,
                    y,
                    new SKColor(
                        ScaleChannel(artwork.Red, litMultiplier),
                        ScaleChannel(artwork.Green, litMultiplier),
                        ScaleChannel(artwork.Blue, litMultiplier),
                        artwork.Alpha));
            }
        }
    }

    private static double ResolveLampContribution(SKColor lampIds, SKColor lampWeights, MachineRuntimeState runtimeState, int channelCount)
    {
        // Phase 3a populates only the first channel. Keep the channel loop explicit so
        // additional lampIds0/lampWeights0 channels, and later lampIds1/lampWeights1, can
        // be enabled without changing the lighting equation.
        var clampedChannelCount = Math.Clamp(channelCount, 1, 4);
        var contribution = ResolveLampChannel(lampIds.Red, lampWeights.Red, runtimeState);
        if (clampedChannelCount >= 2)
        {
            contribution += ResolveLampChannel(lampIds.Green, lampWeights.Green, runtimeState);
        }

        if (clampedChannelCount >= 3)
        {
            contribution += ResolveLampChannel(lampIds.Blue, lampWeights.Blue, runtimeState);
        }

        if (clampedChannelCount >= 4)
        {
            contribution += ResolveLampChannel(lampIds.Alpha, lampWeights.Alpha, runtimeState);
        }

        return contribution;
    }

    private static double ResolveLampChannel(byte lampId, byte weight, MachineRuntimeState runtimeState)
    {
        if (lampId == 0 || weight == 0)
        {
            return 0d;
        }

        var lampState = Math.Clamp(runtimeState.GetLampIntensity(MachineObjectReference.Lamp(lampId)), 0d, 1d);
        return lampState * (weight / 255d);
    }

    private static double ResolveMaskValue(SKColor maskPixel)
    {
        var grayscale = Math.Max(maskPixel.Red, Math.Max(maskPixel.Green, maskPixel.Blue)) / 255d;
        return grayscale * (maskPixel.Alpha / 255d);
    }

    private static byte ScaleChannel(byte channel, double multiplier)
    {
        return (byte)Math.Clamp(Math.Round(channel * multiplier, MidpointRounding.AwayFromZero), 0d, 255d);
    }

    private bool TryLoadTextures(FaceRuntimeRenderAssetsModel? assets, out FaceTexturePreviewTextures textures, out string fallbackReason)
    {
        textures = default!;
        if (assets is null)
        {
            fallbackReason = "missing runtime render assets";
            return false;
        }

        if (!TryLoadTexture(assets.ArtworkPath, "artwork", out var artwork, out fallbackReason))
        {
            return false;
        }

        if (!TryLoadTexture(assets.MaskPath, "mask", out var mask, out fallbackReason))
        {
            artwork.Dispose();
            return false;
        }

        if (!TryLoadTexture(assets.TrayIdPath, "trayId", out var trayId, out fallbackReason))
        {
            DisposeAll(artwork, mask);
            return false;
        }

        if (!TryLoadTexture(assets.LampIds0Path, "lampIds0", out var lampIds0, out fallbackReason))
        {
            DisposeAll(artwork, mask, trayId);
            return false;
        }

        if (!TryLoadTexture(assets.LampWeights0Path, "lampWeights0", out var lampWeights0, out fallbackReason))
        {
            DisposeAll(artwork, mask, trayId, lampIds0);
            return false;
        }

        if (assets.Width > 0 && assets.Height > 0 && (artwork.Width != assets.Width || artwork.Height != assets.Height))
        {
            fallbackReason = $"dimension mismatch: artwork is {artwork.Width}x{artwork.Height}, runtime assets declare {assets.Width}x{assets.Height}";
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

    private bool TryLoadTexture(string? assetPath, string textureName, out SKBitmap bitmap, out string fallbackReason)
    {
        bitmap = default!;
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
}

public sealed class FaceTexturePreviewSettings
{
    public static FaceTexturePreviewSettings Default { get; } = new();

    public double AmbientStrength { get; init; } = 0.35d;
    public double EmissionStrength { get; init; } = 1.15d;
    public double MaskStrength { get; init; } = 1d;
    public int LampIds0ChannelCount { get; init; } = 1;
}

public sealed class FaceTexturePreviewRenderResult : IDisposable
{
    private FaceTexturePreviewRenderResult(bool rendered, SKBitmap? bitmap, string? fallbackReason)
    {
        Rendered = rendered;
        Bitmap = bitmap;
        FallbackReason = fallbackReason;
    }

    public bool Rendered { get; }
    public SKBitmap? Bitmap { get; }
    public string? FallbackReason { get; }

    public static FaceTexturePreviewRenderResult FromBitmap(SKBitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        return new FaceTexturePreviewRenderResult(true, bitmap, null);
    }

    public static FaceTexturePreviewRenderResult Fallback(string fallbackReason)
    {
        return new FaceTexturePreviewRenderResult(false, null, fallbackReason);
    }

    public void Dispose()
    {
        Bitmap?.Dispose();
    }
}
