using SkiaSharp;

namespace OasisEditor.Rendering;

internal readonly record struct PanelElementRenderContext(
    SKCanvas Canvas,
    MachineRuntimeState RuntimeState,
    PanelViewportTransform ViewportTransform);
