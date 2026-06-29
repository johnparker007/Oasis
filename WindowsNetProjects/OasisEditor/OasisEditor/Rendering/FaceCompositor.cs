using SkiaSharp;

namespace OasisEditor.Rendering;

public interface IFaceCompositor
{
    FaceCompositorResult Compose(FaceDocumentModel faceDocument, MachineRuntimeState runtimeState, FaceCompositorRenderOptions? options = null);

    void Render(SKCanvas canvas, FaceDocumentModel faceDocument, MachineRuntimeState runtimeState, PanelViewportTransform viewportTransform, FaceCompositorRenderOptions? options = null);

    FaceCompositorResult ComposeStaticBase(FaceDocumentModel faceDocument, MachineRuntimeState runtimeState, FaceCompositorRenderOptions? options = null);

    void RenderLampOverlay(SKCanvas canvas, FaceDocumentModel faceDocument, MachineRuntimeState runtimeState);
}

public sealed class FaceCompositor : IFaceCompositor
{
    public static FaceCompositor Shared { get; } = new();

    private readonly IFace2DRenderer _renderer;

    public FaceCompositor()
        : this(new Face2DRenderer())
    {
    }

    public FaceCompositor(IFace2DRenderer renderer)
    {
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
    }

    public FaceCompositorResult Compose(FaceDocumentModel faceDocument, MachineRuntimeState runtimeState, FaceCompositorRenderOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(faceDocument);
        ArgumentNullException.ThrowIfNull(runtimeState);

        return ComposeCore(faceDocument, runtimeState, options, staticOnly: false);
    }

    public FaceCompositorResult ComposeStaticBase(FaceDocumentModel faceDocument, MachineRuntimeState runtimeState, FaceCompositorRenderOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(faceDocument);
        ArgumentNullException.ThrowIfNull(runtimeState);

        return ComposeCore(faceDocument, runtimeState, options, staticOnly: true);
    }

    private FaceCompositorResult ComposeCore(FaceDocumentModel faceDocument, MachineRuntimeState runtimeState, FaceCompositorRenderOptions? options, bool staticOnly)
    {
        if (!TryResolveTarget(faceDocument, options, out var target, out var fallbackReason))
        {
            return FaceCompositorResult.Empty(fallbackReason);
        }

        using var surface = SKSurface.Create(new SKImageInfo(target.Width, target.Height, SKColorType.Bgra8888, SKAlphaType.Premul));
        if (surface is null)
        {
            return FaceCompositorResult.Empty("Unable to allocate Face compositing surface.");
        }

        var canvas = surface.Canvas;
        canvas.Clear(target.ClearColor);
        ApplyTargetTransform(canvas, target);
        if (staticOnly)
        {
            _renderer.RenderStaticBase(canvas, faceDocument, runtimeState, PanelViewportTransform.Identity);
        }
        else
        {
            _renderer.Render(canvas, faceDocument, runtimeState, PanelViewportTransform.Identity);
        }

        canvas.Flush();

        using var image = surface.Snapshot();
        var bitmap = SKBitmap.FromImage(image);
        return bitmap is null
            ? FaceCompositorResult.Empty("Unable to snapshot Face compositing surface.")
            : FaceCompositorResult.FromBitmap(bitmap, target.Bounds);
    }

    public void Render(SKCanvas canvas, FaceDocumentModel faceDocument, MachineRuntimeState runtimeState, PanelViewportTransform viewportTransform, FaceCompositorRenderOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(faceDocument);
        ArgumentNullException.ThrowIfNull(runtimeState);
        _renderer.Render(canvas, faceDocument, runtimeState, viewportTransform);
    }

    public void RenderLampOverlay(SKCanvas canvas, FaceDocumentModel faceDocument, MachineRuntimeState runtimeState)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(faceDocument);
        ArgumentNullException.ThrowIfNull(runtimeState);
        _renderer.RenderLampOverlay(canvas, faceDocument, runtimeState);
    }

    public static bool TryResolveTarget(FaceDocumentModel faceDocument, FaceCompositorRenderOptions? options, out FaceCompositorTarget target, out string fallbackReason)
    {
        ArgumentNullException.ThrowIfNull(faceDocument);
        options ??= FaceCompositorRenderOptions.Default;
        var bounds = ResolveBounds(faceDocument);
        if (bounds.Width <= 0d || bounds.Height <= 0d)
        {
            target = default;
            fallbackReason = "Face has no renderable bounds.";
            return false;
        }

        var requestedScale = Math.Clamp(options.Scale, 0.01d, 16d);
        var maxWidthScale = options.MaxWidth > 0 ? options.MaxWidth / bounds.Width : requestedScale;
        var maxHeightScale = options.MaxHeight > 0 ? options.MaxHeight / bounds.Height : requestedScale;
        var scale = Math.Max(0.01d, Math.Min(requestedScale, Math.Min(maxWidthScale, maxHeightScale)));
        var width = Math.Max(1, (int)Math.Ceiling(bounds.Width * scale));
        var height = Math.Max(1, (int)Math.Ceiling(bounds.Height * scale));
        target = new FaceCompositorTarget(bounds, width, height, options.ClearColor);
        fallbackReason = string.Empty;
        return true;
    }

    public static void ApplyTargetTransform(SKCanvas canvas, FaceCompositorTarget target)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        canvas.Scale((float)(target.Width / target.Bounds.Width), (float)(target.Height / target.Bounds.Height));
        canvas.Translate((float)-target.Bounds.X, (float)-target.Bounds.Y);
    }

    private static FaceCompositorBounds ResolveBounds(FaceDocumentModel faceDocument)
    {
        if (faceDocument.SourceRegion is { Width: > 0d, Height: > 0d } sourceRegion)
        {
            return new FaceCompositorBounds(0d, 0d, sourceRegion.Width, sourceRegion.Height);
        }

        var visibleElements = faceDocument.Elements
            .Where(element => element.IsVisible && element.Width > 0d && element.Height > 0d)
            .ToArray();
        if (visibleElements.Length == 0)
        {
            return default;
        }

        var left = visibleElements.Min(element => element.X);
        var top = visibleElements.Min(element => element.Y);
        var right = visibleElements.Max(element => element.X + element.Width);
        var bottom = visibleElements.Max(element => element.Y + element.Height);
        return new FaceCompositorBounds(left, top, Math.Max(0d, right - left), Math.Max(0d, bottom - top));
    }
}

public sealed class FaceCompositorRenderOptions
{
    public static FaceCompositorRenderOptions Default { get; } = new();

    public double Scale { get; init; } = 1d;
    public int MaxWidth { get; init; } = 2048;
    public int MaxHeight { get; init; } = 2048;
    public SKColor ClearColor { get; init; } = SKColors.Transparent;
}

public sealed class FaceCompositorResult : IDisposable
{
    private FaceCompositorResult(bool rendered, SKBitmap? bitmap, FaceCompositorBounds bounds, string? fallbackReason)
    {
        Rendered = rendered;
        Bitmap = bitmap;
        Bounds = bounds;
        FallbackReason = fallbackReason;
    }

    public bool Rendered { get; }
    public SKBitmap? Bitmap { get; }
    public FaceCompositorBounds Bounds { get; }
    public string? FallbackReason { get; }

    public static FaceCompositorResult FromBitmap(SKBitmap bitmap, FaceCompositorBounds bounds)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        return new FaceCompositorResult(true, bitmap, bounds, null);
    }

    public static FaceCompositorResult Empty(string fallbackReason)
    {
        return new FaceCompositorResult(false, null, default, fallbackReason);
    }

    public void Dispose()
    {
        Bitmap?.Dispose();
    }
}

public readonly record struct FaceCompositorBounds(double X, double Y, double Width, double Height);

public readonly record struct FaceCompositorTarget(FaceCompositorBounds Bounds, int Width, int Height, SKColor ClearColor);
