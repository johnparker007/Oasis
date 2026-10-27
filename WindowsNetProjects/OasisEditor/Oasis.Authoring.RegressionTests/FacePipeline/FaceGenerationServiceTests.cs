using OasisEditor;
using OasisEditor.FacePipeline;
using OasisEditor.Features.CabinetEditor.Models;
using Xunit;

namespace OasisEditor.Tests;

public sealed class FaceGenerationServiceTests
{
    [Fact]
    public void GenerateFromPanelFaceSourceShape_DoesNotAcceptCabinetOwnershipParameters()
    {
        var parameterNames = typeof(FaceGenerationService)
            .GetMethod(nameof(FaceGenerationService.GenerateFromPanelFaceSourceShape))!
            .GetParameters()
            .Select(parameter => parameter.Name)
            .ToArray();

        Assert.DoesNotContain("assignedCabinetFaceTargetId", parameterNames);
        Assert.DoesNotContain("assignedCabinetAssetPath", parameterNames);
        Assert.DoesNotContain("cabinetDocument", parameterNames);
        Assert.Contains("targetAspectRatio", parameterNames);
    }

    [Fact]
    public void SourceShapeLampMask_MultipleWorkersMatchesSerialForOverlapThresholdAndPerspective()
    {
        const int width = 37, height = 29;
        var shape = new PanelFaceSourceShapeModel
        {
            TopLeft = new FacePointModel { X = 2, Y = 1 }, TopRight = new FacePointModel { X = 41, Y = 4 },
            BottomRight = new FacePointModel { X = 38, Y = 33 }, BottomLeft = new FacePointModel { X = 0, Y = 28 }
        };
        var lamp = new PanelElementModel { X = 4, Y = 3, Width = 30, Height = 24 };
        var firstWindow = new FaceLampWindowElement { X = -2, Y = 0, Width = 34, Height = 27 };
        var overlapWindow = new FaceLampWindowElement { X = 8, Y = 5, Width = 29, Height = 24 };
        using var source = new SkiaSharp.SKBitmap(31, 25, SkiaSharp.SKColorType.Bgra8888, SkiaSharp.SKAlphaType.Premul);
        for (var y = 0; y < source.Height; y++) for (var x = 0; x < source.Width; x++)
            source.SetPixel(x, y, new SkiaSharp.SKColor(220, 80, 20, (byte)((x * 17 + y * 11) % 256)));
        var serial = new byte[width * height]; var parallel = new byte[serial.Length]; var repeated = new byte[serial.Length];
        var serialFirst = FaceGenerationService.CompositeSourceShapeLampMask(serial, width, height, shape, lamp, source, firstWindow, 96, new OasisEditor.FacePipeline.ImageProcessingExecutionOptions(1));
        var parallelFirst = FaceGenerationService.CompositeSourceShapeLampMask(parallel, width, height, shape, lamp, source, firstWindow, 96, new OasisEditor.FacePipeline.ImageProcessingExecutionOptions(4));
        var repeatedFirst = FaceGenerationService.CompositeSourceShapeLampMask(repeated, width, height, shape, lamp, source, firstWindow, 96, new OasisEditor.FacePipeline.ImageProcessingExecutionOptions(4));
        var serialOverlap = FaceGenerationService.CompositeSourceShapeLampMask(serial, width, height, shape, lamp, source, overlapWindow, 128, new OasisEditor.FacePipeline.ImageProcessingExecutionOptions(1));
        var parallelOverlap = FaceGenerationService.CompositeSourceShapeLampMask(parallel, width, height, shape, lamp, source, overlapWindow, 128, new OasisEditor.FacePipeline.ImageProcessingExecutionOptions(4));
        var repeatedOverlap = FaceGenerationService.CompositeSourceShapeLampMask(repeated, width, height, shape, lamp, source, overlapWindow, 128, new OasisEditor.FacePipeline.ImageProcessingExecutionOptions(4));
        Assert.Equal(serial, parallel); Assert.Equal(parallel, repeated);
        AssertContributionEqual(serialFirst, parallelFirst); AssertContributionEqual(parallelFirst, repeatedFirst);
        AssertContributionEqual(serialOverlap, parallelOverlap); AssertContributionEqual(parallelOverlap, repeatedOverlap);
        Assert.Contains(serial, value => value == 0); Assert.Contains(serial, value => value >= 128);
    }

    private static void AssertContributionEqual(FaceGenerationService.SourceShapeMaskContribution expected,
        FaceGenerationService.SourceShapeMaskContribution actual)
    {
        Assert.Equal(expected.PixelCount, actual.PixelCount);
        Assert.Equal(expected.Bounds?.X, actual.Bounds?.X); Assert.Equal(expected.Bounds?.Y, actual.Bounds?.Y);
        Assert.Equal(expected.Bounds?.Width, actual.Bounds?.Width); Assert.Equal(expected.Bounds?.Height, actual.Bounds?.Height);
    }

    [Fact]
    public void SourceShapeLampMask_CancellationStopsBeforePublishingRows()
    {
        using var source = new SkiaSharp.SKBitmap(4, 4, SkiaSharp.SKColorType.Rgba8888, SkiaSharp.SKAlphaType.Premul);
        source.Erase(SkiaSharp.SKColors.White);
        var cancellation = new CancellationToken(canceled: true);
        Assert.Throws<OperationCanceledException>(() => FaceGenerationService.CompositeSourceShapeLampMask(
            new byte[16], 4, 4, CreateSourceShape(), new PanelElementModel { Width = 100, Height = 100 }, source,
            new FaceLampWindowElement { Width = 4, Height = 4 }, 1, new OasisEditor.FacePipeline.ImageProcessingExecutionOptions(4, cancellation)));
    }

    [Fact]
    public void GenerateFromPanelFaceSourceShape_CreatesCanonicalOriginalArtwork()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"oasis-face-generation-{Guid.NewGuid():N}");
        var faceDirectory = Path.Combine(directory, "Assets", "Faces", "TestFace");
        Directory.CreateDirectory(faceDirectory);
        try
        {
            var backgroundPath = Path.Combine(directory, "background.png");
            using (var bitmap = new SkiaSharp.SKBitmap(8, 8))
            using (var image = SkiaSharp.SKImage.FromBitmap(bitmap))
            using (var data = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100))
            using (var stream = File.Create(backgroundPath)) data.SaveTo(stream);
            var panel = new Panel2DDocumentModel
            {
                Elements = [new PanelElementModel { Kind = PanelElementKind.Background, AssetPath = backgroundPath, Width = 8, Height = 8 }]
            };

            var result = new FaceGenerationService().GenerateFromPanelFaceSourceShape(
                panel, CreateSourceShape(), "Test Face", projectDirectory: directory, faceAssetDirectory: faceDirectory);

            var generatedPath = Path.Combine(directory, result.Document.Artwork!.OutputAssetPath!.Replace('/', Path.DirectorySeparatorChar));
            Assert.Equal(Path.Combine(directory, "Generated", "Faces", "Test Face", "Artwork", "artwork.png"), generatedPath);
            Assert.True(File.Exists(generatedPath));
            Assert.True(File.Exists(FaceArtworkGeneratedPathService.GetCorrectionInputPathFromOutput(generatedPath)));
            Assert.True(File.Exists(FaceArtworkGeneratedPathService.GetBasePathFromOutput(generatedPath)));
            Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(generatedPath)!, "original.png")));
            Assert.False(Directory.Exists(Path.Combine(faceDirectory, "generated")));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }






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

        Assert.Equal(1, result.ConvertedReelMountCount);
        Assert.Equal(1, result.ConvertedSevenSegmentDisplayCount);
        Assert.Equal(1, result.ConvertedAlphaDisplayCount);
        Assert.Equal(1, result.ConvertedButtonCount);
        var reel = Assert.IsType<FaceReelMount>(Assert.Single(result.Document.Elements.OfType<FaceReelMount>()));
        Assert.Equal("Assets/Reels/reel.png", reel.AssetPath);
        Assert.Equal(24, reel.Stops);
        Assert.Equal(1.5, reel.VisibleScale);
        Assert.Equal(2.25, reel.BandOffset);
        Assert.True(reel.IsReversed);
        Assert.False(reel.IsVisible);
        Assert.Equal("reel:3", reel.LinkedMachineObjectReference?.ToString());
        Assert.Equal("reel-1", reel.LinkedPanel2DElementId);
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
        var bounds = Assert.IsType<FaceReelMount>(Assert.Single(service.ConvertSupportedElements(new Panel2DDocumentModel { Elements = [inside, outside] }, shape, 200, 120, null).OfType<FaceReelMount>()));
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
    public void GenerateFromPanelFaceSourceShape_LocksGeneratedArtworkTransformByDefault()
    {
        var result = new FaceGenerationService().GenerateFromPanelFaceSourceShape(CreatePanelWithFaceSourceShape(), CreateSourceShape(), "Face", "panel-doc-1");

        var artwork = Assert.IsType<FaceArtworkElement>(Assert.Single(result.Document.Elements.OfType<FaceArtworkElement>()));
        Assert.True(artwork.IsTransformLocked);
        var authoredArtwork = Assert.IsType<FaceArtworkModel>(result.Document.Artwork);
        Assert.Equal(FaceArtworkSourceKind.Panel2DFaceSourceShape, authoredArtwork.Source.Kind);
        Assert.Equal("panel-doc-1", authoredArtwork.Source.Panel2DDocumentId);
        Assert.Equal("shape-1", authoredArtwork.Source.FaceSourceShapeId);
        Assert.Empty(authoredArtwork.ProcessingPipeline.Operations);
        Assert.Equal((int)artwork.Width, authoredArtwork.OutputWidth);
        Assert.Equal((int)artwork.Height, authoredArtwork.OutputHeight);
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

    private static FaceGenerationSettingsModel SharpeningSettings(bool enabled) => new()
    {
        PostWarpSharpeningEnabled = enabled,
        PostWarpSharpeningAmount = 1,
        PostWarpSharpeningRadiusPixels = 0.75,
        PostWarpSharpeningThreshold = 0
    };

    private static int EdgeContrast(SkiaSharp.SKBitmap bitmap) =>
        bitmap.GetPixel(52, 50).Red - bitmap.GetPixel(47, 50).Red;

}
