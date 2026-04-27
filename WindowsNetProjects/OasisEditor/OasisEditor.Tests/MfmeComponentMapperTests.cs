using System;
using OasisEditor.Features.MfmeImport;
using Xunit;

namespace OasisEditor.Tests;

public sealed class MfmeComponentMapperTests
{
    [Fact]
    public void Map_Background_UsesOriginAndBackgroundName()
    {
        var mapper = new MfmeComponentMapper();
        var component = new MfmeBackgroundComponentData
        {
            Kind = MfmeComponentKind.Background,
            SourceType = "ExtractComponentBackground",
            Width = 800,
            Height = 600,
            ImageFileName = "bg.png",
            Color = "#AABBCC"
        };

        var result = mapper.Map([component]);

        var mapped = Assert.Single(result.Elements);
        Assert.Equal(PanelElementKind.Background, mapped.Kind);
        Assert.Equal("Background", mapped.Name);
        Assert.Equal(0, mapped.X);
        Assert.Equal(0, mapped.Y);
        Assert.Equal(800, mapped.Width);
        Assert.Equal(600, mapped.Height);
        Assert.Equal("Background/bg.png", mapped.AssetPath);
        Assert.Equal("#AABBCC", mapped.PrimaryColor);
    }

    [Fact]
    public void Map_Lamp_MapsFirstPassLampFields()
    {
        var mapper = new MfmeComponentMapper();
        var component = new MfmeLampComponentData
        {
            Kind = MfmeComponentKind.Lamp,
            SourceType = "ExtractComponentLamp",
            X = 12,
            Y = 34,
            Width = 56,
            Height = 78,
            Number = 4,
            ImageFileName = "lamp4.png",
            OnColor = "#00FF00",
            OffColor = "#000011",
            TextColor = "#FFFFFF",
            DisplayName = "HOLD"
        };

        var result = mapper.Map([component]);

        var mapped = Assert.Single(result.Elements);
        Assert.Equal(PanelElementKind.Lamp, mapped.Kind);
        Assert.Equal("Lamp 4", mapped.Name);
        Assert.Equal(4, mapped.DisplayNumber);
        Assert.Equal("Lamps/lamp4.png", mapped.AssetPath);
        Assert.Equal("#00FF00", mapped.PrimaryColor);
        Assert.Equal("#000011", mapped.SecondaryColor);
        Assert.Equal("#FFFFFF", mapped.TextColor);
        Assert.Equal("HOLD", mapped.Text);
    }

    [Fact]
    public void Map_Reel_AppliesUnityNumberOffsetAndMetadata()
    {
        var mapper = new MfmeComponentMapper();
        var component = new MfmeReelComponentData
        {
            Kind = MfmeComponentKind.Reel,
            SourceType = "ExtractComponentReel",
            X = 20,
            Y = 30,
            Width = 100,
            Height = 150,
            Number = 2,
            Stops = 24,
            Reversed = true,
            BandImageFileName = "band.png"
        };

        var result = mapper.Map([component]);

        var mapped = Assert.Single(result.Elements);
        Assert.Equal(PanelElementKind.Reel, mapped.Kind);
        Assert.Equal("Reel 3", mapped.Name);
        Assert.Equal(3, mapped.DisplayNumber);
        Assert.Equal("2", mapped.MfmeSourceId);
        Assert.Equal(24, mapped.Stops);
        Assert.True(mapped.Reversed);
        Assert.Equal("Reels/band.png", mapped.AssetPath);
    }

    [Fact]
    public void Map_SevenSegment_MapsNumberAndColor()
    {
        var mapper = new MfmeComponentMapper();
        var component = new MfmeSevenSegmentComponentData
        {
            Kind = MfmeComponentKind.SevenSegment,
            SourceType = "ExtractComponentSevenSegment",
            Number = 9,
            SegmentOnColor = "#123456"
        };

        var result = mapper.Map([component]);

        var mapped = Assert.Single(result.Elements);
        Assert.Equal(PanelElementKind.SevenSegment, mapped.Kind);
        Assert.Equal("7 Segment 9", mapped.Name);
        Assert.Equal(9, mapped.DisplayNumber);
        Assert.Equal("#123456", mapped.PrimaryColor);
    }

    [Fact]
    public void Map_AlphaVariants_MapToSingleAlphaKind()
    {
        var mapper = new MfmeComponentMapper();
        var alpha = new MfmeAlphaComponentData
        {
            Kind = MfmeComponentKind.Alpha,
            SourceType = "ExtractComponentMatrixAlpha",
            Number = 7,
            Reversed = true,
            Color = "#DD8800",
            AlphaVariant = "MatrixAlpha"
        };

        var result = mapper.Map([alpha]);

        var mapped = Assert.Single(result.Elements);
        Assert.Equal(PanelElementKind.Alpha, mapped.Kind);
        Assert.Equal("Alpha", mapped.Name);
        Assert.Equal(7, mapped.DisplayNumber);
        Assert.True(mapped.Reversed);
        Assert.Equal("#DD8800", mapped.PrimaryColor);
    }

    [Fact]
    public void Map_UnsupportedComponent_SkipsWithWarning()
    {
        var mapper = new MfmeComponentMapper();
        var unsupported = new MfmeUnknownComponentData
        {
            Kind = MfmeComponentKind.Unknown,
            SourceType = "UnsupportedThing"
        };

        var result = mapper.Map([unsupported]);

        Assert.Empty(result.Elements);
        Assert.Single(result.SkippedComponents);
        var warning = Assert.Single(result.Warnings);
        Assert.Equal("unsupported-component", warning.Code);
    }

    [Fact]
    public void Map_InvalidDimensions_FallsBackToDefaults()
    {
        var mapper = new MfmeComponentMapper();
        var component = new MfmeLampComponentData
        {
            Kind = MfmeComponentKind.Lamp,
            SourceType = "ExtractComponentLamp",
            Width = 0,
            Height = double.NaN
        };

        var result = mapper.Map([component]);

        var mapped = Assert.Single(result.Elements);
        Assert.Equal(100, mapped.Width);
        Assert.Equal(100, mapped.Height);
    }
}
