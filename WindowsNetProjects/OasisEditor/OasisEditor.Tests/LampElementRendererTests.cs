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
}
