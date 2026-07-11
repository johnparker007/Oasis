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
    public void Render_BundledMfmeFont_MatchesSharedTypefaceRendering()
    {
        using var actualSurface = SKSurface.Create(new SKImageInfo(120, 48));
        actualSurface.Canvas.Clear(SKColors.Transparent);
        var element = CreateLabel(
            displayText: "MFME",
            textColorHex: "#FFFFFFFF",
            textBoxFontName: "MFME",
            textBoxFontStyle: "Regular",
            textBoxFontSize: "14");

        new LabelElementRenderer().Render(new PanelElementRenderContext(actualSurface.Canvas, new PanelRuntimeState(), PanelViewportTransform.Identity), element);

        using var expectedSurface = SKSurface.Create(new SKImageInfo(120, 48));
        expectedSurface.Canvas.Clear(SKColors.Transparent);
        using var textPaint = new SKPaint
        {
            Color = SKColors.White,
            IsAntialias = true,
            TextSize = (float)LampElementRenderer.ParseFontSize(element.TextBoxFontSize),
            Typeface = MfmeTypefaceResolver.Resolve(element.TextBoxFontName, element.TextBoxFontStyle)
        };
        var bounds = SKRect.Create(0f, 0f, 120f, 48f);
        var textBounds = LampElementRenderer.GetTextBounds(bounds);
        var fontMetrics = textPaint.FontMetrics;
        var measuredLineHeight = Math.Abs(fontMetrics.Ascent) + Math.Abs(fontMetrics.Descent) + Math.Abs(fontMetrics.Leading);
        var lineHeight = Math.Max(1d, measuredLineHeight > 0f ? measuredLineHeight : textPaint.TextSize * 1.2d);
        var wrapWidth = LampElementRenderer.GetEffectiveWrapWidth(element.DisplayText, textBounds.Width, bounds.Width, textPaint);
        var line = Assert.Single(LampElementRenderer.WrapTextToPixelWidth(element.DisplayText, wrapWidth, textPaint));
        var baselineOffset = Math.Abs(fontMetrics.Ascent) > 0f ? Math.Abs(fontMetrics.Ascent) : textPaint.TextSize;
        var x = textBounds.Left + ((textBounds.Width - line.Width) / 2d);
        var y = textBounds.Top + ((textBounds.Height - lineHeight) / 2d) + baselineOffset;
        expectedSurface.Canvas.DrawText(line.Text, (float)x, (float)y, textPaint);

        Assert.Equal(ReadPixels(expectedSurface), ReadPixels(actualSurface));
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

    private static PanelElementModel CreateLabel(
        string displayText = "TEST",
        string? textColorHex = "#FFFFFFFF",
        int? lampNumber = null,
        string? textBoxFontName = "Tahoma",
        string? textBoxFontStyle = "Regular",
        string? textBoxFontSize = "12") => new()
    {
        ObjectId = "label-1",
        Kind = PanelElementKind.Label,
        X = 0,
        Y = 0,
        Width = 80,
        Height = 32,
        DisplayText = displayText,
        TextColorHex = textColorHex,
        TextBoxFontName = textBoxFontName,
        TextBoxFontStyle = textBoxFontStyle,
        TextBoxFontSize = textBoxFontSize,
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
