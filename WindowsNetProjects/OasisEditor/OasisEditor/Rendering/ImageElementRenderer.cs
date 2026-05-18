using SkiaSharp;

namespace OasisEditor.Rendering;

internal sealed class ImageElementRenderer : IPanelElementRenderer
{
    public PanelElementKind Kind => PanelElementKind.Image;

    public void Render(in PanelElementRenderContext context, PanelElementModel element)
    {
        var bounds = SKRect.Create((float)element.X, (float)element.Y, (float)element.Width, (float)element.Height);
        if (bounds.Width <= 0f || bounds.Height <= 0f)
        {
            return;
        }

        using var fill = new SKPaint
        {
            Color = new SKColor(0x1F, 0x29, 0x37),
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };
        using var stroke = new SKPaint
        {
            Color = new SKColor(0x60, 0x70, 0x80),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1f,
            IsAntialias = true
        };
        using var diagonal = new SKPaint
        {
            Color = new SKColor(0x94, 0xA3, 0xB8),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1f,
            IsAntialias = true
        };

        context.Canvas.DrawRect(bounds, fill);
        context.Canvas.DrawLine(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom, diagonal);
        context.Canvas.DrawLine(bounds.Right, bounds.Top, bounds.Left, bounds.Bottom, diagonal);
        context.Canvas.DrawRect(bounds, stroke);
    }
}
