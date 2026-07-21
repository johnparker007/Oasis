using System.Windows;
using OasisEditor.Features.CabinetEditor.Models;
using Xunit;

namespace OasisEditor.Tests;

public sealed class FaceGenerationServiceTests
{






    [Fact]
    public void GenerateFromPanelFaceSourceShape_TransformsContainedLampsIntoFaceSpace()
    {
        var panel = new Panel2DDocumentModel
        {
            Elements =
            [
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
                    SecondaryAssetPath = "Assets/Masks/lamp-17.png",
                    SourceComponentIndex = 2,
                    SharedSourceSetId = "set-a",
                    SharedSourceSetCount = 3,
                    SourceBlend = true,
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
        var sourceShape = new PanelFaceSourceShapeModel
        {
            Id = "shape-1",
            Name = "Glass",
            TopLeft = new FacePointModel { X = 100, Y = 200 },
            TopRight = new FacePointModel { X = 300, Y = 200 },
            BottomRight = new FacePointModel { X = 300, Y = 400 },
            BottomLeft = new FacePointModel { X = 100, Y = 400 }
        };

        var result = new FaceGenerationService().GenerateFromPanelFaceSourceShape(panel, sourceShape, "Face", "panel-doc-1");

        Assert.Equal(1, result.ConvertedLampCount);
        Assert.Equal("shape-1", result.Document.SourceFaceShapeId);
        var lamp = Assert.IsType<FaceLampWindowElement>(Assert.Single(result.Document.Elements.OfType<FaceLampWindowElement>()));
        Assert.Equal("face-lamp-17", lamp.ObjectId);
        Assert.Equal("Start Lamp", lamp.Name);
        Assert.Equal(10d, lamp.X, 9);
        Assert.Equal(20d, lamp.Y, 9);
        Assert.Equal(30d, lamp.Width, 9);
        Assert.Equal(40d, lamp.Height, 9);
        Assert.Equal("lamp:17", lamp.LinkedMachineObjectReference?.ToString());
        Assert.Equal("lamp-17", lamp.LinkedPanel2DElementId);
        Assert.Null(lamp.BulbMaskAssetPath);
        Assert.Equal(2, lamp.SourceComponentIndex);
        Assert.Equal("set-a", lamp.SharedSourceSetId);
        Assert.Equal(3, lamp.SharedSourceSetCount);
        Assert.True(lamp.SourceBlend);
    }



    [Fact]
    public void GenerateFromPanelFaceSourceShape_ConvertsSemanticComponentsAndPreservesProperties()
    {
        var buttonVisualId = Guid.NewGuid();
        var panel = new Panel2DDocumentModel
        {
            Elements =
            [
                new PanelElementModel { ObjectId = "reel-1", Name = "Reel 1", Kind = PanelElementKind.Reel, X = 10, Y = 10, Width = 20, Height = 40, DisplayNumber = 3, AssetPath = "Assets/Reels/reel.png", Stops = 24, VisibleScale = 1.5, BandOffset = 2.25, IsReversed = true, IsVisible = false },
                new PanelElementModel { ObjectId = "seven-1", Name = "Seven", Kind = PanelElementKind.SevenSegment, X = 40, Y = 10, Width = 30, Height = 10, DisplayNumber = 2, OnColorHex = "#FFFF0000", OffColorHex = "#FF220000", ShowDecimalPoint = true },
                new PanelElementModel { ObjectId = "alpha-1", Name = "Alpha", Kind = PanelElementKind.Alpha, X = 10, Y = 60, Width = 50, Height = 10, DisplayNumber = 1, SegmentDisplayType = "bfm-alpha", OnColorHex = "#FF00FF00", OffColorHex = "#FF002200", ShowDecimalPoint = true, ShowCommaTail = true, IsReversed = true },
                new PanelElementModel { ObjectId = buttonVisualId.ToString("N"), Name = "Start Button", Kind = PanelElementKind.Rectangle, X = 70, Y = 70, Width = 10, Height = 10 }
            ]
        };

        var result = new FaceGenerationService().GenerateFromPanelFaceSourceShape(
            panel,
            CreateSourceShape(),
            "Face",
            "panel-doc-1",
            inputDefinitions: [new InputDefinitionModel { Id = "start", Name = "Start", Kind = InputDefinitionKind.Button, LinkedVisualElementId = buttonVisualId }]);

        Assert.Equal(1, result.ConvertedReelDisplayCount);
        Assert.Equal(1, result.ConvertedSevenSegmentDisplayCount);
        Assert.Equal(1, result.ConvertedAlphaDisplayCount);
        Assert.Equal(1, result.ConvertedButtonCount);
        var reel = Assert.IsType<FaceReelDisplayElement>(Assert.Single(result.Document.Elements.OfType<FaceReelDisplayElement>()));
        Assert.Equal("Assets/Reels/reel.png", reel.AssetPath);
        Assert.Equal(24, reel.Stops);
        Assert.Equal(1.5, reel.VisibleScale);
        Assert.Equal(2.25, reel.BandOffset);
        Assert.True(reel.IsReversed);
        Assert.False(reel.IsVisible);
        Assert.Equal("reel:3", reel.LinkedMachineObjectReference?.ToString());
        Assert.Equal("reel-1", reel.LinkedPanel2DElementId);
        Assert.Null(reel.ReelSpecificationId);
        var seven = Assert.Single(result.Document.Elements.OfType<FaceSevenSegmentDisplayElement>());
        Assert.Equal("sevenSegment:2", seven.LinkedMachineObjectReference?.ToString());
        Assert.Equal("#FFFF0000", seven.OnColorHex);
        Assert.Equal("#FF220000", seven.OffColorHex);
        Assert.True(seven.ShowDecimalPoint);
        var alpha = Assert.Single(result.Document.Elements.OfType<FaceAlphaDisplayElement>());
        Assert.Equal("alpha:1", alpha.LinkedMachineObjectReference?.ToString());
        Assert.Equal("bfm-alpha", alpha.SegmentDisplayType);
        Assert.True(alpha.ShowDecimalPoint);
        Assert.True(alpha.ShowCommaTail);
        Assert.True(alpha.IsReversed);
        var button = Assert.Single(result.Document.Elements.OfType<FaceButtonElement>());
        Assert.Equal("input:start", button.LinkedMachineObjectReference?.ToString());
        Assert.Equal("input:start", button.LinkedInputReference?.ToString());
        Assert.Equal(buttonVisualId.ToString("N"), button.LinkedPanel2DElementId);
    }

    [Fact]
    public void SemanticConversion_UsesCenterInclusionAndFourCornerPerspectiveBounds()
    {
        var service = new FaceSemanticElementConversionService();
        var shape = new PanelFaceSourceShapeModel
        {
            TopLeft = new FacePointModel { X = 10, Y = 10 },
            TopRight = new FacePointModel { X = 110, Y = 0 },
            BottomRight = new FacePointModel { X = 90, Y = 110 },
            BottomLeft = new FacePointModel { X = 0, Y = 100 }
        };
        var inside = new PanelElementModel { ObjectId = "inside", Kind = PanelElementKind.Reel, X = 20, Y = 20, Width = 30, Height = 30 };
        var outside = new PanelElementModel { ObjectId = "outside", Kind = PanelElementKind.Reel, X = 200, Y = 200, Width = 30, Height = 30 };

        Assert.True(FaceSemanticElementConversionService.IsCenterInsideSourceShape(inside, shape));
        Assert.False(FaceSemanticElementConversionService.IsCenterInsideSourceShape(outside, shape));
        var bounds = Assert.IsType<FaceReelDisplayElement>(Assert.Single(service.ConvertSupportedElements(new Panel2DDocumentModel { Elements = [inside, outside] }, shape, 200, 120, null).OfType<FaceReelDisplayElement>()));
        var transformedCorners = new[]
        {
            (X: inside.X, Y: inside.Y),
            (X: inside.X + inside.Width, Y: inside.Y),
            (X: inside.X + inside.Width, Y: inside.Y + inside.Height),
            (X: inside.X, Y: inside.Y + inside.Height)
        }.Select(c => { FaceSourceShapeTransformService.TryTransformPanelPointToFace(shape, 200, 120, c.X, c.Y, out var point); return point; }).ToArray();
        Assert.Equal(transformedCorners.Min(p => p.X), bounds.X, 9);
        Assert.Equal(transformedCorners.Min(p => p.Y), bounds.Y, 9);
        Assert.Equal(transformedCorners.Max(p => p.X) - transformedCorners.Min(p => p.X), bounds.Width, 9);
        Assert.Equal(transformedCorners.Max(p => p.Y) - transformedCorners.Min(p => p.Y), bounds.Height, 9);
    }

    [Fact]
    public void Regenerate_PreservesSemanticObjectIdsAndUpdatesSourceDerivedFieldsWithoutDuplicates()
    {
        var existingFace = new FaceDocumentModel
        {
            Id = "face-1",
            Title = "Face",
            SourcePanel2DDocumentId = "panel-doc-1",
            SourceFaceShapeId = "shape-1",
            SourceRegion = FaceSourceRegionModel.FromRect(new Rect(0, 0, 100, 100)),
            Elements =
            [
                new FaceReelDisplayElement { ObjectId = "existing-reel", Name = "Old", LinkedPanel2DElementId = "reel-1", X = 0, Y = 0, Width = 1, Height = 1, LinkedMachineObjectReference = MachineObjectReference.Reel(99), ReelSpecificationId = "user-selected" },
                new FaceArtworkElement { ObjectId = "manual-art", Name = "Manual", X = 1, Y = 1, Width = 2, Height = 2 }
            ]
        };
        var panel = new Panel2DDocumentModel
        {
            FaceSourceShapes = [CreateSourceShape()],
            Elements = [new PanelElementModel { ObjectId = "reel-1", Name = "New Reel", Kind = PanelElementKind.Reel, X = 20, Y = 20, Width = 30, Height = 40, DisplayNumber = 3, AssetPath = "Assets/Reels/new.png", Stops = 18 }]
        };

        var result = new FaceRegenerationService().Regenerate(existingFace, panel, documentPath: "Assets/Faces/Face/asset.face");

        var reel = Assert.Single(result.Document.Elements.OfType<FaceReelDisplayElement>());
        Assert.Equal("existing-reel", reel.ObjectId);
        Assert.Equal("New Reel", reel.Name);
        Assert.Equal("reel:99", reel.LinkedMachineObjectReference?.ToString());
        Assert.Equal("Assets/Reels/new.png", reel.AssetPath);
        Assert.Equal(18, reel.Stops);
        Assert.Equal("user-selected", reel.ReelSpecificationId);
        Assert.Single(result.Document.Elements.Where(e => e.LinkedPanel2DElementId == "reel-1"));
        Assert.Contains(result.Document.Elements, e => e.ObjectId == "manual-art");
    }


    [Fact]
    public void GenerateFromPanelFaceSourceShape_AssignsCabinetDefaultReelSpecification()
    {
        var panel = new Panel2DDocumentModel
        {
            Elements = [new PanelElementModel { ObjectId = "reel-1", Kind = PanelElementKind.Reel, X = 10, Y = 10, Width = 20, Height = 40, DisplayNumber = 1 }]
        };
        var cabinet = new CabinetDocument(
            2,
            new CabinetModelReference("source.glb", 1, "Y"),
            [],
            CabinetPreviewSettings.Default,
            [new CabinetReelSpecification("default-reel", "Default", 210, 50)],
            "default-reel");

        var result = new FaceGenerationService().GenerateFromPanelFaceSourceShape(panel, CreateSourceShape(), "Face", cabinetDocument: cabinet);

        var reel = Assert.Single(result.Document.Elements.OfType<FaceReelDisplayElement>());
        Assert.Equal("default-reel", reel.ReelSpecificationId);
    }

    [Fact]
    public void GenerateFromPanelFaceSourceShape_LocksGeneratedArtworkTransformByDefault()
    {
        var result = new FaceGenerationService().GenerateFromPanelFaceSourceShape(CreatePanelWithFaceSourceShape(), CreateSourceShape(), "Face", "panel-doc-1");

        var artwork = Assert.IsType<FaceArtworkElement>(Assert.Single(result.Document.Elements.OfType<FaceArtworkElement>()));
        Assert.True(artwork.IsTransformLocked);
    }

    [Fact]
    public void Regenerate_LocksReplacementGeneratedArtworkTransformByDefault()
    {
        var existingFace = new FaceDocumentModel
        {
            Id = "face-1",
            Title = "Face",
            SourcePanel2DDocumentId = "panel-doc-1",
            SourceFaceShapeId = "shape-1",
            SourceRegion = FaceSourceRegionModel.FromRect(new Rect(0, 0, 100, 100)),
            Elements =
            [
                new FaceArtworkElement
                {
                    ObjectId = "existing-artwork",
                    Name = "Existing Artwork",
                    X = 0,
                    Y = 0,
                    Width = 100,
                    Height = 100,
                    IsVisible = true,
                    SourcePanel2DDocumentId = "panel-doc-1"
                }
            ]
        };

        var result = new FaceRegenerationService().Regenerate(existingFace, CreatePanelWithFaceSourceShape(), documentPath: "Assets/Faces/Face/asset.face");

        var artwork = Assert.IsType<FaceArtworkElement>(Assert.Single(result.Document.Elements.OfType<FaceArtworkElement>()));
        Assert.Equal("existing-artwork", artwork.ObjectId);
        Assert.True(artwork.IsTransformLocked);
    }

    private static Panel2DDocumentModel CreatePanelWithFaceSourceShape()
    {
        return new Panel2DDocumentModel
        {
            FaceSourceShapes = [CreateSourceShape()]
        };
    }

    private static PanelFaceSourceShapeModel CreateSourceShape()
    {
        return new PanelFaceSourceShapeModel
        {
            Id = "shape-1",
            Name = "Glass",
            TopLeft = new FacePointModel { X = 0, Y = 0 },
            TopRight = new FacePointModel { X = 100, Y = 0 },
            BottomRight = new FacePointModel { X = 100, Y = 100 },
            BottomLeft = new FacePointModel { X = 0, Y = 100 }
        };
    }

}
