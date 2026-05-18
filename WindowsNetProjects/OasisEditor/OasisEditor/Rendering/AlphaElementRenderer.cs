using System.IO;
using System.Text.Json;
using SkiaSharp;

namespace OasisEditor.Rendering;

internal sealed class AlphaElementRenderer : IPanelElementRenderer
{
    private static readonly Lazy<AlphaSkiaDefinition?> Definition = new(LoadDefinition);

    public PanelElementKind Kind => PanelElementKind.Alpha;

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

        var cellMasks = context.RuntimeState.GetSegmentCellMasks(element.ObjectId, 16);
        var cellBrightness = context.RuntimeState.GetSegmentCellBrightness(element.ObjectId, 16);

        var onColor = SkiaColorParser.ParseOrDefault(element.OnColorHex, new SKColor(255, 64, 64));
        var offColor = SkiaColorParser.ParseOrDefault(element.OffColorHex, new SKColor(72, 24, 24));

        using var backgroundPaint = new SKPaint { Color = new SKColor(17, 24, 39), Style = SKPaintStyle.Fill, IsAntialias = true };
        using var borderPaint = new SKPaint { Color = new SKColor(71, 85, 105), Style = SKPaintStyle.Stroke, StrokeWidth = 1f, IsAntialias = true };
        context.Canvas.DrawRoundRect(bounds, 2f, 2f, backgroundPaint);
        context.Canvas.DrawRoundRect(bounds, 2f, 2f, borderPaint);

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

        using var paint = new SKPaint { Style = SKPaintStyle.Fill, IsAntialias = true };
        for (var cellIndex = 0; cellIndex < cellCount; cellIndex++)
        {
            var mask = cellIndex < cellMasks.Length ? cellMasks[cellIndex] : 0;
            var litAmount = cellIndex < cellBrightness.Length ? Math.Clamp(cellBrightness[cellIndex], 0d, 1d) : 1d;

            context.Canvas.Save();
            context.Canvas.Translate(originX + (cellIndex * scaledPitch), originY);
            context.Canvas.Scale(scale, scale);

            foreach (var segment in definition.Segments)
            {
                var lit = (mask & (1 << segment.Index)) != 0;
                paint.Color = lit ? Lerp(offColor, onColor, litAmount) : offColor;
                context.Canvas.DrawPath(segment.Path, paint);
            }

            if (definition.DecimalPoint is not null)
            {
                paint.Color = offColor;
                context.Canvas.DrawPath(definition.DecimalPoint, paint);
            }

            context.Canvas.Restore();
        }
    }

    private static AlphaSkiaDefinition? LoadDefinition()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "SegmentDisplays", "oasis_16_segment_display_definition.json");
        if (!File.Exists(path))
        {
            return null;
        }

        var json = File.ReadAllText(path);
        var root = JsonSerializer.Deserialize<AlphaDefinitionRoot>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        var cell = root?.Cell;
        if (cell?.Size is null || cell.Segments is null || cell.Segments.Count != 16)
        {
            return null;
        }

        var paths = new List<AlphaSkiaPath>(cell.Segments.Count);
        foreach (var segment in cell.Segments)
        {
            if (string.IsNullOrWhiteSpace(segment.PathData))
            {
                return null;
            }

            paths.Add(new AlphaSkiaPath(segment.Index, SKPath.ParseSvgPathData(segment.PathData)));
        }

        var decimalPath = string.IsNullOrWhiteSpace(cell.DecimalPoint?.PathData)
            ? null
            : SKPath.ParseSvgPathData(cell.DecimalPoint.PathData);

        return new AlphaSkiaDefinition((float)cell.Size.Width, (float)cell.Size.Height, (float)cell.RecommendedPitch, paths, decimalPath);
    }

    private static SKColor Lerp(SKColor from, SKColor to, double t)
    {
        var clamped = Math.Clamp(t, 0d, 1d);
        byte Blend(byte a, byte b) => (byte)Math.Clamp(Math.Round(a + ((b - a) * clamped)), 0d, 255d);
        return new SKColor(Blend(from.Red, to.Red), Blend(from.Green, to.Green), Blend(from.Blue, to.Blue), 255);
    }

    private sealed record AlphaSkiaDefinition(float Width, float Height, float RecommendedPitch, IReadOnlyList<AlphaSkiaPath> Segments, SKPath? DecimalPoint);
    private sealed record AlphaSkiaPath(int Index, SKPath Path);

    private sealed class AlphaDefinitionRoot
    {
        public AlphaCell? Cell { get; set; }
    }

    private sealed class AlphaCell
    {
        public AlphaSize? Size { get; set; }
        public double RecommendedPitch { get; set; }
        public List<AlphaPath>? Segments { get; set; }
        public AlphaDecimalPoint? DecimalPoint { get; set; }
    }

    private sealed class AlphaSize
    {
        public double Width { get; set; }
        public double Height { get; set; }
    }

    private sealed class AlphaPath
    {
        public int Index { get; set; }
        public string? PathData { get; set; }
    }

    private sealed class AlphaDecimalPoint
    {
        public string? PathData { get; set; }
    }
}
