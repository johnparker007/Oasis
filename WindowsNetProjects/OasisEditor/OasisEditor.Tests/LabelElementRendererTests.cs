using OasisEditor.Rendering;
using SkiaSharp;
using Xunit;

namespace OasisEditor.Tests;

public sealed class LabelElementRendererTests
{
    [Fact]
    public void Render_StaticLabel_DrawsTextPixelsUsingTextColor()
    {
        using var surface = SKSurface.Create(new SKImageInfo(80, 32));
        surface.Canvas.Clear(SKColors.Transparent);
        var element = CreateLabel(textColorHex: "#FFFF0000");

        new LabelElementRenderer().Render(new PanelElementRenderContext(surface.Canvas, new PanelRuntimeState(), PanelViewportTransform.Identity), element);

        Assert.Contains(ReadPixels(surface), p => p.Alpha > 0 && p.Red > 128 && p.Green < 80 && p.Blue < 80);
    }

    [Fact]
    public void Render_EmptyLabel_DrawsNothing()
    {
        using var surface = SKSurface.Create(new SKImageInfo(80, 32));
        surface.Canvas.Clear(SKColors.Transparent);
        var element = CreateLabel(displayText: "   ");

        new LabelElementRenderer().Render(new PanelElementRenderContext(surface.Canvas, new PanelRuntimeState(), PanelViewportTransform.Identity), element);

        Assert.All(ReadPixels(surface), p => Assert.Equal(0, p.Alpha));
    }

    [Fact]
    public void Render_LampControlledLabel_UsesMachineLampStateIncludingLampZero()
    {
        using var offSurface = SKSurface.Create(new SKImageInfo(80, 32));
        offSurface.Canvas.Clear(SKColors.Transparent);
        var offRuntime = new PanelRuntimeState();
        var element = CreateLabel(lampNumber: 0);

        new LabelElementRenderer().Render(new PanelElementRenderContext(offSurface.Canvas, offRuntime, PanelViewportTransform.Identity), element);

        Assert.All(ReadPixels(offSurface), p => Assert.Equal(0, p.Alpha));

        using var onSurface = SKSurface.Create(new SKImageInfo(80, 32));
        onSurface.Canvas.Clear(SKColors.Transparent);
        var onRuntime = new PanelRuntimeState();
        onRuntime.SetLampIntensity(MachineObjectReference.Lamp(0), 1d);

        new LabelElementRenderer().Render(new PanelElementRenderContext(onSurface.Canvas, onRuntime, PanelViewportTransform.Identity), element);

        Assert.Contains(ReadPixels(onSurface), p => p.Alpha > 0);
    }

    [Fact]
    public void Render_InvalidFontAndColor_DoesNotThrow()
    {
        using var surface = SKSurface.Create(new SKImageInfo(80, 32));
        var element = PanelElementModelCloner.Clone(CreateLabel(textColorHex: "not-a-color"));
        element = new PanelElementModel
        {
            ObjectId = element.ObjectId, Kind = element.Kind, X = element.X, Y = element.Y, Width = element.Width, Height = element.Height, DisplayText = element.DisplayText, TextColorHex = element.TextColorHex, IsVisible = element.IsVisible, LampNumber = element.LampNumber,
            TextBoxFontName = "Definitely Missing Font",
            TextBoxFontStyle = "Bold Italic",
            TextBoxFontSize = "bad"
        };

        var exception = Record.Exception(() => new LabelElementRenderer().Render(new PanelElementRenderContext(surface.Canvas, new PanelRuntimeState(), PanelViewportTransform.Identity), element));

        Assert.Null(exception);
    }

    private static PanelElementModel CreateLabel(string displayText = "TEST", string? textColorHex = "#FFFFFFFF", int? lampNumber = null) => new()
    {
        ObjectId = "label-1",
        Kind = PanelElementKind.Label,
        X = 0,
        Y = 0,
        Width = 80,
        Height = 32,
        DisplayText = displayText,
        TextColorHex = textColorHex,
        TextBoxFontName = "Tahoma",
        TextBoxFontStyle = "Regular",
        TextBoxFontSize = "12",
        LampNumber = lampNumber,
        IsVisible = true
    };

    private static IReadOnlyList<SKColor> ReadPixels(SKSurface surface)
    {
        using var image = surface.Snapshot();
        using var bitmap = SKBitmap.FromImage(image);
        var pixels = new List<SKColor>();
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                pixels.Add(bitmap.GetPixel(x, y));
            }
        }

        return pixels;
    }
}
