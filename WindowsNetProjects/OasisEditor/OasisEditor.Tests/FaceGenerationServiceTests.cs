using System.Windows;
using Xunit;

namespace OasisEditor.Tests;

public sealed class FaceGenerationServiceTests
{
    [Fact]
    public void GenerateFromPanelRegion_ConvertsContainedLampsWithRelativeCoordinatesAndReferences()
    {
        var panel = new Panel2DDocumentModel
        {
            Title = "Panel",
            Elements =
            [
                new PanelElementModel
                {
                    ObjectId = "background-1",
                    Name = "Glass",
                    Kind = PanelElementKind.Background,
                    X = 0,
                    Y = 0,
                    Width = 400,
                    Height = 300,
                    AssetPath = "Assets/Panel2D/glass.png",
                    IsVisible = true
                },
                new PanelElementModel
                {
                    ObjectId = "lamp-17",
                    Name = "Start Lamp",
                    Kind = PanelElementKind.Lamp,
                    X = 110,
                    Y = 220,
                    Width = 30,
                    Height = 40,
                    DisplayNumber = 17,
                    IsVisible = true
                },
                new PanelElementModel
                {
                    ObjectId = "reel-1",
                    Name = "Ignored Reel",
                    Kind = PanelElementKind.Reel,
                    X = 120,
                    Y = 225,
                    Width = 20,
                    Height = 20,
                    DisplayNumber = 1,
                    IsVisible = true
                },
                new PanelElementModel
                {
                    ObjectId = "lamp-outside",
                    Name = "Outside Lamp",
                    Kind = PanelElementKind.Lamp,
                    X = 10,
                    Y = 20,
                    Width = 30,
                    Height = 40,
                    DisplayNumber = 99,
                    IsVisible = true
                }
            ]
        };

        var service = new FaceGenerationService();
        var result = service.GenerateFromPanelRegion(
            panel,
            FaceSourceRegionModel.FromRect(new Rect(100, 200, 200, 150)),
            "Generated Face",
            "panel-doc-1");

        Assert.Equal(1, result.ConvertedLampCount);
        Assert.Equal(1, result.ArtworkElementCount);
        Assert.Equal("Generated Face", result.Document.Title);
        Assert.Equal("panel-doc-1", result.Document.SourcePanel2DDocumentId);
        Assert.NotNull(result.Document.SourceRegion);

        var artwork = Assert.IsType<FaceArtworkElement>(result.Document.Elements[0]);
        Assert.Equal("face-artwork-background-1", artwork.ObjectId);
        Assert.Equal("Assets/Panel2D/glass.png", artwork.AssetPath);
        Assert.Equal("panel-doc-1", artwork.SourcePanel2DDocumentId);
        Assert.Equal(100d, artwork.SourceRegion!.X);
        Assert.Equal(200d, artwork.SourceRegion.Y);
        Assert.Equal("background-1", artwork.Provenance!.SourcePanel2DElementId);
        Assert.Equal("background", artwork.Provenance.SourcePanel2DElementKind);

        var element = Assert.IsType<FaceLampWindowElement>(result.Document.Elements[1]);
        Assert.Equal("face-lamp-17", element.ObjectId);
        Assert.Equal("Start Lamp", element.Name);
        Assert.Equal(10d, element.X);
        Assert.Equal(20d, element.Y);
        Assert.Equal(30d, element.Width);
        Assert.Equal(40d, element.Height);
        Assert.Equal("lamp:17", element.LinkedMachineObjectReference?.ToString());
        Assert.Equal("lamp-17", element.LinkedPanel2DElementId);
    }

    [Fact]
    public void GenerateFromPanelRegion_RoundTripsGeneratedSourceAndElementLinks()
    {
        var panel = new Panel2DDocumentModel
        {
            Elements =
            [
                new PanelElementModel
                {
                    ObjectId = "background-1",
                    Name = "Glass",
                    Kind = PanelElementKind.Background,
                    X = 0,
                    Y = 0,
                    Width = 200,
                    Height = 200,
                    AssetPath = "Assets/Panel2D/glass.png",
                    IsVisible = true
                },
                new PanelElementModel
                {
                    ObjectId = "lamp-5",
                    Name = "Hold Lamp",
                    Kind = PanelElementKind.Lamp,
                    X = 25,
                    Y = 35,
                    Width = 15,
                    Height = 20,
                    DisplayNumber = 5,
                    IsVisible = true
                }
            ]
        };

        var generated = new FaceGenerationService().GenerateFromPanelRegion(
            panel,
            FaceSourceRegionModel.FromRect(new Rect(20, 30, 100, 100)),
            "Round Trip Face",
            "source-panel").Document;

        var json = FaceDocumentStorage.Serialize(generated);
        Assert.True(FaceDocumentStorage.TryReadValidated(json, out var file, out var error), error);
        var model = FaceDocumentStorage.ToModel(file);

        Assert.Equal("source-panel", model.SourcePanel2DDocumentId);
        Assert.Equal(20d, model.SourceRegion!.X);
        Assert.Equal(30d, model.SourceRegion!.Y);
        var artwork = Assert.IsType<FaceArtworkElement>(model.Elements[0]);
        Assert.Equal("source-panel", artwork.SourcePanel2DDocumentId);
        Assert.Equal(20d, artwork.SourceRegion!.X);
        Assert.Equal("Assets/Panel2D/glass.png", artwork.Provenance!.SourceAssetPath);

        var element = Assert.IsType<FaceLampWindowElement>(model.Elements[1]);
        Assert.Equal("lamp:5", element.LinkedMachineObjectReference?.ToString());
        Assert.Equal("lamp-5", element.LinkedPanel2DElementId);
        Assert.Equal(5d, element.X);
        Assert.Equal(5d, element.Y);
    }
    [Fact]
    public void GenerateFromPanelRegion_ConvertsContainedSevenSegmentDisplaysWithMachineReferences()
    {
        var panel = new Panel2DDocumentModel
        {
            Elements =
            [
                new PanelElementModel
                {
                    ObjectId = "seven-3",
                    Name = "Credit Digit",
                    Kind = PanelElementKind.SevenSegment,
                    X = 120,
                    Y = 230,
                    Width = 24,
                    Height = 36,
                    DisplayNumber = 3,
                    OnColorHex = "#FF2020",
                    OffColorHex = "#220404",
                    ShowDecimalPoint = true,
                    IsVisible = true
                }
            ]
        };

        var result = new FaceGenerationService().GenerateFromPanelRegion(
            panel,
            FaceSourceRegionModel.FromRect(new Rect(100, 200, 200, 150)),
            "Generated Face",
            "panel-doc-1");

        Assert.Equal(1, result.ConvertedSevenSegmentDisplayCount);
        var element = Assert.IsType<FaceSevenSegmentDisplayElement>(result.Document.Elements[1]);
        Assert.Equal("face-seven-3", element.ObjectId);
        Assert.Equal("Credit Digit", element.Name);
        Assert.Equal(20d, element.X);
        Assert.Equal(30d, element.Y);
        Assert.Equal("sevenSegment:3", element.LinkedMachineObjectReference?.ToString());
        Assert.Equal("seven-3", element.LinkedPanel2DElementId);
        Assert.Equal("#FF2020", element.OnColorHex);
        Assert.Equal("#220404", element.OffColorHex);
        Assert.True(element.ShowDecimalPoint);
    }

    [Fact]
    public void GenerateFromPanelRegion_ConvertsContainedAlphaDisplaysWithMachineReferences()
    {
        var panel = new Panel2DDocumentModel
        {
            Elements =
            [
                new PanelElementModel
                {
                    ObjectId = "alpha-10",
                    Name = "Message Display",
                    Kind = PanelElementKind.Alpha,
                    X = 125,
                    Y = 235,
                    Width = 160,
                    Height = 32,
                    DisplayNumber = 10,
                    SegmentDisplayType = "bfm16seg",
                    OnColorHex = "#FF4040",
                    OffColorHex = "#200808",
                    ShowDecimalPoint = true,
                    ShowCommaTail = true,
                    IsReversed = true,
                    IsVisible = true
                }
            ]
        };

        var result = new FaceGenerationService().GenerateFromPanelRegion(
            panel,
            FaceSourceRegionModel.FromRect(new Rect(100, 200, 220, 120)),
            "Generated Face",
            "panel-doc-1");

        Assert.Equal(1, result.ConvertedAlphaDisplayCount);
        var element = Assert.IsType<FaceAlphaDisplayElement>(result.Document.Elements[1]);
        Assert.Equal("face-alpha-10", element.ObjectId);
        Assert.Equal("Message Display", element.Name);
        Assert.Equal(25d, element.X);
        Assert.Equal(35d, element.Y);
        Assert.Equal("alpha:10", element.LinkedMachineObjectReference?.ToString());
        Assert.Equal("alpha-10", element.LinkedPanel2DElementId);
        Assert.Equal("bfm16seg", element.SegmentDisplayType);
        Assert.Equal("#FF4040", element.OnColorHex);
        Assert.Equal("#200808", element.OffColorHex);
        Assert.True(element.ShowDecimalPoint);
        Assert.True(element.ShowCommaTail);
        Assert.True(element.IsReversed);
    }

}
