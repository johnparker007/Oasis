using OasisEditor.Rendering;
using SkiaSharp;
using Xunit;

namespace OasisEditor.Tests;

public sealed class Panel2DRendererTests
{
    [Fact]
    public void Render_DispatchesVisibleElementsToMatchingRenderer()
    {
        var lampRenderer = new FakeRenderer(PanelElementKind.Lamp);
        var reelRenderer = new FakeRenderer(PanelElementKind.Reel);
        var renderer = new Panel2DRenderer([lampRenderer, reelRenderer]);
        var runtimeState = new PanelRuntimeState();
        using var surface = SKSurface.Create(new SKImageInfo(64, 64));

        renderer.Render(
            surface.Canvas,
            [
                new PanelElementModel { Kind = PanelElementKind.Lamp, IsVisible = true, ObjectId = "lamp-1", Name = "Lamp" },
                new PanelElementModel { Kind = PanelElementKind.Reel, IsVisible = true, ObjectId = "reel-1", Name = "Reel" },
                new PanelElementModel { Kind = PanelElementKind.Alpha, IsVisible = true, ObjectId = "alpha-1", Name = "Alpha" }
            ],
            runtimeState,
            PanelViewportTransform.Identity);

        Assert.Equal(1, lampRenderer.Rendered.Count);
        Assert.Equal("lamp-1", lampRenderer.Rendered[0].ObjectId);
        Assert.Equal(1, reelRenderer.Rendered.Count);
        Assert.Equal("reel-1", reelRenderer.Rendered[0].ObjectId);
    }

    [Fact]
    public void Render_SkipsHiddenElements()
    {
        var lampRenderer = new FakeRenderer(PanelElementKind.Lamp);
        var renderer = new Panel2DRenderer([lampRenderer]);
        using var surface = SKSurface.Create(new SKImageInfo(64, 64));

        renderer.Render(
            surface.Canvas,
            [
                new PanelElementModel { Kind = PanelElementKind.Lamp, IsVisible = false, ObjectId = "lamp-hidden", Name = "Hidden" }
            ],
            new PanelRuntimeState(),
            PanelViewportTransform.Identity);

        Assert.Empty(lampRenderer.Rendered);
    }

    private sealed class FakeRenderer(PanelElementKind kind) : IPanelElementRenderer
    {
        public PanelElementKind Kind { get; } = kind;

        public List<PanelElementModel> Rendered { get; } = [];

        public void Render(in PanelElementRenderContext context, PanelElementModel element)
        {
            Rendered.Add(element);
        }
    }
}
