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
}
