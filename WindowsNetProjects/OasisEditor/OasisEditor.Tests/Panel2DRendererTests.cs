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


    [Fact]
    public void Render_WithLampRenderer_DoesNotThrow()
    {
        var renderer = new Panel2DRenderer([new LampElementRenderer()]);
        var runtimeState = new PanelRuntimeState();
        runtimeState.SetLampIntensity("lamp-1", 0.75d);
        using var surface = SKSurface.Create(new SKImageInfo(64, 64));

        renderer.Render(
            surface.Canvas,
            [
                new PanelElementModel
                {
                    Kind = PanelElementKind.Lamp,
                    IsVisible = true,
                    ObjectId = "lamp-1",
                    Name = "Lamp",
                    Width = 20,
                    Height = 20,
                    OnColorHex = "#FF0000",
                    OffColorHex = "#110000",
                    DisplayText = "HI",
                    TextColorHex = "#FFFFFF",
                    TextBoxFontSize = "8"
                }
            ],
            runtimeState,
            PanelViewportTransform.Identity);
    }

    [Fact]
    public void Render_WithReelRenderer_DoesNotThrow()
    {
        var renderer = new Panel2DRenderer([new ReelElementRenderer()]);
        var runtimeState = new PanelRuntimeState();
        runtimeState.SetReelPositionIfChanged("reel-1", 31);
        using var surface = SKSurface.Create(new SKImageInfo(64, 64));

        renderer.Render(
            surface.Canvas,
            [
                new PanelElementModel
                {
                    Kind = PanelElementKind.Reel,
                    IsVisible = true,
                    ObjectId = "reel-1",
                    Name = "Reel",
                    Width = 24,
                    Height = 24,
                    Stops = 16
                }
            ],
            runtimeState,
            PanelViewportTransform.Identity);
    }

    [Theory]
    [InlineData(0, 12, 0d)]
    [InlineData(96, 12, 0d)]
    [InlineData(-1, 12, 95d / 96d)]
    [InlineData(24, 12, 0.25d)]
    public void ReelOffset_ComputesExpectedWrappedOffset(int position, int stops, double expected)
    {
        var actual = ReelElementRenderer.ComputeWrappedOffset(position, stops);

        Assert.Equal(expected, actual, 4);
    }


    [Fact]
    public void Render_WithSevenSegmentRenderer_DoesNotThrow()
    {
        var renderer = new Panel2DRenderer([new SevenSegmentElementRenderer()]);
        var runtimeState = new PanelRuntimeState();
        runtimeState.SetSegmentCellMasksIfChanged("seg-1", [0x3F]);
        runtimeState.SetSegmentCellBrightnessIfChanged("seg-1", [0.8d]);
        using var surface = SKSurface.Create(new SKImageInfo(64, 64));

        renderer.Render(
            surface.Canvas,
            [
                new PanelElementModel
                {
                    Kind = PanelElementKind.SevenSegment,
                    IsVisible = true,
                    ObjectId = "seg-1",
                    Name = "Seven",
                    Width = 24,
                    Height = 36,
                    OnColorHex = "#FF4444",
                    OffColorHex = "#220000"
                }
            ],
            runtimeState,
            PanelViewportTransform.Identity);
    }


    [Fact]
    public void Render_WithAlphaRenderer_DoesNotThrow()
    {
        var renderer = new Panel2DRenderer([new AlphaElementRenderer()]);
        var runtimeState = new PanelRuntimeState();
        runtimeState.SetSegmentCellMasksIfChanged("alpha-1", [0x3FFF, 0x0001, 0x00FF]);
        runtimeState.SetSegmentCellBrightnessIfChanged("alpha-1", [1d, 0.5d, 0.75d]);
        using var surface = SKSurface.Create(new SKImageInfo(320, 64));

        renderer.Render(
            surface.Canvas,
            [
                new PanelElementModel
                {
                    Kind = PanelElementKind.Alpha,
                    IsVisible = true,
                    ObjectId = "alpha-1",
                    Name = "Alpha",
                    Width = 240,
                    Height = 32,
                    OnColorHex = "#FF4444",
                    OffColorHex = "#220000"
                }
            ],
            runtimeState,
            PanelViewportTransform.Identity);
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
