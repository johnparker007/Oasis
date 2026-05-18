using SkiaSharp;

namespace OasisEditor.Rendering;

internal interface IPanelElementRenderer
{
    PanelElementKind Kind { get; }

    void Render(in PanelElementRenderContext context, PanelElementModel element);
}
