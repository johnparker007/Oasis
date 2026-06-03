using SkiaSharp;

namespace OasisEditor.Rendering;

internal sealed class VfdDotMatrixElementRenderer : IPanelElementRenderer
{
    internal const int CellCount = 16;
    internal const int CellWidth = 6;
    internal const int Columns = CellCount * CellWidth;
    internal const int Rows = 8;
    internal const int DotCount = Columns * Rows;

    public PanelElementKind Kind => PanelElementKind.VfdDotMatrix;

    public void Render(in PanelElementRenderContext context, PanelElementModel element)
    {
        var bounds = SKRect.Create((float)element.X, (float)element.Y, (float)element.Width, (float)element.Height);
        if (bounds.Width <= 0f || bounds.Height <= 0f)
        {
            return;
        }

        var onColor = SkiaColorParser.ParseOrDefault(element.OnColorHex, new SKColor(255, 64, 64));
        var offColor = ScaleBrightness(onColor, 0.10d);
        var backgroundColor = ScaleBrightness(onColor, 0.04d);
        var dots = context.RuntimeState.GetVfdDotMatrixDots(element.ObjectId, DotCount);

        using var backgroundPaint = new SKPaint { Color = backgroundColor, Style = SKPaintStyle.Fill, IsAntialias = true };
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

        var dotPitch = Math.Min(contentBounds.Width / Columns, contentBounds.Height / Rows);
        if (dotPitch <= 0f)
        {
            return;
        }

        var matrixWidth = dotPitch * Columns;
        var matrixHeight = dotPitch * Rows;
        var originX = contentBounds.Left + ((contentBounds.Width - matrixWidth) * 0.5f);
        var originY = contentBounds.Top + ((contentBounds.Height - matrixHeight) * 0.5f);
        var dotSize = Math.Max(0.5f, dotPitch * 0.72f);
        var dotInset = (dotPitch - dotSize) * 0.5f;

        using var dotPaint = new SKPaint { Style = SKPaintStyle.Fill, IsAntialias = true };
        for (var y = 0; y < Rows; y++)
        {
            for (var x = 0; x < Columns; x++)
            {
                var index = (y * Columns) + x;
                dotPaint.Color = index < dots.Length && dots[index] ? onColor : offColor;
                context.Canvas.DrawOval(
                    SKRect.Create(originX + (x * dotPitch) + dotInset, originY + (y * dotPitch) + dotInset, dotSize, dotSize),
                    dotPaint);
            }
        }
    }

    private static SKColor ScaleBrightness(SKColor color, double factor)
    {
        var clamped = Math.Clamp(factor, 0d, 1d);
        byte Scale(byte value) => (byte)Math.Clamp(Math.Round(value * clamped), 0d, 255d);
        return new SKColor(Scale(color.Red), Scale(color.Green), Scale(color.Blue), 255);
    }
}
