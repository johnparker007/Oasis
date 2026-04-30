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
                    NoOutline: false),
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
                    Reversed: false)
            ]
        };

        var mapper = new MfmeToOasisComponentMapper();

        var result = mapper.Map(extract);

        Assert.Empty(result.Warnings);
        Assert.Empty(result.SkippedLegacyComponentTypes);
        Assert.Equal(5, result.Elements.Count);

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
                    NoOutline: false)
            ]
        };

        var mapper = new MfmeToOasisComponentMapper();

        var result = mapper.Map(extract);

        var lamp = Assert.Single(result.Elements);
        Assert.Equal(PanelElementKind.Lamp, lamp.Kind);
        Assert.Null(lamp.AssetPath);
        Assert.Null(lamp.SecondaryAssetPath);
        Assert.Equal("#FF00FF00", lamp.OnColorHex);
        Assert.Equal("#FF000000", lamp.OffColorHex);
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
                    NoOutline: true),
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
