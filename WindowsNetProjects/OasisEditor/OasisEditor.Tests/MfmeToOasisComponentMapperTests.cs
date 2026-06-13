using OasisEditor.Features.MfmeImport;
using Xunit;

namespace OasisEditor.Tests;

public sealed class MfmeToOasisComponentMapperTests
{
    [Fact]
    public void Map_WithSupportedComponents_ConvertsToNativeOasisElements()
    {
        var extract = new MfmeLegacyExtractData
        {
            ExtractRootPath = "C:/extract",
            ManifestPath = "C:/extract/layout.json",
            LayoutName = "layout",
            Components =
            [
                new MfmeLegacyBackgroundComponent(
                    new MfmeLegacyPoint(10, 20),
                    new MfmeLegacyPoint(800, 600),
                    "background.png",
                    new MfmeLegacyColor(1f, 0.5f, 0f, 1f)),
                new MfmeLegacyLampComponent(
                    new MfmeLegacyPoint(100, 200),
                    new MfmeLegacyPoint(30, 40),
                    "HOLD",
                    "Arial",
                    "Regular",
                    "12",
                    new MfmeLegacyLampElement("8", 8, new MfmeLegacyColor(0f, 1f, 0f, 1f), "lamp.png", "lamp-mask.png", Graphic: true),
                    new MfmeLegacyColor(0f, 0f, 0f, 1f),
                    new MfmeLegacyColor(1f, 1f, 1f, 1f),
                    NoOutline: false, HasButtonInput: false, HasCoinInput: false, ButtonNumberAsString: null, Inverted: false, Shortcut1: null, Shortcut2: null),
                new MfmeLegacyReelComponent(
                    new MfmeLegacyPoint(10, 30),
                    new MfmeLegacyPoint(90, 120),
                    Number: 2,
                    Stops: 24,
                    Reversed: true,
                    Height: 100,
                    BandBmpImageFilename: "band.png",
                    HasOverlay: true,
                    OverlayBmpImageFilename: "overlay.png"),
                new MfmeLegacySevenSegmentComponent(
                    new MfmeLegacyPoint(5, 6),
                    new MfmeLegacyPoint(70, 20),
                    Number: 11,
                    SegmentOnColor: new MfmeLegacyColor(1f, 0f, 0f, 1f)),
                new MfmeLegacyAlphaComponent(
                    "ExtractComponentMatrixAlpha",
                    new MfmeLegacyPoint(1, 2),
                    new MfmeLegacyPoint(3, 4),
                    Reversed: false,
                    SegmentOnColor: new MfmeLegacyColor(0f, 0f, 1f, 1f),
                    HasOverlay: true,
                    OverlayBmpImageFilename: "alpha-overlay.bmp"),
                new MfmeLegacyButtonComponent(
                    new MfmeLegacyPoint(120, 220),
                    new MfmeLegacyPoint(44, 22),
                    "Start",
                    "Arial",
                    "Regular",
                    "10",
                    HasButtonInput: true,
                    HasCoinInput: false,
                    ButtonNumberAsString: "6",
                    Inverted: true,
                    Shortcut1: "SPACE",
                    Shortcut2: null,
                    FirstLampElement: new MfmeLegacyLampElement("6", 6, new MfmeLegacyColor(1f,0f,0f,1f), "btn.png", null, Graphic: true),
                    OffImageColor: new MfmeLegacyColor(0f,0f,0f,1f),
                    TextColor: new MfmeLegacyColor(1f,1f,1f,1f),
                    NoOutline: false),
                new MfmeLegacyLabelComponent(
                    new MfmeLegacyPoint(50, 60),
                    new MfmeLegacyPoint(120, 24),
                    "COLLECT",
                    "Tahoma",
                    "Bold",
                    "10",
                    new MfmeLegacyColor(1f, 1f, 0f, 1f))
            ]
        };

        var mapper = new MfmeToOasisComponentMapper();

        var result = mapper.Map(extract);

        Assert.Empty(result.Warnings);
        Assert.Empty(result.SkippedLegacyComponentTypes);
        Assert.Equal(7, result.Elements.Count);
        Assert.Single(result.InputDefinitions);

        var background = result.Elements[0];
        Assert.Equal(PanelElementKind.Background, background.Kind);
        Assert.Equal("Background", background.Name);
        Assert.Equal(0, background.X);
        Assert.Equal(0, background.Y);
        Assert.Equal(800, background.Width);
        Assert.Equal(600, background.Height);
        Assert.Equal("background/background.png", background.AssetPath);
        Assert.Equal("#FFFF8000", background.OnColorHex);

        var lamp = result.Elements[1];
        Assert.Equal(PanelElementKind.Lamp, lamp.Kind);
        Assert.Equal("Lamp 8", lamp.Name);
        Assert.Equal(8, lamp.DisplayNumber);
        Assert.Equal("lamps/lamp.png", lamp.AssetPath);
        Assert.Equal("lamps/lamp-mask.png", lamp.SecondaryAssetPath);
        Assert.Equal("#FF00FF00", lamp.OnColorHex);
        Assert.Equal("#FF000000", lamp.OffColorHex);
        Assert.Equal("#FFFFFFFF", lamp.TextColorHex);
        Assert.Equal("HOLD", lamp.DisplayText);
        Assert.Equal("Arial", lamp.TextBoxFontName);
        Assert.Equal("Regular", lamp.TextBoxFontStyle);
        Assert.Equal("12", lamp.TextBoxFontSize);

        var reel = result.Elements[2];
        Assert.Equal(PanelElementKind.Reel, reel.Kind);
        Assert.Equal("Reel 3", reel.Name);
        Assert.Equal(3, reel.DisplayNumber);
        Assert.Equal(24, reel.Stops);
        Assert.True(reel.IsReversed);
        Assert.Equal(100d / 50d / 24d, reel.VisibleScale);
        Assert.Equal("reels/band.png", reel.AssetPath);
        Assert.Equal("reels/overlay.png", reel.SecondaryAssetPath);

        var sevenSegment = result.Elements[3];
        Assert.Equal(PanelElementKind.SevenSegment, sevenSegment.Kind);
        Assert.Equal("7 Segment 11", sevenSegment.Name);
        Assert.Equal(11, sevenSegment.DisplayNumber);
        Assert.Equal("#FFFF0000", sevenSegment.OnColorHex);

        var alpha = result.Elements[4];
        Assert.Equal(PanelElementKind.Alpha, alpha.Kind);
        Assert.Equal("Alpha", alpha.Name);
        Assert.False(alpha.IsReversed);
        Assert.Equal("reels/alpha-overlay.bmp", alpha.SecondaryAssetPath);
        Assert.Equal("#FF0000FF", alpha.OnColorHex);

        var label = result.Elements[6];

        var inputDefinition = result.InputDefinitions[0];
        Assert.Equal(InputDefinitionKind.Button, inputDefinition.Kind);
        Assert.Equal("6", inputDefinition.ButtonNumber);
        Assert.Equal("SPACE", inputDefinition.RawMfmeShortcut);
        Assert.Equal("Space", inputDefinition.KeyboardShortcut);


        Assert.Equal(PanelElementKind.Label, label.Kind);
        Assert.Equal("Label", label.Name);
        Assert.Equal("COLLECT", label.DisplayText);
        Assert.Equal("Tahoma", label.TextBoxFontName);
        Assert.Equal("Bold", label.TextBoxFontStyle);
        Assert.Equal("10", label.TextBoxFontSize);
        Assert.Equal("#FFFFFF00", label.TextColorHex);

        Assert.All(result.Elements, element => Assert.NotEqual(PanelElementKind.Unknown, element.Kind));
        Assert.All(result.Elements, element => Assert.False(string.IsNullOrWhiteSpace(element.ObjectId)));
        Assert.All(result.Elements, element =>
        {
            Assert.NotNull(element.ImportSource);
            Assert.Equal("LegacyImport", element.ImportSource!.Format);
            Assert.False(string.IsNullOrWhiteSpace(element.ImportSource.Reference));
        });
    }


    [Fact]
    public void Map_LampWithGraphicFalse_DoesNotMapLampImages()
    {
        var extract = new MfmeLegacyExtractData
        {
            ExtractRootPath = "C:/extract",
            ManifestPath = "C:/extract/layout.json",
            LayoutName = "layout",
            Components =
            [
                new MfmeLegacyLampComponent(
                    new MfmeLegacyPoint(100, 200),
                    new MfmeLegacyPoint(30, 40),
                    null,
                    null,
                    null,
                    null,
                    new MfmeLegacyLampElement("8", 8, new MfmeLegacyColor(0f, 1f, 0f, 1f), "lamp.png", "lamp-mask.png", Graphic: false),
                    new MfmeLegacyColor(0f, 0f, 0f, 1f),
                    null,
                    NoOutline: false, HasButtonInput: false, HasCoinInput: false, ButtonNumberAsString: null, Inverted: false, Shortcut1: null, Shortcut2: null)
            ]
        };

        var mapper = new MfmeToOasisComponentMapper();

        var result = mapper.Map(extract);

        var lamp = Assert.Single(result.Elements);
        Assert.Equal(PanelElementKind.Lamp, lamp.Kind);
        Assert.Null(lamp.AssetPath);
        Assert.Null(lamp.SecondaryAssetPath);
        Assert.Equal("Tahoma", lamp.TextBoxFontName);
        Assert.Equal("Regular", lamp.TextBoxFontStyle);
        Assert.Equal("8", lamp.TextBoxFontSize);
        Assert.Equal("#FF00FF00", lamp.OnColorHex);
        Assert.Equal("#FF000000", lamp.OffColorHex);
    }

    [Fact]
    public void Map_LampWithButtonInput_CreatesLinkedInputDefinition()
    {
        var extract = new MfmeLegacyExtractData
        {
            ExtractRootPath = "C:/extract",
            ManifestPath = "C:/extract/layout.json",
            LayoutName = "layout",
            Components =
            [
                new MfmeLegacyLampComponent(
                    new MfmeLegacyPoint(100, 200),
                    new MfmeLegacyPoint(30, 40),
                    "Start",
                    null,
                    null,
                    null,
                    new MfmeLegacyLampElement("6", 6, new MfmeLegacyColor(1f, 0f, 0f, 1f), "start.png", null, Graphic: true),
                    new MfmeLegacyColor(0f, 0f, 0f, 1f),
                    null,
                    NoOutline: false,
                    HasButtonInput: true,
                    HasCoinInput: false,
                    ButtonNumberAsString: "6",
                    Inverted: true,
                    Shortcut1: "SPACE",
                    Shortcut2: "S")
            ]
        };

        var mapper = new MfmeToOasisComponentMapper();

        var result = mapper.Map(extract);

        var lamp = Assert.Single(result.Elements);
        var inputDefinition = Assert.Single(result.InputDefinitions);
        Assert.Equal(InputDefinitionKind.Button, inputDefinition.Kind);
        Assert.Equal("6", inputDefinition.ButtonNumber);
        Assert.True(inputDefinition.Inverted);
        Assert.Equal("SPACE", inputDefinition.RawMfmeShortcut);
        Assert.Equal("Space", inputDefinition.KeyboardShortcut);
        Assert.Equal(Guid.Parse(lamp.ObjectId), inputDefinition.LinkedVisualElementId);
        Assert.Equal("Shortcut2: S", inputDefinition.Notes);
    }

    [Fact]
    public void Map_WithInvalidNumbersAndUnsupportedComponent_AddsWarnings()
    {
        var extract = new MfmeLegacyExtractData
        {
            ExtractRootPath = "C:/extract",
            ManifestPath = "C:/extract/layout.json",
            LayoutName = "layout",
            Components =
            [
                new MfmeLegacyLampComponent(
                    new MfmeLegacyPoint(1, 2),
                    new MfmeLegacyPoint(3, 4),
                    null,
                    null,
                    null,
                    null,
                    new MfmeLegacyLampElement("NaN", null, null, null, null, Graphic: false),
                    null,
                    null,
                    NoOutline: true, HasButtonInput: false, HasCoinInput: false, ButtonNumberAsString: null, Inverted: false, Shortcut1: null, Shortcut2: null),
                new MfmeLegacyReelComponent(
                    new MfmeLegacyPoint(1, 2),
                    new MfmeLegacyPoint(30, 40),
                    Number: 1,
                    Stops: 0,
                    Reversed: false,
                    Height: 100,
                    BandBmpImageFilename: null,
                    HasOverlay: false,
                    OverlayBmpImageFilename: null),
                new UnsupportedLegacyComponent()
            ]
        };

        var mapper = new MfmeToOasisComponentMapper();

        var result = mapper.Map(extract);

        Assert.Equal(2, result.Elements.Count);
        Assert.Single(result.SkippedLegacyComponentTypes);
        Assert.Contains("ExtractComponentButton", result.SkippedLegacyComponentTypes);

        Assert.Contains(result.Warnings, w => w.Code == "mfme.import.lamp.number.invalid");
        Assert.Contains(result.Warnings, w => w.Code == "mfme.import.reel.stops.invalid");
        Assert.Contains(result.Warnings, w => w.Code == "mfme.import.component.unsupported");

        var lamp = result.Elements[0];
        Assert.Equal("Lamp", lamp.Name);
        Assert.Null(lamp.DisplayNumber);

        var reel = result.Elements[1];
        Assert.Null(reel.VisibleScale);
        Assert.Equal("Reel 2", reel.Name);

        Assert.All(result.Elements, element =>
        {
            Assert.NotNull(element.ImportSource);
            Assert.Equal("LegacyImport", element.ImportSource!.Format);
            Assert.False(string.IsNullOrWhiteSpace(element.ImportSource.Reference));
        });
    }


    [Fact]
    public void Map_WithMultipleValidLampElements_CreatesIndependentSharedBoundsLamps()
    {
        var lampElements = new[]
        {
            new MfmeLegacyLampElement("147", 147, new MfmeLegacyColor(1f, 0f, 0f, 1f), "jackpot-147.bmp", "jackpot-147-mask.bmp", Graphic: true, SourceElementIndex: 0),
            new MfmeLegacyLampElement("", null, null, null, null, Graphic: false, SourceElementIndex: 1),
            new MfmeLegacyLampElement("164", 164, new MfmeLegacyColor(0f, 1f, 0f, 1f), "jackpot-164.bmp", "jackpot-164-mask.bmp", Graphic: true, SourceElementIndex: 2)
        };
        var extract = new MfmeLegacyExtractData
        {
            ExtractRootPath = "C:/extract",
            ManifestPath = "C:/extract/layout.json",
            LayoutName = "layout",
            Components =
            [
                new MfmeLegacyLampComponent(
                    new MfmeLegacyPoint(100, 200),
                    new MfmeLegacyPoint(300, 80),
                    "JACKPOT",
                    "Arial",
                    "Regular",
                    "12",
                    lampElements[0],
                    new MfmeLegacyColor(0f, 0f, 0f, 1f),
                    new MfmeLegacyColor(1f, 1f, 1f, 1f),
                    NoOutline: false,
                    HasButtonInput: false,
                    HasCoinInput: false,
                    ButtonNumberAsString: null,
                    Inverted: false,
                    Shortcut1: null,
                    Shortcut2: null,
                    LampElements: lampElements.Where(element => element.HasUsefulIdentityOrImage).ToArray(),
                    SourceComponentIndex: 12)
            ]
        };

        var result = new MfmeToOasisComponentMapper().Map(extract);

        Assert.Empty(result.Warnings);
        Assert.Equal(2, result.Elements.Count);
        Assert.All(result.Elements, element =>
        {
            Assert.Equal(PanelElementKind.Lamp, element.Kind);
            Assert.Equal(100, element.X);
            Assert.Equal(200, element.Y);
            Assert.Equal(300, element.Width);
            Assert.Equal(80, element.Height);
            Assert.Equal(12, element.ImportSource!.SourceComponentIndex);
            Assert.Equal("mfme-component-12", element.ImportSource.SharedLampSetId);
            Assert.Equal(2, element.ImportSource.SharedLampSetCount);
        });
        Assert.Equal(147, result.Elements[0].DisplayNumber);
        Assert.Equal("lamps/jackpot-147.bmp", result.Elements[0].AssetPath);
        Assert.Equal("Lamp:147", result.Elements[0].ImportSource!.Reference);
        Assert.Equal(0, result.Elements[0].ImportSource.LampElementIndex);
        Assert.Equal(164, result.Elements[1].DisplayNumber);
        Assert.Equal("lamps/jackpot-164.bmp", result.Elements[1].AssetPath);
        Assert.Equal("Lamp:164", result.Elements[1].ImportSource!.Reference);
        Assert.Equal(2, result.Elements[1].ImportSource.LampElementIndex);
    }

    private sealed record UnsupportedLegacyComponent()
        : MfmeLegacyComponentBase(
            "ExtractComponentButton",
            new MfmeLegacyPoint(0, 0),
            new MfmeLegacyPoint(1, 1),
            null,
            null,
            null,
            null);
}
