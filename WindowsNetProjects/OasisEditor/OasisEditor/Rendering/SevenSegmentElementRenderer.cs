using SkiaSharp;

namespace OasisEditor.Rendering;

internal sealed class SevenSegmentElementRenderer : IPanelElementRenderer
{
    public PanelElementKind Kind => PanelElementKind.SevenSegment;

    public void Render(in PanelElementRenderContext context, PanelElementModel element)
    {
        var bounds = SKRect.Create((float)element.X, (float)element.Y, (float)element.Width, (float)element.Height);
        if (bounds.Width <= 0f || bounds.Height <= 0f)
        {
            return;
        }

        using var background = new SKPaint { Color = new SKColor(17, 24, 39), Style = SKPaintStyle.Fill, IsAntialias = true };
        using var border = new SKPaint { Color = new SKColor(71, 85, 105), Style = SKPaintStyle.Stroke, StrokeWidth = 1f, IsAntialias = true };
        context.Canvas.DrawRoundRect(bounds, 3f, 3f, background);
        context.Canvas.DrawRoundRect(bounds, 3f, 3f, border);

        var masks = context.RuntimeState.GetSegmentCellMasks(element.ObjectId, 1);
        var brightness = context.RuntimeState.GetSegmentCellBrightness(element.ObjectId, 1);
        var isLit = masks.Length > 0 && masks[0] != 0;
        var litAmount = brightness.Length > 0 ? Math.Clamp(brightness[0], 0d, 1d) : 1d;

        var onColor = SkiaColorParser.ParseOrDefault(element.OnColorHex, new SKColor(255, 64, 64));
        var offColor = SkiaColorParser.ParseOrDefault(element.OffColorHex, new SKColor(72, 24, 24));
        var segColor = isLit ? Lerp(offColor, onColor, litAmount) : offColor;

        var thickness = MathF.Max(2f, bounds.Height * 0.12f);
        var pad = thickness * 0.8f;
        using var segmentPaint = new SKPaint { Color = segColor, Style = SKPaintStyle.Fill, IsAntialias = true };

        foreach (var segment in BuildSegments(bounds, pad, thickness))
        {
            context.Canvas.DrawRoundRect(segment, thickness * 0.4f, thickness * 0.4f, segmentPaint);
        }
    }

    internal static IReadOnlyList<SKRect> BuildSegments(SKRect bounds, float padding, float thickness)
    {
        var left = bounds.Left + padding;
        var right = bounds.Right - padding;
        var top = bounds.Top + padding;
        var bottom = bounds.Bottom - padding;
        var mid = (top + bottom) * 0.5f;
        var span = right - left;
        var half = MathF.Max(1f, (span * 0.5f) - (thickness * 0.5f));

        var cx = (left + right) * 0.5f;

        return
        [
            SKRect.Create(cx - half, top, half * 2f, thickness),
            SKRect.Create(cx - half, mid - (thickness * 0.5f), half * 2f, thickness),
            SKRect.Create(cx - half, bottom - thickness, half * 2f, thickness)
        ];
    }

    private static SKColor Lerp(SKColor from, SKColor to, double t)
    {
        var clamped = Math.Clamp(t, 0d, 1d);
        byte Blend(byte a, byte b) => (byte)Math.Clamp(Math.Round(a + ((b - a) * clamped)), 0d, 255d);
        return new SKColor(Blend(from.Red, to.Red), Blend(from.Green, to.Green), Blend(from.Blue, to.Blue), 255);
    }
}
