using System.IO;
using System.Text.Json;
using SkiaSharp;

namespace OasisEditor.Rendering;

internal sealed class SevenSegmentElementRenderer : IPanelElementRenderer
{
    private const int MaxVisualCacheEntries = 2048;
    private static readonly Lazy<SevenSegmentSkiaDefinition?> Definition = new(LoadDefinition);
    private static readonly Dictionary<SegmentVisualCacheKey, SKImage> VisualCache = new();
    private static readonly object VisualCacheGate = new();
    [ThreadStatic]
    private static int _diagnosticsCacheHits;
    [ThreadStatic]
    private static int _diagnosticsCacheMisses;

    public PanelElementKind Kind => PanelElementKind.SevenSegment;
    internal static int DiagnosticsCacheHits => _diagnosticsCacheHits;
    internal static int DiagnosticsCacheMisses => _diagnosticsCacheMisses;
    internal static void ResetDiagnosticsCounters()
    {
        _diagnosticsCacheHits = 0;
        _diagnosticsCacheMisses = 0;
    }

    public void Render(in PanelElementRenderContext context, PanelElementModel element)
    {
        var bounds = SKRect.Create((float)element.X, (float)element.Y, (float)element.Width, (float)element.Height);
        if (bounds.Width <= 0f || bounds.Height <= 0f)
        {
            return;
        }

        var definition = Definition.Value;
        if (definition is null)
        {
            return;
        }

        var masks = context.RuntimeState.GetSegmentCellMasks(element.ObjectId, 1);
        var brightness = context.RuntimeState.GetSegmentCellBrightness(element.ObjectId, 1);
        var segmentMask = masks.Length > 0 ? masks[0] : 0;
        var litAmount = brightness.Length > 0 ? Math.Clamp(brightness[0], 0d, 1d) : 1d;

        var onColor = SkiaColorParser.ParseOrDefault(element.OnColorHex, new SKColor(255, 64, 64));
        var offColor = SkiaColorParser.ParseOrDefault(element.OffColorHex, new SKColor(72, 24, 24));

        var width = Math.Max(1, (int)Math.Round(bounds.Width));
        var height = Math.Max(1, (int)Math.Round(bounds.Height));
        var brightnessBucket = (int)Math.Round(Math.Clamp(litAmount, 0d, 1d) * 4d);
        var cacheKey = new SegmentVisualCacheKey(width, height, segmentMask, brightnessBucket, onColor, offColor);
        var visual = GetOrCreateVisual(cacheKey, definition);
        context.Canvas.DrawImage(visual, bounds.Left, bounds.Top);
    }

    private static SKImage GetOrCreateVisual(SegmentVisualCacheKey cacheKey, SevenSegmentSkiaDefinition definition)
    {
        lock (VisualCacheGate)
        {
            if (VisualCache.TryGetValue(cacheKey, out var cached))
            {
                _diagnosticsCacheHits++;
                return cached;
            }

            _diagnosticsCacheMisses++;
            var created = BuildVisual(cacheKey, definition);
            if (VisualCache.Count > MaxVisualCacheEntries)
            {
                VisualCache.Clear();
            }

            VisualCache[cacheKey] = created;
            return created;
        }
    }

    private static SKImage BuildVisual(SegmentVisualCacheKey cacheKey, SevenSegmentSkiaDefinition definition)
    {
        using var surface = SKSurface.Create(new SKImageInfo(cacheKey.Width, cacheKey.Height));
        var canvas = surface.Canvas;
        var bounds = SKRect.Create(0f, 0f, cacheKey.Width, cacheKey.Height);
        using var backgroundPaint = new SKPaint { Color = new SKColor(17, 24, 39), Style = SKPaintStyle.Fill, IsAntialias = true };
        using var borderPaint = new SKPaint { Color = new SKColor(71, 85, 105), Style = SKPaintStyle.Stroke, StrokeWidth = 1f, IsAntialias = true };
        canvas.DrawRoundRect(bounds, 2f, 2f, backgroundPaint);
        canvas.DrawRoundRect(bounds, 2f, 2f, borderPaint);
        var marginX = bounds.Width * 0.1f;
        var marginY = bounds.Height * 0.1f;
        var contentBounds = SKRect.Create(bounds.Left + marginX, bounds.Top + marginY, Math.Max(1f, bounds.Width - (marginX * 2f)), Math.Max(1f, bounds.Height - (marginY * 2f)));
        using var paint = new SKPaint { Style = SKPaintStyle.Fill, IsAntialias = true };
        var scale = Math.Min(contentBounds.Width / definition.Width, contentBounds.Height / definition.Height);
        var offsetX = contentBounds.Left + ((contentBounds.Width - (definition.Width * scale)) * 0.5f);
        var offsetY = contentBounds.Top + ((contentBounds.Height - (definition.Height * scale)) * 0.5f);
        canvas.Save();
        canvas.Translate(offsetX, offsetY);
        canvas.Scale(scale, scale);
        var litAmount = cacheKey.BrightnessBucket / 4d;
        foreach (var segment in definition.Segments)
        {
            var lit = (cacheKey.SegmentMask & (1 << segment.Index)) != 0;
            paint.Color = lit ? Lerp(cacheKey.OffColor, cacheKey.OnColor, litAmount) : cacheKey.OffColor;
            canvas.DrawPath(segment.Path, paint);
        }
        if (definition.DecimalPoint is not null)
        {
            paint.Color = cacheKey.OffColor;
            canvas.DrawPath(definition.DecimalPoint, paint);
        }
        canvas.Restore();
        return surface.Snapshot();
    }

    private static SevenSegmentSkiaDefinition? LoadDefinition()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "SegmentDisplays", "oasis_7_segment_display_definition.json");
        if (!File.Exists(path))
        {
            return null;
        }

        var json = File.ReadAllText(path);
        var root = JsonSerializer.Deserialize<SevenSegmentDefinitionRoot>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        var cell = root?.Cell;
        if (cell?.Size is null || cell.Segments is null || cell.Segments.Count != 7)
        {
            return null;
        }

        var paths = new List<SevenSegmentSkiaPath>(cell.Segments.Count);
        foreach (var segment in cell.Segments)
        {
            if (string.IsNullOrWhiteSpace(segment.PathData))
            {
                return null;
            }

            paths.Add(new SevenSegmentSkiaPath(segment.Index, SKPath.ParseSvgPathData(segment.PathData)));
        }

        var decimalPath = string.IsNullOrWhiteSpace(cell.DecimalPoint?.PathData)
            ? null
            : SKPath.ParseSvgPathData(cell.DecimalPoint.PathData);

        return new SevenSegmentSkiaDefinition((float)cell.Size.Width, (float)cell.Size.Height, paths, decimalPath);
    }

    private static SKColor Lerp(SKColor from, SKColor to, double t)
    {
        var clamped = Math.Clamp(t, 0d, 1d);
        byte Blend(byte a, byte b) => (byte)Math.Clamp(Math.Round(a + ((b - a) * clamped)), 0d, 255d);
        return new SKColor(Blend(from.Red, to.Red), Blend(from.Green, to.Green), Blend(from.Blue, to.Blue), 255);
    }

    private sealed record SevenSegmentSkiaDefinition(float Width, float Height, IReadOnlyList<SevenSegmentSkiaPath> Segments, SKPath? DecimalPoint);
    private readonly record struct SegmentVisualCacheKey(int Width, int Height, int SegmentMask, int BrightnessBucket, SKColor OnColor, SKColor OffColor);
    private sealed record SevenSegmentSkiaPath(int Index, SKPath Path);

    private sealed class SevenSegmentDefinitionRoot
    {
        public SevenSegmentCell? Cell { get; set; }
    }

    private sealed class SevenSegmentCell
    {
        public SevenSegmentSize? Size { get; set; }
        public List<SevenSegmentPath>? Segments { get; set; }
        public SevenSegmentDecimalPoint? DecimalPoint { get; set; }
    }

    private sealed class SevenSegmentSize
    {
        public double Width { get; set; }
        public double Height { get; set; }
    }

    private sealed class SevenSegmentPath
    {
        public int Index { get; set; }
        public string? PathData { get; set; }
    }

    private sealed class SevenSegmentDecimalPoint
    {
        public string? PathData { get; set; }
    }
}
