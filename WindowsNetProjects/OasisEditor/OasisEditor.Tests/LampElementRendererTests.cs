using OasisEditor.Rendering;
using SkiaSharp;
using Xunit;

namespace OasisEditor.Tests;

public sealed class LampElementRendererTests
{
    [Fact]
    public void ParseFontSize_ValidAndFallbackValues_AreDeterministic()
    {
        Assert.Equal(16d, LampElementRenderer.ParseFontSize("12"), 3);
        Assert.Equal(10.66666664d, LampElementRenderer.ParseFontSize(null), 6);
        Assert.Equal(10.66666664d, LampElementRenderer.ParseFontSize("bad"), 6);
    }

    [Fact]
    public void GetTextBounds_AppliesInsetAndClampsSize()
    {
        var bounds = SKRect.Create(10f, 20f, 100f, 50f);

        var textBounds = LampElementRenderer.GetTextBounds(bounds);

        Assert.Equal(18f, textBounds.Left, 3);
        Assert.Equal(25f, textBounds.Top, 3);
        Assert.Equal(84f, textBounds.Width, 3);
        Assert.Equal(40f, textBounds.Height, 3);
    }

    [Fact]
    public void GetTextBounds_WithTinyLamp_DoesNotReturnNonPositiveDimensions()
    {
        var bounds = SKRect.Create(0f, 0f, 0.2f, 0.2f);

        var textBounds = LampElementRenderer.GetTextBounds(bounds);

        Assert.True(textBounds.Width >= 1f);
        Assert.True(textBounds.Height >= 1f);
    }

    [Fact]
    public void WrapTextToPixelWidth_KeepsMfmeLikeWordWrapping()
    {
        using var paint = new SKPaint
        {
            TextSize = 16f,
            Typeface = SKTypeface.FromFamilyName("Tahoma") ?? SKTypeface.Default,
            IsAntialias = true
        };

        var targetWidth = paint.MeasureText("BIFF THE") + 0.5f;
        var lines = LampElementRenderer.WrapTextToPixelWidth("BIFF THE BOUNCER", targetWidth, paint);

        Assert.Collection(lines,
            line => Assert.Equal("BIFF THE", line.Text),
            line => Assert.Equal("BOUNCER", line.Text));
    }

    [Fact]
    public void GetEffectiveWrapWidth_AllowsSingleLineWhenItFitsLampBounds()
    {
        using var paint = new SKPaint { TextSize = 16f, Typeface = SKTypeface.FromFamilyName("Tahoma") ?? SKTypeface.Default };
        var text = "TO ACTIVATE PICKS";
        var measured = paint.MeasureText(text);

        var wrapWidth = LampElementRenderer.GetEffectiveWrapWidth(text, insetWidth: measured - 2d, lampWidth: measured, paint);

        Assert.True(wrapWidth >= measured);
    }

    [Fact]
    public void GetEffectiveWrapWidth_UsesInsetWidthWhenSingleLineDoesNotFit()
    {
        using var paint = new SKPaint { TextSize = 16f, Typeface = SKTypeface.FromFamilyName("Tahoma") ?? SKTypeface.Default };
        var text = "TO ACTIVATE PICKS";
        var measured = paint.MeasureText(text);

        var wrapWidth = LampElementRenderer.GetEffectiveWrapWidth(text, insetWidth: measured - 20d, lampWidth: measured - 20d, paint);

        Assert.True(wrapWidth < measured);
    }
    [Fact]
    public void Render_TextLampAtZeroIntensity_UsesStoredOffColor()
    {
        using var surface = SKSurface.Create(new SKImageInfo(24, 24));
        var runtimeState = new PanelRuntimeState();
        runtimeState.SetLampIntensity("lamp-1", 0d);
        var element = new PanelElementModel
        {
            ObjectId = "lamp-1",
            Kind = PanelElementKind.Lamp,
            X = 0,
            Y = 0,
            Width = 24,
            Height = 24,
            DisplayText = "HI",
            OnColorHex = "#FFFFFFFF",
            OffColorHex = "#FF204060",
            TextColorHex = "#FFFFFFFF",
            TextBoxFontSize = "8"
        };

        new LampElementRenderer().Render(new PanelElementRenderContext(surface.Canvas, runtimeState, PanelViewportTransform.Identity), element);

        using var image = surface.Snapshot();
        using var bitmap = SKBitmap.FromImage(image);
        var pixel = bitmap.GetPixel(1, 1);
        Assert.Equal(32, pixel.Red);
        Assert.Equal(64, pixel.Green);
        Assert.Equal(96, pixel.Blue);
        Assert.Equal(255, pixel.Alpha);
    }

    [Fact]
    public void Render_LampAtZeroIntensity_WithNoOffColor_UsesDefensiveRendererFallback()
    {
        using var surface = SKSurface.Create(new SKImageInfo(16, 16));
        var runtimeState = new PanelRuntimeState();
        runtimeState.SetLampIntensity("lamp-1", 0d);
        var element = new PanelElementModel
        {
            ObjectId = "lamp-1",
            Kind = PanelElementKind.Lamp,
            X = 0,
            Y = 0,
            Width = 16,
            Height = 16,
            OnColorHex = "#FFFFFFFF"
        };

        new LampElementRenderer().Render(new PanelElementRenderContext(surface.Canvas, runtimeState, PanelViewportTransform.Identity), element);

        using var image = surface.Snapshot();
        using var bitmap = SKBitmap.FromImage(image);
        var pixel = bitmap.GetPixel(1, 1);
        Assert.Equal(40, pixel.Red);
        Assert.Equal(0, pixel.Green);
        Assert.Equal(0, pixel.Blue);
        Assert.Equal(255, pixel.Alpha);
    }

}
