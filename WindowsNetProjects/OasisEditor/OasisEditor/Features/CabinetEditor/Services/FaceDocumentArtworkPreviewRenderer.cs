using System.IO;
using System.Windows.Media.Imaging;
using OasisEditor.Features.CabinetEditor.Models;
using OasisEditor.Rendering;
using SkiaSharp;

namespace OasisEditor.Features.CabinetEditor.Services;

public sealed class FaceDocumentArtworkPreviewRenderer
{
    public static FaceCompositorRenderOptions LivePreviewRenderOptions { get; } = new() { MaxWidth = 512, MaxHeight = 512 };
    public static FaceCompositorRenderOptions StaticPreviewRenderOptions { get; } = new() { MaxWidth = 1024, MaxHeight = 1024 };

    private const int DefaultPreviewWidth = 1024;
    private const int DefaultPreviewHeight = 1024;

    public BitmapSource? RenderPreview(FaceDocumentModel faceDocument, MachineRuntimeState runtimeState, string? lampPreviewMode = null, FaceCompositorRenderOptions? renderOptions = null)
    {
        ArgumentNullException.ThrowIfNull(faceDocument);
        ArgumentNullException.ThrowIfNull(runtimeState);

        var normalizedMode = CabinetLampPreviewMode.Normalize(lampPreviewMode);
        if (normalizedMode != CabinetLampPreviewMode.BackgroundOnly
            && TryRenderCompositedPreview(faceDocument, runtimeState, normalizedMode, renderOptions, out var runtimePreview))
        {
            return runtimePreview;
        }

        var bounds = ResolveBounds(faceDocument);
        if (bounds.Width <= 0d || bounds.Height <= 0d)
        {
            return null;
        }

        var width = Math.Clamp((int)Math.Round(bounds.Width), 1, DefaultPreviewWidth);
        var height = Math.Clamp((int)Math.Round(bounds.Height), 1, DefaultPreviewHeight);
        using var surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
        if (surface is null)
        {
            return null;
        }

        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        canvas.Scale((float)(width / bounds.Width), (float)(height / bounds.Height));
        canvas.Translate((float)-bounds.X, (float)-bounds.Y);

        var renderedAny = false;
        foreach (var artwork in faceDocument.Elements.OfType<FaceArtworkElement>().Where(element => element.IsVisible))
        {
            if (TryDrawArtwork(canvas, artwork))
            {
                renderedAny = true;
            }
        }

        if (!renderedAny)
        {
            return null;
        }

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        if (data is null)
        {
            return null;
        }

        using var stream = new MemoryStream(data.ToArray());
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }


    private static bool TryRenderCompositedPreview(FaceDocumentModel faceDocument, MachineRuntimeState runtimeState, string lampPreviewMode, FaceCompositorRenderOptions? renderOptions, out BitmapSource? preview)
    {
        preview = null;
        var compositorRuntimeState = runtimeState;
        if (lampPreviewMode != CabinetLampPreviewMode.Live)
        {
            compositorRuntimeState = CreateStaticPreviewRuntimeState(faceDocument, lampPreviewMode);
        }

        renderOptions ??= lampPreviewMode == CabinetLampPreviewMode.Live ? LivePreviewRenderOptions : StaticPreviewRenderOptions;
        using var result = FaceCompositor.Shared.Compose(faceDocument, compositorRuntimeState, renderOptions);
        if (!result.Rendered || result.Bitmap is null)
        {
            return false;
        }

        preview = ToBitmapSource(result.Bitmap);
        return preview is not null;
    }

    private static MachineRuntimeState CreateStaticPreviewRuntimeState(FaceDocumentModel faceDocument, string lampPreviewMode)
    {
        var runtimeState = new MachineRuntimeState();
        if (lampPreviewMode == CabinetLampPreviewMode.LampsAllOn)
        {
            foreach (var lampWindow in faceDocument.Elements.OfType<FaceLampWindowElement>())
            {
                if (lampWindow.LinkedMachineObjectReference is MachineObjectReference { Kind: MachineObjectKind.Lamp } reference && !reference.IsEmpty)
                {
                    runtimeState.SetLampIntensity(reference, 1d);
                }
            }
        }

        return runtimeState;
    }

    private static BitmapSource? ToBitmapSource(SKBitmap bitmap)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        if (data is null)
        {
            return null;
        }

        using var stream = new MemoryStream(data.ToArray());
        var source = new BitmapImage();
        source.BeginInit();
        source.CacheOption = BitmapCacheOption.OnLoad;
        source.StreamSource = stream;
        source.EndInit();
        source.Freeze();
        return source;
    }

    private static FacePreviewBounds ResolveBounds(FaceDocumentModel faceDocument)
    {
        if (faceDocument.SourceRegion is { Width: > 0d, Height: > 0d } sourceRegion)
        {
            return new FacePreviewBounds(0d, 0d, sourceRegion.Width, sourceRegion.Height);
        }

        var visibleArtwork = faceDocument.Elements.OfType<FaceArtworkElement>()
            .Where(element => element.IsVisible && element.Width > 0d && element.Height > 0d)
            .ToArray();
        if (visibleArtwork.Length == 0)
        {
            return default;
        }

        var left = visibleArtwork.Min(element => element.X);
        var top = visibleArtwork.Min(element => element.Y);
        var right = visibleArtwork.Max(element => element.X + element.Width);
        var bottom = visibleArtwork.Max(element => element.Y + element.Height);
        return new FacePreviewBounds(left, top, Math.Max(0d, right - left), Math.Max(0d, bottom - top));
    }

    private static bool TryDrawArtwork(SKCanvas canvas, FaceArtworkElement element)
    {
        if (!TryLoadImage(element.AssetPath, out var image))
        {
            return false;
        }

        using (image)
        {
            var source = ResolveArtworkSourceRect(element, image);
            var destination = SKRect.Create((float)element.X, (float)element.Y, (float)Math.Max(0d, element.Width), (float)Math.Max(0d, element.Height));
            if (source.Width <= 0f || source.Height <= 0f || destination.Width <= 0f || destination.Height <= 0f)
            {
                return false;
            }

            canvas.DrawImage(image, source, destination);
            return true;
        }
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

    private static bool TryLoadImage(string? assetPath, out SKImage image)
    {
        image = default!;
        if (!TryResolveAssetPath(assetPath, out var resolvedPath) || !File.Exists(resolvedPath))
        {
            return false;
        }

        using var codec = SKCodec.Create(resolvedPath);
        if (codec is null)
        {
            return false;
        }

        using var bitmap = SKBitmap.Decode(codec);
        if (bitmap is null)
        {
            return false;
        }

        image = SKImage.FromBitmap(bitmap);
        return true;
    }

    private static string? ResolveAssetPathOrNull(string? assetPath)
    {
        return TryResolveAssetPath(assetPath, out var resolvedPath) ? resolvedPath : null;
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

        var relativePath = candidate.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        resolvedPath = Path.GetFullPath(Path.Combine(PanelElementFactory.ProjectDirectoryPath, relativePath));
        return true;
    }

    private readonly record struct FacePreviewBounds(double X, double Y, double Width, double Height);
}
