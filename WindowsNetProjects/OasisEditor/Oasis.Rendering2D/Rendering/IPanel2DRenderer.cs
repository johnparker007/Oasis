using SkiaSharp;

namespace OasisEditor.Rendering;

internal interface IPanel2DRenderer
{
    void Render(SKCanvas canvas, IReadOnlyList<PanelElementModel> elements, MachineRuntimeState runtimeState, PanelViewportTransform viewportTransform);
}
