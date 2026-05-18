using SkiaSharp;

namespace OasisEditor.Rendering;

internal sealed class Panel2DRenderer : IPanel2DRenderer
{
    private readonly IReadOnlyDictionary<PanelElementKind, IPanelElementRenderer> _renderersByKind;

    public Panel2DRenderer(IEnumerable<IPanelElementRenderer> renderers)
    {
        ArgumentNullException.ThrowIfNull(renderers);

        _renderersByKind = renderers
            .GroupBy(renderer => renderer.Kind)
            .ToDictionary(group => group.Key, group => group.Last());
    }

    public void Render(SKCanvas canvas, IReadOnlyList<PanelElementModel> elements, PanelRuntimeState runtimeState, PanelViewportTransform viewportTransform)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(elements);
        ArgumentNullException.ThrowIfNull(runtimeState);

        var context = new PanelElementRenderContext(canvas, runtimeState, viewportTransform);
        foreach (var element in elements)
        {
            if (!element.IsVisible)
            {
                continue;
            }

            if (_renderersByKind.TryGetValue(element.Kind, out var renderer))
            {
                renderer.Render(context, element);
            }
        }
    }
}
