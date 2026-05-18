using SkiaSharp;
using System.Collections.Concurrent;
using System.IO;

namespace OasisEditor.Rendering;

internal sealed class ReelElementRenderer : IPanelElementRenderer
{
    private const double LegacyReelPositionsPerRevolution = 96d;
    private static readonly ConcurrentDictionary<string, SKImage?> CachedStripImages = new(StringComparer.OrdinalIgnoreCase);

    public PanelElementKind Kind => PanelElementKind.Reel;

    public void Render(in PanelElementRenderContext context, PanelElementModel element)
    {
        var bounds = SKRect.Create((float)element.X, (float)element.Y, (float)element.Width, (float)element.Height);
        if (bounds.Width <= 0f || bounds.Height <= 0f)
        {
            return;
        }

        var stops = Math.Max(1, element.Stops.GetValueOrDefault(1));
        var reelPosition = context.RuntimeState.GetReelPosition(element.ObjectId);
        var wrappedOffset = ComputeWrappedOffset(reelPosition, stops);

        using var borderPaint = new SKPaint { Color = new SKColor(45, 45, 45), Style = SKPaintStyle.Stroke, StrokeWidth = 1f, IsAntialias = true };
        using var clipPath = new SKPath();
        clipPath.AddRect(bounds);

        context.Canvas.Save();
        context.Canvas.ClipPath(clipPath);

        if (TryGetStripImage(element.AssetPath, out var stripImage))
        {
            DrawStripImage(context.Canvas, bounds, stripImage, wrappedOffset);
        }
        else
        {
            var cellHeight = bounds.Height;
            DrawStripPlaceholder(context.Canvas, bounds.Left, bounds.Top - (float)(wrappedOffset * cellHeight), bounds.Width, cellHeight, stops);
            DrawStripPlaceholder(context.Canvas, bounds.Left, bounds.Top - (float)(wrappedOffset * cellHeight) + cellHeight, bounds.Width, cellHeight, stops);
        }

        context.Canvas.Restore();
        context.Canvas.DrawRect(bounds, borderPaint);
    }

    internal static double ComputeWrappedOffset(int position, int stops)
    {
        var safeStops = Math.Max(1, stops);
        var positionsPerRevolution = Math.Max(LegacyReelPositionsPerRevolution, safeStops);
        var raw = ((position % positionsPerRevolution) + positionsPerRevolution) % positionsPerRevolution;
        return raw / positionsPerRevolution;
    }

    private static void DrawStripPlaceholder(SKCanvas canvas, float left, float top, float width, float height, int stops)
    {
        var safeStops = Math.Max(1, stops);
        var stopHeight = height / safeStops;

        for (var index = 0; index < safeStops; index++)
        {
            var y = top + (index * stopHeight);
            var t = safeStops == 1 ? 0d : index / (double)(safeStops - 1);
            var baseColor = Lerp(new SKColor(36, 36, 36), new SKColor(180, 180, 180), t);

            using var fill = new SKPaint { Color = baseColor, Style = SKPaintStyle.Fill, IsAntialias = true };
            using var label = new SKPaint { Color = SKColors.White, IsAntialias = true, TextSize = Math.Max(10f, stopHeight * 0.35f) };

            var rect = SKRect.Create(left, y, width, stopHeight);
            canvas.DrawRect(rect, fill);

            var text = index.ToString();
            var textBounds = new SKRect();
            label.MeasureText(text, ref textBounds);
            var textX = left + (width * 0.5f) - (textBounds.MidX);
            var textY = y + (stopHeight * 0.5f) - textBounds.MidY;
            canvas.DrawText(text, textX, textY, label);
        }
    }

    private static void DrawStripImage(SKCanvas canvas, SKRect bounds, SKImage stripImage, double wrappedOffset)
    {
        var destinationHeight = bounds.Height;
        var top = bounds.Top - (float)(wrappedOffset * destinationHeight);
        var destinationRect = SKRect.Create(bounds.Left, top, bounds.Width, destinationHeight);
        var wrappedDestinationRect = SKRect.Create(bounds.Left, top + destinationHeight, bounds.Width, destinationHeight);
        var sourceRect = SKRect.Create(0f, 0f, stripImage.Width, stripImage.Height);

        canvas.DrawImage(stripImage, sourceRect, destinationRect);
        canvas.DrawImage(stripImage, sourceRect, wrappedDestinationRect);
    }

    private static bool TryGetStripImage(string? assetPath, out SKImage stripImage)
    {
        stripImage = default!;
        if (!TryResolveAssetPath(assetPath, out var resolvedPath))
        {
            return false;
        }

        var cached = CachedStripImages.GetOrAdd(resolvedPath, LoadImage);
        if (cached is null)
        {
            return false;
        }

        stripImage = cached;
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

    private static SKColor Lerp(SKColor from, SKColor to, double t)
    {
        var clamped = Math.Clamp(t, 0d, 1d);
        byte Blend(byte a, byte b) => (byte)Math.Clamp(Math.Round(a + ((b - a) * clamped)), 0d, 255d);
        return new SKColor(Blend(from.Red, to.Red), Blend(from.Green, to.Green), Blend(from.Blue, to.Blue), 255);
    }
}
