using MfmeFmlDecoder.src.Model;
using MfmeFmlDecoder.src.Model.Component;
using OasisEditor.Features.FmlImport;
using Xunit;

namespace OasisEditor.Tests;

public sealed class FmlToOasisMapperTests
{
    [Fact]
    public void Map_WithTextOnlyLamp_PreservesNumberTextColourAndNoAssets()
    {
        var lamp = new Lamp
        {
            X = 10,
            Y = 20,
            Width = 30,
            Height = 40,
            SublampTable = [new LampSublampTableEntry(0, 42)]
        };
        lamp.Strings["OffText"] = "HOLD";
        lamp.Colours["Sublamp0Colour"] = "#11223344";
        lamp.Fonts["Primary"] = new FontTagEntry(0, "Primary", "Arial", 12, 0, "Western", "#FF010203", 0);

        var result = new FmlToOasisMapper().Map(new Layout([lamp]), new Dictionary<FmlDecodedImageKey, string>());

        var element = Assert.Single(result.Elements);
        Assert.Equal(PanelElementKind.Lamp, element.Kind);
        Assert.Equal(42, element.DisplayNumber);
        Assert.Equal("HOLD", element.DisplayText);
        Assert.Equal("#11223344", element.OnColorHex);
        Assert.Null(element.AssetPath);
        Assert.Null(element.SecondaryAssetPath);
        Assert.Equal("Arial", element.TextBoxFontName);
        Assert.Equal("12", element.TextBoxFontSize);
    }

    [Fact]
    public void Map_WithMultiSublampLamp_PreservesNumberColourPairingAndSharedSourceGroup()
    {
        var lamp = new Lamp
        {
            X = 1,
            Y = 2,
            Width = 3,
            Height = 4,
            SublampTable =
            [
                new LampSublampTableEntry(0, 10),
                new LampSublampTableEntry(1, 11),
                new LampSublampTableEntry(2, -2)
            ]
        };
        lamp.Colours["Sublamp0Colour"] = "#FF102030";
        lamp.Colours["Sublamp1Colour"] = "#FF405060";

        var result = new FmlToOasisMapper().Map(new Layout([lamp]), new Dictionary<FmlDecodedImageKey, string>());

        Assert.Equal(2, result.Elements.Count);
        Assert.Equal(10, result.Elements[0].DisplayNumber);
        Assert.Equal("#FF102030", result.Elements[0].OnColorHex);
        Assert.Equal(11, result.Elements[1].DisplayNumber);
        Assert.Equal("#FF405060", result.Elements[1].OnColorHex);
        Assert.Equal(result.Elements[0].SharedSourceSetId, result.Elements[1].SharedSourceSetId);
        Assert.All(result.Elements, element => Assert.Equal(2, element.SharedSourceSetCount));
    }

    [Fact]
    public void Map_WithCoreComponentTypes_PreservesDirectMappingBehavior()
    {
        var background = new Background { X = 1, Y = 2, Width = 300, Height = 200 };
        var reel = new BandReel { X = 10, Y = 20, Width = 30, Height = 100, View = 2 };
        reel.UInt32s["Stops"] = 20;
        reel.Booleans["Reverse"] = true;
        var sevenSeg = new SevenSeg { X = 3, Y = 4, Width = 50, Height = 12, Number = 7 };
        sevenSeg.Colours["OnColour"] = "#FFAA0001";
        var alpha = new Alpha { X = 5, Y = 6, Width = 70, Height = 20 };
        alpha.Colours["OnColour"] = "#FF00AA02";
        var label = new Label { X = 7, Y = 8, Width = 90, Height = 24 };
        label.Strings["Caption"] = "HELLO";
        label.Fonts["Primary"] = new FontTagEntry(0, "Primary", "Tahoma", 9, 0, "Western", "#FF010203", 0);

        var images = new Dictionary<FmlDecodedImageKey, string>
        {
            [new FmlDecodedImageKey(0, "Background")] = "background/bg.bmp",
            [new FmlDecodedImageKey(1, "Reel Band")] = "reels/band.bmp",
            [new FmlDecodedImageKey(1, "Window Overlay")] = "reels/overlay.bmp",
            [new FmlDecodedImageKey(2, "Window Overlay")] = "reels/seg-overlay.bmp",
            [new FmlDecodedImageKey(3, "Window Overlay")] = "reels/alpha-overlay.bmp"
        };

        var result = new FmlToOasisMapper().Map(new Layout([background, reel, sevenSeg, alpha, label]), images);

        Assert.Contains(result.Elements, e => e.Kind == PanelElementKind.Background && e.AssetPath == "background/bg.bmp");
        Assert.Contains(result.Elements, e => e.Kind == PanelElementKind.Reel && e.AssetPath == "reels/band.bmp" && e.SecondaryAssetPath == "reels/overlay.bmp" && e.IsReversed == true && e.VisibleScale == 0.1d);
        Assert.Contains(result.Elements, e => e.Kind == PanelElementKind.SevenSegment && e.DisplayNumber == 7 && e.OnColorHex == "#FFAA0001" && e.SecondaryAssetPath == "reels/seg-overlay.bmp");
        Assert.Contains(result.Elements, e => e.Kind == PanelElementKind.Alpha && e.OnColorHex == "#FF00AA02" && e.SecondaryAssetPath == "reels/alpha-overlay.bmp");
        Assert.Contains(result.Elements, e => e.Kind == PanelElementKind.Label && e.DisplayText == "HELLO" && e.TextBoxFontName == "Tahoma");
    }

    [Fact]
    public void Map_WithGraphicalLampAndButton_PreservesAssetsAndInputDefinition()
    {
        var lamp = new Lamp { X = 1, Y = 2, Width = 30, Height = 40, SublampTable = [new LampSublampTableEntry(0, 9)] };
        lamp.Colours["Sublamp0Colour"] = "#FF112233";
        var button = new Button { X = 5, Y = 6, Width = 20, Height = 20, SublampTable = [new LampSublampTableEntry(0, 12)] };
        button.Strings["ButtonNumber"] = "1";
        button.Strings["Text"] = "START";

        var images = new Dictionary<FmlDecodedImageKey, string>
        {
            [new FmlDecodedImageKey(0, "Sublamp 0 Main")] = "lamps/lamp.bmp",
            [new FmlDecodedImageKey(0, "Sublamp 0 Mask")] = "lamps/lamp-mask.bmp"
        };

        var result = new FmlToOasisMapper().Map(new Layout([lamp, button]), images);

        var graphicalLamp = Assert.Single(result.Elements.Where(e => e.DisplayNumber == 9));
        Assert.Equal("lamps/lamp.bmp", graphicalLamp.AssetPath);
        Assert.Equal("lamps/lamp-mask.bmp", graphicalLamp.SecondaryAssetPath);
        Assert.Equal("#FF112233", graphicalLamp.OnColorHex);
        Assert.Contains(result.Elements, e => e.Kind == PanelElementKind.Lamp && e.DisplayNumber == 12 && e.DisplayText == "START");
        Assert.Single(result.InputDefinitions);
    }

    [Fact]
    public void Map_WithUnsupportedComponent_AddsWarningAndSkipsComponent()
    {
        var unsupported = new Border { X = 1, Y = 2, Width = 3, Height = 4 };

        var result = new FmlToOasisMapper().Map(new Layout([unsupported]), new Dictionary<FmlDecodedImageKey, string>());

        Assert.Empty(result.Elements);
        Assert.Contains(result.Warnings, warning => warning.Code == "fml.import.component.unsupported" && warning.Context == nameof(Border));
    }

}
