using SkiaSharp;

namespace OasisEditor.Rendering;

internal sealed class RectangleElementRenderer : IPanelElementRenderer
{
    public PanelElementKind Kind => PanelElementKind.Rectangle;

    public void Render(in PanelElementRenderContext context, PanelElementModel element)
    {
        var bounds = SKRect.Create((float)element.X, (float)element.Y, (float)element.Width, (float)element.Height);
        if (bounds.Width <= 0f || bounds.Height <= 0f)
        {
            return;
        }

        using var fill = new SKPaint
        {
            Color = SkiaColorParser.ParseOrDefault(element.OnColorHex, new SKColor(0x33, 0x3A, 0x45)),
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };
        using var stroke = new SKPaint
        {
            Color = new SKColor(0x70, 0x80, 0x90),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.5f,
            IsAntialias = true
        };

        context.Canvas.DrawRect(bounds, fill);
        context.Canvas.DrawRect(bounds, stroke);
    }
}
