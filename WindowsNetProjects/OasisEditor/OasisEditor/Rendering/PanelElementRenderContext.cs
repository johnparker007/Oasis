using SkiaSharp;

namespace OasisEditor.Rendering;

internal readonly record struct PanelElementRenderContext(
    SKCanvas Canvas,
    PanelRuntimeState RuntimeState,
    PanelViewportTransform ViewportTransform);
