using OasisEditor.Rendering;
using SkiaSharp;
using Xunit;

namespace OasisEditor.Tests;

public sealed class LiveArtworkRenderingTests
{
    [Fact]
    public void Alignment_ProjectsRegisteredQuadAndClipsOutsideDestination()
    {
        using var source = new SKBitmap(100, 100);
        source.Erase(SKColors.Red);
        using (var canvas = new SKCanvas(source))
        using (var paint = new SKPaint { Color = SKColors.Lime }) canvas.DrawRect(25, 25, 50, 50, paint);
        using var image = SKImage.FromBitmap(source);
        using var surface = SKSurface.Create(new SKImageInfo(200, 200));
        surface.Canvas.Clear(SKColors.Blue);
        var value = new FaceArtworkOverrideModel { PerspectiveRegistration = new()
        {
            TopLeft = new() { X = .25, Y = .25 }, TopRight = new() { X = .75, Y = .25 },
            BottomRight = new() { X = .75, Y = .75 }, BottomLeft = new() { X = .25, Y = .75 }
        }};
        FaceArtworkAlignmentRenderer.Draw(surface.Canvas, image, value, SKRect.Create(50, 50, 100, 100), 1);
        using var result = SKBitmap.FromImage(surface.Snapshot());
        Assert.Equal(SKColors.Lime, result.GetPixel(100, 100));
        Assert.Equal(SKColors.Blue, result.GetPixel(49, 100));
        Assert.Equal(SKColors.Blue, result.GetPixel(150, 100));
    }

    [Fact]
    public void Alignment_ZeroOpacityLeavesBaseUntouched()
    {
        using var source = new SKBitmap(16, 16); source.Erase(SKColors.Red);
        using var image = SKImage.FromBitmap(source);
        using var surface = SKSurface.Create(new SKImageInfo(32, 32)); surface.Canvas.Clear(SKColors.Blue);
        FaceArtworkAlignmentRenderer.Draw(surface.Canvas, image, new(), SKRect.Create(0, 0, 32, 32), 0);
        using var result = SKBitmap.FromImage(surface.Snapshot());
        Assert.Equal(SKColors.Blue, result.GetPixel(16, 16));
    }

    [Fact]
    public void TextLamp_ZoomRerasterizesGlyphsInsteadOfEnlargingCachedPixels()
    {
        using var surface = SKSurface.Create(new SKImageInfo(640, 160));
        surface.Canvas.Clear(SKColors.Black); surface.Canvas.Scale(4);
        var context = new PanelElementRenderContext(surface.Canvas, new MachineRuntimeState(), new(4, 0, 0));
        new LampElementRenderer().Render(context, new PanelElementModel
        {
            ObjectId = "zoom-quality-regression", Kind = PanelElementKind.Lamp, Width = 160, Height = 40,
            DisplayText = "Sharp text", TextBoxFontSize = "14", OffColorHex = "#FF000000", TextColorHex = "#FFFFFFFF"
        });
        using var result = SKBitmap.FromImage(surface.Snapshot());
        var detailedBlocks = 0;
        for (var y = 8; y < 152; y += 4)
        for (var x = 8; x < 632; x += 4)
        {
            var first = result.GetPixel(x, y);
            if (Enumerable.Range(0, 16).Any(i => result.GetPixel(x + i % 4, y + i / 4) != first)) detailedBlocks++;
        }
        Assert.True(detailedBlocks > 100, $"Only {detailedBlocks} blocks contain detail beyond the cached native pixels.");
    }
}
