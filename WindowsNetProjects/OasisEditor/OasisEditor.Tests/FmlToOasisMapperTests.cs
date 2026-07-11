using MfmeFmlDecoder.src.Model;
using MfmeFmlDecoder.src.Model.Component;
using OasisEditor.Features.FmlImport;
using System.Windows.Media;
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
            SublampTable = [new LampSublampTableEntry(1, 42)]
        };
        lamp.Strings["OffText"] = "HOLD";
        lamp.Colours["Sublamp1Colour"] = "#0000FFFF";
        lamp.Colours["OffImageColour"] = "#204060FF";
        lamp.Fonts["Primary"] = new FontTagEntry(0, "Primary", "Arial", 12, 0, "Western", "#0080C0FF", 0);

        var result = new FmlToOasisMapper().Map(new Layout([lamp]), new Dictionary<FmlDecodedImageKey, string>());

        var element = Assert.Single(result.Elements);
        Assert.Equal(PanelElementKind.Lamp, element.Kind);
        Assert.Equal(42, element.DisplayNumber);
        Assert.Equal("HOLD", element.DisplayText);
        Assert.Equal("#FF0000FF", element.OnColorHex);
        Assert.Equal("#FF204060", element.OffColorHex);
        Assert.Equal("#FF0080C0", element.TextColorHex);
        AssertOasisArgbChannels(element.OnColorHex, 255, 0, 0, 255);
        AssertOasisArgbChannels(element.OffColorHex, 255, 32, 64, 96);
        AssertOasisArgbChannels(element.TextColorHex, 255, 0, 128, 192);
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
                new LampSublampTableEntry(1, 10),
                new LampSublampTableEntry(2, 11),
                new LampSublampTableEntry(3, -2)
            ]
        };
        lamp.Colours["Sublamp1Colour"] = "#102030FF";
        lamp.Colours["Sublamp2Colour"] = "#40506080";

        var result = new FmlToOasisMapper().Map(new Layout([lamp]), new Dictionary<FmlDecodedImageKey, string>());

        Assert.Equal(2, result.Elements.Count);
        Assert.Equal(10, result.Elements[0].DisplayNumber);
        Assert.Equal("#FF102030", result.Elements[0].OnColorHex);
        Assert.Equal(11, result.Elements[1].DisplayNumber);
        Assert.Equal("#80405060", result.Elements[1].OnColorHex);
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
        sevenSeg.Colours["OnColour"] = "#AA0001FF";
        var alpha = new Alpha { X = 5, Y = 6, Width = 70, Height = 20 };
        alpha.Colours["OnColour"] = "#00AA02FF";
        var label = new Label { X = 7, Y = 8, Width = 90, Height = 24 };
        label.Strings["Caption"] = "HELLO";
        label.Fonts["Primary"] = new FontTagEntry(0, "Primary", "Tahoma", 9, 0, "Western", "#010203FF", 0);

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
    public void Map_WithDecodedMfmeLabel_UsesLabelKeyFontTextColourAndLampNumber()
    {
        var label = new Label { X = 7, Y = 8, Width = 90, Height = 24 };
        label.Strings["Label"] = "COLLECT";
        label.UInt32s["Lamp"] = 0;
        label.Fonts["Primary"] = new FontTagEntry(0, "Primary", "Tahoma", 9, 0, "Western", "#010203FF", 1);

        var result = new FmlToOasisMapper().Map(new Layout([label]), new Dictionary<FmlDecodedImageKey, string>());

        var element = Assert.Single(result.Elements);
        Assert.Equal(PanelElementKind.Label, element.Kind);
        Assert.Equal("COLLECT", element.DisplayText);
        Assert.Equal(0, element.LampNumber);
        Assert.Equal("Tahoma", element.TextBoxFontName);
        Assert.Equal("Bold", element.TextBoxFontStyle);
        Assert.Equal("9", element.TextBoxFontSize);
        Assert.Equal("#FF010203", element.TextColorHex);
    }

    [Fact]
    public void Map_WithStaticMfmeLabel_LeavesLampNumberNullAndKeepsCompatibilityAliases()
    {
        var label = new Label { X = 7, Y = 8, Width = 90, Height = 24 };
        label.Strings["Label (UTF-16)"] = "NUDGE";

        var result = new FmlToOasisMapper().Map(new Layout([label]), new Dictionary<FmlDecodedImageKey, string>());

        var element = Assert.Single(result.Elements);
        Assert.Equal(PanelElementKind.Label, element.Kind);
        Assert.Equal("NUDGE", element.DisplayText);
        Assert.Null(element.LampNumber);
    }
    [Fact]
    public void Map_WithGraphicalLampAndButton_PreservesAssetsAndInputDefinition()
    {
        var lamp = new Lamp { X = 1, Y = 2, Width = 30, Height = 40, SublampTable = [new LampSublampTableEntry(1, 9)] };
        lamp.Colours["Sublamp1Colour"] = "#112233FF";
        var button = new Button { X = 5, Y = 6, Width = 20, Height = 20, SublampTable = [new LampSublampTableEntry(1, 12)] };
        button.Strings["ButtonNumber"] = "1";
        button.Strings["Label"] = "START";

        var images = new Dictionary<FmlDecodedImageKey, string>
        {
            [new FmlDecodedImageKey(0, "Sublamp 1 Main")] = "lamps/lamp.bmp",
            [new FmlDecodedImageKey(0, "Sublamp 1 Mask")] = "lamps/lamp-mask.bmp"
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
    public void Map_WithButtonLabel_MapsToLampDisplayTextAndInputDefinition()
    {
        var button = new Button { X = 5, Y = 6, Width = 20, Height = 20, SublampTable = [new LampSublampTableEntry(1, 12)] };
        button.Strings["ButtonNumber"] = "1";
        button.Strings["Label"] = "START";

        var result = new FmlToOasisMapper().Map(new Layout([button]), new Dictionary<FmlDecodedImageKey, string>());

        var element = Assert.Single(result.Elements);
        Assert.Equal(PanelElementKind.Lamp, element.Kind);
        Assert.Equal(12, element.DisplayNumber);
        Assert.Equal("START", element.DisplayText);
        var input = Assert.Single(result.InputDefinitions);
        Assert.Equal("1", input.ButtonNumber);
        Assert.Equal(element.ObjectId, input.LinkedVisualElementId?.ToString("N"));
    }

    [Fact]
    public void Map_WithDecodedButtonUtf16Label_MapsToLampDisplayText()
    {
        var button = new Button { X = 5, Y = 6, Width = 20, Height = 20, SublampTable = [new LampSublampTableEntry(1, 12)] };
        button.Strings["ButtonNumber"] = "1";
        button.Strings["Label (UTF-16)"] = "START";

        var result = new FmlToOasisMapper().Map(new Layout([button]), new Dictionary<FmlDecodedImageKey, string>());

        var element = Assert.Single(result.Elements);
        Assert.Equal(PanelElementKind.Lamp, element.Kind);
        Assert.Equal("START", element.DisplayText);
        Assert.Single(result.InputDefinitions);
    }

    [Fact]
    public void Map_WithLampSpecificTextAndLabel_PrefersLampSpecificText()
    {
        var lamp = new Lamp
        {
            X = 1,
            Y = 2,
            Width = 30,
            Height = 40,
            SublampTable = [new LampSublampTableEntry(1, 9)]
        };
        lamp.Strings["OffText"] = "HOLD";
        lamp.Strings["Label"] = "START";
        lamp.Strings["Label (UTF-16)"] = "COLLECT";

        var result = new FmlToOasisMapper().Map(new Layout([lamp]), new Dictionary<FmlDecodedImageKey, string>());

        var element = Assert.Single(result.Elements);
        Assert.Equal(PanelElementKind.Lamp, element.Kind);
        Assert.Equal("HOLD", element.DisplayText);
    }


    [Theory]
    [InlineData("OffImageColour", "#204060FF", "#FF204060", 255, 32, 64, 96)]
    [InlineData("OffImageColor", "#20406080", "#80204060", 128, 32, 64, 96)]
    public void Map_WithLampOffImageColorAliases_MapsOffColor(string key, string decoderValue, string expected, byte a, byte r, byte g, byte b)
    {
        var lamp = new Lamp
        {
            X = 1,
            Y = 2,
            Width = 30,
            Height = 40,
            SublampTable = [new LampSublampTableEntry(1, 9)]
        };
        lamp.Colours["Sublamp1Colour"] = "#0000FFFF";
        lamp.Colours[key] = decoderValue;

        var result = new FmlToOasisMapper().Map(new Layout([lamp]), new Dictionary<FmlDecodedImageKey, string>());

        var element = Assert.Single(result.Elements);
        Assert.Equal(expected, element.OffColorHex);
        AssertOasisArgbChannels(element.OffColorHex, a, r, g, b);
    }

    [Fact]
    public void Map_WithLampOffImageColour_PrefersDecoderKeyOverLegacyAliases()
    {
        var lamp = new Lamp
        {
            X = 1,
            Y = 2,
            Width = 30,
            Height = 40,
            SublampTable = [new LampSublampTableEntry(1, 9)]
        };
        lamp.Colours["Sublamp1Colour"] = "#0000FFFF";
        lamp.Colours["OffImageColour"] = "#204060FF";
        lamp.Colours["OffColour"] = "#010203FF";

        var result = new FmlToOasisMapper().Map(new Layout([lamp]), new Dictionary<FmlDecodedImageKey, string>());

        var element = Assert.Single(result.Elements);
        Assert.Equal("#FF204060", element.OffColorHex);
    }

    [Fact]
    public void Map_WithMalformedLampOffImageColour_LeavesOffColorUnset()
    {
        var lamp = new Lamp
        {
            X = 1,
            Y = 2,
            Width = 30,
            Height = 40,
            SublampTable = [new LampSublampTableEntry(1, 9)]
        };
        lamp.Colours["Sublamp1Colour"] = "#0000FFFF";
        lamp.Colours["OffImageColour"] = "#20406GFF";
        lamp.Colours["OffColour"] = "#010203FF";

        var result = new FmlToOasisMapper().Map(new Layout([lamp]), new Dictionary<FmlDecodedImageKey, string>());

        var element = Assert.Single(result.Elements);
        Assert.Null(element.OffColorHex);
    }

    [Fact]
    public void Map_WithUnsupportedComponent_AddsWarningAndSkipsComponent()
    {
        var unsupported = new Border { X = 1, Y = 2, Width = 3, Height = 4 };

        var result = new FmlToOasisMapper().Map(new Layout([unsupported]), new Dictionary<FmlDecodedImageKey, string>());

        Assert.Empty(result.Elements);
        Assert.Contains(result.Warnings, warning => warning.Code == "fml.import.component.unsupported" && warning.Context == nameof(Border));
    }

    [Theory]
    [InlineData("#0080C0FF", "#FF0080C0", 255, 0, 128, 192)]
    [InlineData("#12345678", "#78123456", 0x78, 0x12, 0x34, 0x56)]
    [InlineData("#123456", "#FF123456", 255, 0x12, 0x34, 0x56)]
    public void ConvertDecoderRgbaToOasisArgb_ReordersChannels(string decoderValue, string expected, byte a, byte r, byte g, byte b)
    {
        var converted = FmlToOasisMapper.ConvertDecoderRgbaToOasisArgb(decoderValue);

        Assert.Equal(expected, converted);
        AssertOasisArgbChannels(converted, a, r, g, b);
    }

    [Theory]
    [InlineData("#12345")]
    [InlineData("#1234567890")]
    [InlineData("#12345G")]
    [InlineData("123456")]
    public void ConvertDecoderRgbaToOasisArgb_RejectsMalformedValues(string decoderValue)
    {
        Assert.Null(FmlToOasisMapper.ConvertDecoderRgbaToOasisArgb(decoderValue));
    }

    private static void AssertOasisArgbChannels(string? value, byte a, byte r, byte g, byte b)
    {
        Assert.True(InspectorColorHex.TryParse(value, out Color color));
        Assert.Equal(a, color.A);
        Assert.Equal(r, color.R);
        Assert.Equal(g, color.G);
        Assert.Equal(b, color.B);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Map_Button_MapsBorderFromNoOutlineWithButtonDefault(bool? noOutline, bool expectedHasBorder)
    {
        var button = new Button { SublampTable = [new LampSublampTableEntry(1, 5)] };
        if (noOutline.HasValue) button.Booleans["NoOutline"] = noOutline.Value;

        var result = new FmlToOasisMapper().Map(new Layout([button]), new Dictionary<FmlDecodedImageKey, string>());

        Assert.Equal(expectedHasBorder, Assert.Single(result.Elements).HasBorder);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Map_Lamp_MapsBorderFromNoOutlineWithDefaultDisabled(bool? noOutline, bool expectedHasBorder)
    {
        var lamp = new Lamp { SublampTable = [new LampSublampTableEntry(1, 5)] };
        if (noOutline.HasValue) lamp.Booleans["NoOutline"] = noOutline.Value;

        var result = new FmlToOasisMapper().Map(new Layout([lamp]), new Dictionary<FmlDecodedImageKey, string>());

        Assert.Equal(expectedHasBorder, Assert.Single(result.Elements).HasBorder);
    }

    [Fact]
    public void Map_MultiSublamp_AppliesBorderToEveryGeneratedLamp()
    {
        var lamp = new Lamp { SublampTable = [new LampSublampTableEntry(1, 5), new LampSublampTableEntry(2, 6)] };
        lamp.Booleans["NoOutline"] = false;

        var result = new FmlToOasisMapper().Map(new Layout([lamp]), new Dictionary<FmlDecodedImageKey, string>());

        Assert.Equal(2, result.Elements.Count);
        Assert.All(result.Elements, element => Assert.True(element.HasBorder));
    }

}
