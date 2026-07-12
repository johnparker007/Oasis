using MfmeFmlDecoder.src.Model;
using MfmeFmlDecoder.src.Model.Component;
using OasisEditor.Features.FmlImport;
using OasisEditor.Features.LayoutImport;
using Xunit;

namespace OasisEditor.Tests;

public sealed class FmlBackgroundImportTests
{
    [Fact]
    public void SolidColourBackground_MapsNativeBackgroundAndOrdersDisplaysInFront()
    {
        var background = new Background { X = 0, Y = 0, Width = 570, Height = 663 };
        background.Colours["Colour"] = "#F0F0F0FF";
        var reel = new Reel { X = 10, Y = 10, Width = 50, Height = 100 };
        reel.UInt32s["Stops"] = 20;
        var sevenSeg = new SevenSeg { X = 70, Y = 10, Width = 40, Height = 20 };
        var alpha = new Alpha { X = 120, Y = 10, Width = 80, Height = 20 };
        var lamp = new Lamp { X = 20, Y = 140, Width = 30, Height = 30, SublampTable = [new LampSublampTableEntry(1, 1)] };

        var layout = new Layout([background, reel, sevenSeg, alpha, lamp]);
        var images = new Dictionary<FmlDecodedImageKey, string>();

        var classification = FmlBackgroundClassifier.Classify(layout, images);
        var mapResult = new FmlToOasisMapper().Map(layout, images);
        var ordered = FmlPanelElementOrdering.ArrangeForBackgroundMode(mapResult.Elements, classification.Mode).ToArray();

        Assert.Equal(FmlBackgroundMode.SolidColourBackground, classification.Mode);
        var mappedBackground = Assert.Single(mapResult.Elements.Where(element => element.Kind == PanelElementKind.Background));
        Assert.Null(mappedBackground.AssetPath);
        Assert.Equal("#FFF0F0F0", mappedBackground.OnColorHex);
        Assert.Equal(0, mappedBackground.SourceComponentIndex);
        Assert.Equal(0, Array.IndexOf(ordered, mappedBackground));
        Assert.True(Array.FindIndex(ordered, element => element.Kind == PanelElementKind.Reel) > 0);
        Assert.True(Array.FindIndex(ordered, element => element.Kind == PanelElementKind.SevenSegment) > 0);
        Assert.True(Array.FindIndex(ordered, element => element.Kind == PanelElementKind.Alpha) > 0);
        Assert.True(Array.FindIndex(ordered, element => element.Kind == PanelElementKind.Lamp) > Array.FindIndex(ordered, element => element.Kind == PanelElementKind.Alpha));
    }

    [Fact]
    public void ImageBackedBackground_UsesMainBackgroundImageAndPreservesDisplayCutoutOrdering()
    {
        var background = new Background { X = 0, Y = 0, Width = 300, Height = 200 };
        background.Colours["Colour"] = "#000000FF";
        var reel = new Reel { X = 10, Y = 10, Width = 50, Height = 100 };
        reel.UInt32s["Stops"] = 20;
        var lamp = new Lamp { X = 20, Y = 140, Width = 30, Height = 30, SublampTable = [new LampSublampTableEntry(1, 1)] };
        var layout = new Layout([background, reel, lamp]);
        var images = new Dictionary<FmlDecodedImageKey, string>
        {
            [new FmlDecodedImageKey(0, "Background")] = "background/bg.bmp"
        };

        var classification = FmlBackgroundClassifier.Classify(layout, images);
        var mapResult = new FmlToOasisMapper().Map(layout, images);
        var ordered = FmlPanelElementOrdering.ArrangeForBackgroundMode(mapResult.Elements, classification.Mode).ToArray();

        Assert.Equal(FmlBackgroundMode.ImageBackedBackground, classification.Mode);
        Assert.Equal("background/bg.bmp", classification.MainBackgroundImagePath);
        Assert.Contains(mapResult.Elements, element => element.Kind == PanelElementKind.Background && element.AssetPath == "background/bg.bmp");
        Assert.Equal(PanelElementKind.Reel, ordered[0].Kind);
        Assert.True(Array.FindIndex(ordered, element => element.Kind == PanelElementKind.Background) > Array.FindIndex(ordered, element => element.Kind == PanelElementKind.Reel));
        Assert.True(Array.FindIndex(ordered, element => element.Kind == PanelElementKind.Lamp) > Array.FindIndex(ordered, element => element.Kind == PanelElementKind.Background));
    }

    [Fact]
    public void SolidColourBackground_WithLaterBitmap_DoesNotSelectBitmapAsMainBackground()
    {
        var background = new Background { X = 0, Y = 0, Width = 300, Height = 200 };
        background.Colours["Colour"] = "#F0F0F0FF";
        var bitmap = new Bitmap { X = 50, Y = 50, Width = 40, Height = 40 };
        var reel = new Reel { X = 10, Y = 10, Width = 50, Height = 100 };
        reel.UInt32s["Stops"] = 20;
        var layout = new Layout([background, reel, bitmap]);
        var images = new Dictionary<FmlDecodedImageKey, string>
        {
            [new FmlDecodedImageKey(2, "Bitmap")] = "background/overlay.bmp"
        };

        var classification = FmlBackgroundClassifier.Classify(layout, images);
        var mapResult = new FmlToOasisMapper().Map(layout, images);
        var ordered = FmlPanelElementOrdering.ArrangeForBackgroundMode(mapResult.Elements, classification.Mode).ToArray();

        Assert.Equal(FmlBackgroundMode.SolidColourBackground, classification.Mode);
        Assert.Null(classification.MainBackgroundImagePath);
        Assert.Equal(PanelElementKind.Background, ordered[0].Kind);
        Assert.Null(ordered[0].AssetPath);
        var bitmapImage = Assert.Single(mapResult.Elements.Where(element => element.Kind == PanelElementKind.Image));
        Assert.Equal("background/overlay.bmp", bitmapImage.AssetPath);
        Assert.DoesNotContain(mapResult.Elements.Where(element => element.AssetPath == "background/overlay.bmp"), element => element.Kind == PanelElementKind.Background);
        Assert.True(Array.FindIndex(ordered, element => element.AssetPath == "background/overlay.bmp") > Array.FindIndex(ordered, element => element.Kind == PanelElementKind.Reel));
    }

    [Fact]
    public void SolidColourBackground_WithLaterBitmap_KeepsImageOutOfBackgroundLayer()
    {
        var background = new Background { X = 0, Y = 0, Width = 300, Height = 200 };
        background.Colours["Colour"] = "#F0F0F0FF";
        var lamp = new Lamp { X = 20, Y = 20, Width = 30, Height = 30, SublampTable = [new LampSublampTableEntry(1, 1)] };
        var bitmap = new Bitmap { X = 50, Y = 50, Width = 40, Height = 40 };
        var layout = new Layout([background, lamp, bitmap]);
        var images = new Dictionary<FmlDecodedImageKey, string>
        {
            [new FmlDecodedImageKey(2, "Bitmap")] = "background/overlay.bmp"
        };

        var classification = FmlBackgroundClassifier.Classify(layout, images);
        var mapResult = new FmlToOasisMapper().Map(layout, images);
        var ordered = FmlPanelElementOrdering.ArrangeForBackgroundMode(mapResult.Elements, classification.Mode).ToArray();

        var backgroundIndex = Array.FindIndex(ordered, element => element.Kind == PanelElementKind.Background);
        var lampIndex = Array.FindIndex(ordered, element => element.Kind == PanelElementKind.Lamp);
        var imageIndex = Array.FindIndex(ordered, element => element.Kind == PanelElementKind.Image);
        Assert.Equal(0, backgroundIndex);
        Assert.True(lampIndex > backgroundIndex);
        Assert.True(imageIndex > lampIndex);
        Assert.Equal(2, ordered[imageIndex].SourceComponentIndex);
    }

    [Fact]
    public void NoBackground_PreservesSourceOrderWithoutInventingBackground()
    {
        var reel = new Reel { X = 10, Y = 10, Width = 50, Height = 100 };
        reel.UInt32s["Stops"] = 20;
        var lamp = new Lamp { X = 20, Y = 140, Width = 30, Height = 30, SublampTable = [new LampSublampTableEntry(1, 1)] };
        var layout = new Layout([reel, lamp]);

        var classification = FmlBackgroundClassifier.Classify(layout, new Dictionary<FmlDecodedImageKey, string>());
        var mapResult = new FmlToOasisMapper().Map(layout, new Dictionary<FmlDecodedImageKey, string>());
        var ordered = FmlPanelElementOrdering.ArrangeForBackgroundMode(mapResult.Elements, classification.Mode).ToArray();

        Assert.Equal(FmlBackgroundMode.NoBackground, classification.Mode);
        Assert.DoesNotContain(ordered, element => element.Kind == PanelElementKind.Background);
        Assert.Equal(PanelElementKind.Reel, ordered[0].Kind);
        Assert.Equal(PanelElementKind.Lamp, ordered[1].Kind);
    }
}
