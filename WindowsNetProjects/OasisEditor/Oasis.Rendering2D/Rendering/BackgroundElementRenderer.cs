using SkiaSharp;

namespace OasisEditor.Rendering;

internal sealed class BackgroundElementRenderer : IPanelElementRenderer
{
    public PanelElementKind Kind => PanelElementKind.Background;

    public void Render(in PanelElementRenderContext context, PanelElementModel element)
    {
        var bounds = SKRect.Create((float)element.X, (float)element.Y, (float)element.Width, (float)element.Height);
        if (bounds.Width <= 0f || bounds.Height <= 0f)
        {
            return;
        }

        if (SkiaPanelImageLoader.TryGetImage(element.AssetPath, out var backgroundImage))
        {
            using var imagePaint = new SKPaint { FilterQuality = context.ViewportTransform.NormalizedRasterMagnification >= 1d ? SKFilterQuality.None : SKFilterQuality.Medium };
            context.Canvas.DrawImage(backgroundImage, bounds, imagePaint);
            return;
        }

        using var paint = new SKPaint
        {
            Color = SkiaColorParser.ParseOrDefault(element.OnColorHex, new SKColor(0x2B, 0x2B, 0x2B)),
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        context.Canvas.DrawRect(bounds, paint);
    }

}
