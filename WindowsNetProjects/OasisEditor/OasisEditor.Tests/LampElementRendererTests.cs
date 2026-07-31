using System;
using System.IO;
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

    [Fact]
    public void Render_PlainFill_DrawsBorderOnlyWhenEnabled()
    {
        using var disabled = RenderLamp(new PanelElementModel { ObjectId = "lamp-no-border", Kind = PanelElementKind.Lamp, Width = 20, Height = 20, OnColorHex = "#FFFFFFFF", OffColorHex = "#FFFFFFFF", HasBorder = false }, 1d);
        using var enabled = RenderLamp(new PanelElementModel { ObjectId = "lamp-border", Kind = PanelElementKind.Lamp, Width = 20, Height = 20, OnColorHex = "#FFFFFFFF", OffColorHex = "#FFFFFFFF", HasBorder = true }, 1d);

        Assert.NotEqual(SKColors.Black, disabled.GetPixel(10, 0));
        Assert.Equal(SKColors.Black, enabled.GetPixel(10, 0));
    }

    [Fact]
    public void Render_TextLamp_DrawsBorderWhenEnabled()
    {
        using var bitmap = RenderLamp(new PanelElementModel { ObjectId = "lamp-text-border", Kind = PanelElementKind.Lamp, Width = 30, Height = 20, DisplayText = "HI", OnColorHex = "#FFFFFFFF", OffColorHex = "#FFFFFFFF", TextColorHex = "#FFFFFFFF", HasBorder = true }, 1d);

        Assert.Equal(SKColors.Black, bitmap.GetPixel(15, 0));
    }

    [Fact]
    public void Render_ImageBackedLamp_DrawsBorderWhenEnabledAndKeepsBorderAtZeroIntensity()
    {
        var imagePath = Path.Combine(Path.GetTempPath(), $"oasis-lamp-border-{Guid.NewGuid():N}.png");
        try
        {
            using (var image = SKSurface.Create(new SKImageInfo(4, 4)))
            {
                image.Canvas.Clear(SKColors.White);
                using var data = image.Snapshot().Encode(SKEncodedImageFormat.Png, 100);
                using var stream = File.Create(imagePath);
                data.SaveTo(stream);
            }

            using var onBitmap = RenderLamp(new PanelElementModel { ObjectId = "lamp-image-border", Kind = PanelElementKind.Lamp, Width = 20, Height = 20, AssetPath = imagePath, HasBorder = true }, 1d);
            using var offBitmap = RenderLamp(new PanelElementModel { ObjectId = "lamp-image-zero-border", Kind = PanelElementKind.Lamp, Width = 20, Height = 20, AssetPath = imagePath, HasBorder = true }, 0d);

            Assert.Equal(SKColors.Black, onBitmap.GetPixel(10, 0));
            Assert.Equal(SKColors.Black, offBitmap.GetPixel(10, 0));
            Assert.Equal(0, offBitmap.GetPixel(10, 10).Alpha);
        }
        finally
        {
            if (File.Exists(imagePath)) File.Delete(imagePath);
        }
    }

    [Theory]
    [InlineData(0d, 20, 40, 60, 255, 20, 40, 60)]
    [InlineData(0.5d, 110, 45, 40, 255, 65, 43, 50)]
    [InlineData(1d, 200, 50, 20, 255, 110, 45, 40)]
    public void Render_NormalImageLamp_FadesOnlySourcePixels(
        double intensity,
        byte opaqueRed, byte opaqueGreen, byte opaqueBlue, byte opaqueAlpha,
        byte partialRed, byte partialGreen, byte partialBlue)
    {
        WithTestLampImage(path =>
        {
            var background = new SKColor(20, 40, 60, 255);
            using var bitmap = RenderLamp(
                new PanelElementModel { ObjectId = "normal-image", Kind = PanelElementKind.Lamp, Width = 3, Height = 1, AssetPath = path },
                intensity,
                background);

            AssertColorNear(new SKColor(opaqueRed, opaqueGreen, opaqueBlue, opaqueAlpha), bitmap.GetPixel(0, 0));
            AssertColorNear(new SKColor(partialRed, partialGreen, partialBlue, 255), bitmap.GetPixel(1, 0));
            Assert.Equal(background, bitmap.GetPixel(2, 0));
        });
    }

    [Fact]
    public void Render_BlendedImageLamp_UsesAdditiveCompositingAndPreservesTransparentDestination()
    {
        WithTestLampImage(path =>
        {
            var background = new SKColor(20, 40, 60, 255);
            using var bitmap = RenderLamp(
                new PanelElementModel { ObjectId = "blended-image", Kind = PanelElementKind.Lamp, Width = 3, Height = 1, AssetPath = path, SourceBlend = true },
                0.5d,
                background);

            AssertColorNear(new SKColor(120, 65, 70, 255), bitmap.GetPixel(0, 0));
            AssertColorNear(new SKColor(70, 53, 65, 255), bitmap.GetPixel(1, 0));
            Assert.Equal(background, bitmap.GetPixel(2, 0));
        });
    }

    [Fact]
    public void Render_NormalImageLampAtFullIntensity_PreservesSourceColorAndAlpha()
    {
        WithTestLampImage(path =>
        {
            using var bitmap = RenderLamp(
                new PanelElementModel { ObjectId = "full-image", Kind = PanelElementKind.Lamp, Width = 3, Height = 1, AssetPath = path },
                1d);

            AssertColorNear(new SKColor(200, 50, 20, 255), bitmap.GetPixel(0, 0));
            AssertColorNear(new SKColor(200, 50, 20, 128), bitmap.GetPixel(1, 0));
            Assert.Equal(0, bitmap.GetPixel(2, 0).Alpha);
        });
    }

    [Fact]
    public void Render_FractionalImageLamp_TransparentPixelDoesNotDarkenBackgroundRectangle()
    {
        WithTestLampImage(path =>
        {
            var background = new SKColor(80, 120, 160, 255);
            using var bitmap = RenderLamp(
                new PanelElementModel { ObjectId = "regression", Kind = PanelElementKind.Lamp, Width = 3, Height = 1, AssetPath = path },
                0.5d,
                background);

            Assert.Equal(background, bitmap.GetPixel(2, 0));
        });
    }

    [Theory]
    [InlineData(0.25d)]
    [InlineData(0.5d)]
    [InlineData(1d)]
    public void Render_NormalImageLampScaledEdges_MatchPremultipliedSourceOverWithoutDarkHalo(double intensity)
    {
        WithEdgeLampImage(path =>
        {
            const int renderedSize = 16;
            var background = new SKColor(64, 96, 144, 255);
            using var actual = RenderLamp(
                new PanelElementModel { ObjectId = $"scaled-edge-{intensity}", Kind = PanelElementKind.Lamp, Width = renderedSize, Height = renderedSize, AssetPath = path },
                intensity,
                background);
            using var scaledSource = ScalePremultipliedSource(path, renderedSize);

            for (var y = 0; y < renderedSize; y++)
            {
                for (var x = 0; x < renderedSize; x++)
                {
                    var source = scaledSource.GetPixel(x, y);
                    var effectiveAlpha = (source.Alpha / 255d) * intensity;
                    var expected = new SKColor(
                        Composite(source.Red, background.Red, effectiveAlpha),
                        Composite(source.Green, background.Green, effectiveAlpha),
                        Composite(source.Blue, background.Blue, effectiveAlpha),
                        255);
                    AssertColorNear(expected, actual.GetPixel(x, y), tolerance: 3);
                }
            }

            Assert.Equal(background, actual.GetPixel(0, 0));
            var edge = actual.GetPixel(6, 2);
            Assert.True(edge.Red >= Math.Min(background.Red, scaledSource.GetPixel(6, 2).Red));
            Assert.True(edge.Green >= Math.Min(background.Green, scaledSource.GetPixel(6, 2).Green));
            Assert.True(edge.Blue >= Math.Min(background.Blue, scaledSource.GetPixel(6, 2).Blue));
        });
    }

    private static SKBitmap RenderLamp(PanelElementModel element, double intensity, SKColor? background = null)
    {
        using var surface = SKSurface.Create(new SKImageInfo(Math.Max(1, (int)element.Width), Math.Max(1, (int)element.Height)));
        surface.Canvas.Clear(background ?? SKColors.Transparent);
        var runtimeState = new PanelRuntimeState();
        runtimeState.SetLampIntensity(element.ObjectId, intensity);
        new LampElementRenderer().Render(new PanelElementRenderContext(surface.Canvas, runtimeState, PanelViewportTransform.Identity), element);
        return SKBitmap.FromImage(surface.Snapshot());
    }

    private static void WithTestLampImage(Action<string> test)
    {
        var imagePath = Path.Combine(Path.GetTempPath(), $"oasis-lamp-pixels-{Guid.NewGuid():N}.png");
        try
        {
            using var bitmap = new SKBitmap(3, 1, SKColorType.Rgba8888, SKAlphaType.Unpremul);
            bitmap.SetPixel(0, 0, new SKColor(200, 50, 20, 255));
            bitmap.SetPixel(1, 0, new SKColor(200, 50, 20, 128));
            bitmap.SetPixel(2, 0, new SKColor(10, 200, 240, 0));
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using (var stream = File.Create(imagePath)) data.SaveTo(stream);

            test(imagePath);
        }
        finally
        {
            if (File.Exists(imagePath)) File.Delete(imagePath);
        }
    }

    private static void WithEdgeLampImage(Action<string> test)
    {
        var imagePath = Path.Combine(Path.GetTempPath(), $"oasis-lamp-edge-{Guid.NewGuid():N}.png");
        try
        {
            using var bitmap = new SKBitmap(4, 4, SKColorType.Rgba8888, SKAlphaType.Unpremul);
            bitmap.Erase(SKColors.Transparent);
            bitmap.SetPixel(3, 0, new SKColor(90, 180, 240, 0));
            var edge = new SKColor(240, 180, 40, 96);
            bitmap.SetPixel(1, 0, edge);
            bitmap.SetPixel(2, 0, edge);
            bitmap.SetPixel(0, 1, edge);
            bitmap.SetPixel(3, 1, edge);
            bitmap.SetPixel(0, 2, edge);
            bitmap.SetPixel(3, 2, edge);
            bitmap.SetPixel(1, 3, edge);
            bitmap.SetPixel(2, 3, edge);
            bitmap.SetPixel(1, 1, new SKColor(255, 210, 60, 255));
            bitmap.SetPixel(2, 1, new SKColor(255, 210, 60, 255));
            bitmap.SetPixel(1, 2, new SKColor(255, 210, 60, 255));
            bitmap.SetPixel(2, 2, new SKColor(255, 210, 60, 255));
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using (var stream = File.Create(imagePath)) data.SaveTo(stream);

            test(imagePath);
        }
        finally
        {
            if (File.Exists(imagePath)) File.Delete(imagePath);
        }
    }

    private static SKBitmap ScalePremultipliedSource(string imagePath, int size)
    {
        using var codec = SKCodec.Create(imagePath);
        using var source = new SKBitmap(new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Bgra8888, SKAlphaType.Premul));
        Assert.Equal(SKCodecResult.Success, codec.GetPixels(source.Info, source.GetPixels()));
        using var image = SKImage.FromBitmap(source);
        using var surface = SKSurface.Create(new SKImageInfo(size, size, SKColorType.Bgra8888, SKAlphaType.Premul));
        surface.Canvas.Clear(SKColors.Transparent);
        surface.Canvas.DrawImage(image, SKRect.Create(size, size));
        return SKBitmap.FromImage(surface.Snapshot());
    }

    private static byte Composite(byte source, byte destination, double alpha)
        => (byte)Math.Clamp(Math.Round((source * alpha) + (destination * (1d - alpha))), 0d, 255d);

    private static void AssertColorNear(SKColor expected, SKColor actual, byte tolerance = 1)
    {
        Assert.InRange((int)actual.Red, Math.Max(0, expected.Red - tolerance), Math.Min(255, expected.Red + tolerance));
        Assert.InRange((int)actual.Green, Math.Max(0, expected.Green - tolerance), Math.Min(255, expected.Green + tolerance));
        Assert.InRange((int)actual.Blue, Math.Max(0, expected.Blue - tolerance), Math.Min(255, expected.Blue + tolerance));
        Assert.InRange((int)actual.Alpha, Math.Max(0, expected.Alpha - tolerance), Math.Min(255, expected.Alpha + tolerance));
    }

}
