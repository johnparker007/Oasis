using SkiaSharp;

namespace OasisEditor.Rendering;

internal sealed class AlphaElementRenderer : IPanelElementRenderer
{
    private const int MaxVisualCacheEntries = 4096;
    private static readonly Dictionary<string, Lazy<AlphaSkiaDefinition?>> DefinitionsByType = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<AlphaVisualCacheKey, SKImage> VisualCache = new();
    private static readonly object VisualCacheGate = new();
    [ThreadStatic]
    private static int _diagnosticsCacheHits;
    [ThreadStatic]
    private static int _diagnosticsCacheMisses;

    public PanelElementKind Kind => PanelElementKind.Alpha;
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

        var displayType = string.IsNullOrWhiteSpace(element.SegmentDisplayType) ? "led16seg" : element.SegmentDisplayType!;
        var definition = GetDefinition(displayType);
        if (definition is null)
        {
            return;
        }

        var cellMasks = context.RuntimeState.GetSegmentCellMasks(element.ObjectId, 16);
        var defaultMask = BuildDefaultMask(definition);
        var cellBrightness = context.RuntimeState.GetSegmentCellBrightness(element.ObjectId, 16);

        var onColor = SkiaColorParser.ParseOrDefault(element.OnColorHex, new SKColor(255, 64, 64));
        var offColor = ScaleBrightness(onColor, 0.10d);
        var backgroundColor = ScaleBrightness(onColor, 0.04d);

        using var backgroundPaint = new SKPaint { Color = backgroundColor, Style = SKPaintStyle.Fill, IsAntialias = true };
        context.Canvas.DrawRoundRect(bounds, 2f, 2f, backgroundPaint);

        var marginX = bounds.Width * 0.05f;
        var marginY = bounds.Height * 0.1f;
        var contentBounds = SKRect.Create(
            bounds.Left + marginX,
            bounds.Top + marginY,
            Math.Max(1f, bounds.Width - (marginX * 2f)),
            Math.Max(1f, bounds.Height - (marginY * 2f)));

        var cellCount = Math.Max(1, Math.Max(cellMasks.Length, cellBrightness.Length));
        var pitch = definition.RecommendedPitch > 0f ? definition.RecommendedPitch : definition.Width;
        var totalWidth = (pitch * cellCount) - Math.Max(0f, pitch - definition.Width);
        var scale = Math.Min(contentBounds.Height / definition.Height, contentBounds.Width / Math.Max(1f, totalWidth));
        var scaledPitch = pitch * scale;
        var scaledCellWidth = definition.Width * scale;
        var scaledCellHeight = definition.Height * scale;
        var originX = contentBounds.Left + ((contentBounds.Width - ((scaledPitch * cellCount) - Math.Max(0f, scaledPitch - scaledCellWidth))) * 0.5f);
        var originY = contentBounds.Top + ((contentBounds.Height - scaledCellHeight) * 0.5f);
        var cellPixelWidth = Math.Max(1, (int)Math.Round(scaledCellWidth));
        var cellPixelHeight = Math.Max(1, (int)Math.Round(scaledCellHeight));

        for (var cellIndex = 0; cellIndex < cellCount; cellIndex++)
        {
            var dataIndex = element.IsReversed == true ? (cellCount - 1 - cellIndex) : cellIndex;
            var mask = dataIndex < cellMasks.Length ? cellMasks[dataIndex] : defaultMask;
            var litAmount = dataIndex < cellBrightness.Length ? Math.Clamp(cellBrightness[dataIndex], 0d, 1d) : 1d;
            var brightnessBucket = (int)Math.Round(litAmount * 4d);
            var key = new AlphaVisualCacheKey(displayType, cellPixelWidth, cellPixelHeight, mask, brightnessBucket, onColor, offColor, element.ShowDecimalPoint, element.ShowCommaTail);
            var visual = GetOrCreateVisual(key, definition);
            var cellRect = SKRect.Create(originX + (cellIndex * scaledPitch), originY, scaledCellWidth, scaledCellHeight);
            context.Canvas.DrawImage(visual, cellRect);
        }
    }

    private static SKImage GetOrCreateVisual(AlphaVisualCacheKey key, AlphaSkiaDefinition definition)
    {
        lock (VisualCacheGate)
        {
            if (VisualCache.TryGetValue(key, out var cached))
            {
                _diagnosticsCacheHits++;
                return cached;
            }

            _diagnosticsCacheMisses++;
            var created = BuildVisual(key, definition);
            if (VisualCache.Count > MaxVisualCacheEntries)
            {
                VisualCache.Clear();
            }

            VisualCache[key] = created;
            return created;
        }
    }

    private static SKImage BuildVisual(AlphaVisualCacheKey key, AlphaSkiaDefinition definition)
    {
        using var surface = SKSurface.Create(new SKImageInfo(key.Width, key.Height));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        var bounds = SKRect.Create(0f, 0f, key.Width, key.Height);
        using var paint = new SKPaint { Style = SKPaintStyle.Fill, IsAntialias = true };
        var scale = Math.Min(bounds.Width / definition.Width, bounds.Height / definition.Height);
        var offsetX = (bounds.Width - (definition.Width * scale)) * 0.5f;
        var offsetY = (bounds.Height - (definition.Height * scale)) * 0.5f;
        canvas.Save();
        canvas.Translate(offsetX, offsetY);
        canvas.Scale(scale, scale);
        var litAmount = key.BrightnessBucket / 4d;
        foreach (var segment in definition.Segments)
        {
            var lit = (key.Mask & (1 << segment.Index)) != 0;
            paint.Color = lit ? Lerp(key.OffColor, key.OnColor, litAmount) : key.OffColor;
            canvas.DrawPath(segment.Path, paint);
        }
        if (definition.DecimalPoint is not null && key.ShowDecimalPoint)
        {
            var lit = (key.Mask & (1 << definition.DecimalPointBitIndex)) != 0;
            paint.Color = lit ? Lerp(key.OffColor, key.OnColor, litAmount) : key.OffColor;
            canvas.DrawPath(definition.DecimalPoint, paint);
        }

        if (definition.CommaTail is not null && key.ShowCommaTail)
        {
            var lit = (key.Mask & (1 << definition.CommaTailBitIndex)) != 0;
            paint.Color = lit ? Lerp(key.OffColor, key.OnColor, litAmount) : key.OffColor;
            canvas.DrawPath(definition.CommaTail, paint);
        }
        canvas.Restore();
        return surface.Snapshot();
    }

    private static AlphaSkiaDefinition? GetDefinition(string displayType)
    {
        lock (VisualCacheGate)
        {
            if (!DefinitionsByType.TryGetValue(displayType, out var lazy))
            {
                lazy = new Lazy<AlphaSkiaDefinition?>(() => LoadDefinition(displayType));
                DefinitionsByType[displayType] = lazy;
            }

            return lazy.Value;
        }
    }

    private static AlphaSkiaDefinition? LoadDefinition(string displayType)
    {
        if (!SegmentDisplayDefinitionLoader.TryGetDefinitionByType(displayType, out var definition)
            || definition.Cell is null || definition.Cell.Size is null || definition.Cell.Segments is null)
        {
            return null;
        }

        var paths = new List<AlphaSkiaPath>(definition.Cell.Segments.Count);
        foreach (var segment in definition.Cell.Segments)
        {
            if (string.IsNullOrWhiteSpace(segment.PathData))
            {
                return null;
            }

            paths.Add(new AlphaSkiaPath(segment.BitIndex ?? segment.Index, SKPath.ParseSvgPathData(segment.PathData)));
        }

        var decimalPoint = string.IsNullOrWhiteSpace(definition.Cell.DecimalPoint?.PathData)
            ? null
            : SKPath.ParseSvgPathData(definition.Cell.DecimalPoint.PathData);
        var decimalPointBitIndex = definition.Cell.DecimalPoint?.BitIndex ?? 16;

        var commaTail = string.IsNullOrWhiteSpace(definition.Cell.CommaTail?.PathData)
            ? null
            : SKPath.ParseSvgPathData(definition.Cell.CommaTail.PathData);
        var commaTailBitIndex = definition.Cell.CommaTail?.BitIndex ?? 17;

        return new AlphaSkiaDefinition((float)definition.Cell.Size.Width, (float)definition.Cell.Size.Height, (float)definition.Cell.RecommendedPitch, paths, decimalPoint, decimalPointBitIndex, commaTail, commaTailBitIndex);
    }

    private static int BuildDefaultMask(AlphaSkiaDefinition definition)
    {
        var mask = 0;
        foreach (var segment in definition.Segments)
        {
            if (segment.Index is >= 0 and < 31)
            {
                mask |= 1 << segment.Index;
            }
        }

        return mask;
    }

    private static SKColor ScaleBrightness(SKColor color, double factor)
    {
        var clamped = Math.Clamp(factor, 0d, 1d);
        byte Scale(byte value) => (byte)Math.Clamp(Math.Round(value * clamped), 0d, 255d);
        return new SKColor(Scale(color.Red), Scale(color.Green), Scale(color.Blue), 255);
    }

    private static SKColor Lerp(SKColor from, SKColor to, double t)
    {
        var clamped = Math.Clamp(t, 0d, 1d);
        byte Blend(byte a, byte b) => (byte)Math.Clamp(Math.Round(a + ((b - a) * clamped)), 0d, 255d);
        return new SKColor(Blend(from.Red, to.Red), Blend(from.Green, to.Green), Blend(from.Blue, to.Blue), 255);
    }

    private sealed record AlphaSkiaDefinition(float Width, float Height, float RecommendedPitch, IReadOnlyList<AlphaSkiaPath> Segments, SKPath? DecimalPoint, int DecimalPointBitIndex, SKPath? CommaTail, int CommaTailBitIndex);
    private readonly record struct AlphaVisualCacheKey(string DisplayType, int Width, int Height, int Mask, int BrightnessBucket, SKColor OnColor, SKColor OffColor, bool ShowDecimalPoint, bool ShowCommaTail);
    private sealed record AlphaSkiaPath(int Index, SKPath Path);
}
