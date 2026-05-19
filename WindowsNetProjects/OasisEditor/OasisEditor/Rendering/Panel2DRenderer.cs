using System.Diagnostics;
using SkiaSharp;

namespace OasisEditor.Rendering;

internal sealed class Panel2DRenderer : IPanel2DRenderer
{
    private readonly IReadOnlyDictionary<PanelElementKind, IPanelElementRenderer> _renderersByKind;
    private readonly string _viewName;

    public Panel2DRenderer(IEnumerable<IPanelElementRenderer> renderers, string viewName = "SkiaView")
    {
        ArgumentNullException.ThrowIfNull(renderers);
        _viewName = string.IsNullOrWhiteSpace(viewName) ? "SkiaView" : viewName;
        _renderersByKind = renderers.GroupBy(renderer => renderer.Kind).ToDictionary(group => group.Key, group => group.Last());
    }

    public void Render(SKCanvas canvas, IReadOnlyList<PanelElementModel> elements, PanelRuntimeState runtimeState, PanelViewportTransform viewportTransform)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(elements);
        ArgumentNullException.ThrowIfNull(runtimeState);

        LampElementRenderer.ResetDiagnosticsCounters();
        SevenSegmentElementRenderer.ResetDiagnosticsCounters();
        var frame = SkiaRenderDiagnostics.BeginFrame(_viewName);
        var context = new PanelElementRenderContext(canvas, runtimeState, viewportTransform);

        var backgroundElapsed = TimeSpan.Zero;
        var lampElapsed = TimeSpan.Zero;
        var alphaElapsed = TimeSpan.Zero;
        var sevenElapsed = TimeSpan.Zero;
        var reelElapsed = TimeSpan.Zero;
        var backgroundCount = 0;
        var lampCount = 0;
        var textLampCount = 0;
        var alphaCount = 0;
        var sevenCount = 0;
        var reelCount = 0;

        foreach (var element in elements)
        {
            if (!element.IsVisible)
            {
                continue;
            }

            if (!_renderersByKind.TryGetValue(element.Kind, out var renderer))
            {
                continue;
            }

            var sw = Stopwatch.StartNew();
            renderer.Render(context, element);
            sw.Stop();

            switch (element.Kind)
            {
                case PanelElementKind.Background:
                    backgroundCount++;
                    backgroundElapsed += sw.Elapsed;
                    break;
                case PanelElementKind.Lamp:
                    lampCount++;
                    lampElapsed += sw.Elapsed;
                    if (!string.IsNullOrWhiteSpace(element.DisplayText))
                    {
                        textLampCount++;
                    }
                    break;
                case PanelElementKind.Alpha:
                    alphaCount++;
                    alphaElapsed += sw.Elapsed;
                    break;
                case PanelElementKind.SevenSegment:
                    sevenCount++;
                    sevenElapsed += sw.Elapsed;
                    break;
                case PanelElementKind.Reel:
                    reelCount++;
                    reelElapsed += sw.Elapsed;
                    break;
            }
        }

        frame.Complete(new SkiaRenderDiagnostics.FrameData(
            _viewName,
            TimeSpan.Zero,
            backgroundElapsed,
            lampElapsed,
            LampElementRenderer.DiagnosticsTextElapsed,
            alphaElapsed,
            sevenElapsed,
            reelElapsed,
            elements.Count,
            backgroundCount,
            lampCount,
            textLampCount,
            alphaCount,
            sevenCount,
            reelCount,
            LampElementRenderer.DiagnosticsTextLayoutCount,
            LampElementRenderer.DiagnosticsTextDrawCount,
            LampElementRenderer.DiagnosticsTextLayoutCacheHits,
            LampElementRenderer.DiagnosticsTextLayoutCacheMisses,
            LampElementRenderer.DiagnosticsTextVisualCacheHits,
            LampElementRenderer.DiagnosticsTextVisualCacheMisses,
            SevenSegmentElementRenderer.DiagnosticsCacheHits,
            SevenSegmentElementRenderer.DiagnosticsCacheMisses));
    }
}
