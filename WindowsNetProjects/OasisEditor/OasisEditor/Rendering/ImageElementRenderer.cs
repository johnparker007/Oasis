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

        if (!SkiaPanelImageLoader.TryGetImage(element.AssetPath, out var image))
        {
            return;
        }

        context.Canvas.DrawImage(image, bounds);
    }
}
