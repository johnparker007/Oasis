using System.IO;
using System.Text.Json;
using SkiaSharp;

namespace OasisEditor.Rendering;

internal sealed class SevenSegmentElementRenderer : IPanelElementRenderer
{
    private static readonly Lazy<SevenSegmentSkiaDefinition?> Definition = new(LoadDefinition);

    public PanelElementKind Kind => PanelElementKind.SevenSegment;

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
        using var paint = new SKPaint { Style = SKPaintStyle.Fill, IsAntialias = true };

        var scale = Math.Min(bounds.Width / definition.Width, bounds.Height / definition.Height);
        var offsetX = bounds.Left + ((bounds.Width - (definition.Width * scale)) * 0.5f);
        var offsetY = bounds.Top + ((bounds.Height - (definition.Height * scale)) * 0.5f);

        context.Canvas.Save();
        context.Canvas.Translate(offsetX, offsetY);
        context.Canvas.Scale(scale, scale);

        foreach (var segment in definition.Segments)
        {
            var lit = (segmentMask & (1 << segment.Index)) != 0;
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
