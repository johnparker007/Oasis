using OasisEditor;
using System.Windows;
using OasisEditor.Features.CabinetEditor.Models;
using Xunit;

namespace OasisEditor.Tests;

public sealed class FaceGenerationServiceTests
{
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
        var serialFirst = FaceGenerationService.CompositeSourceShapeLampMask(serial, width, height, shape, lamp, source, firstWindow, 96, new ImageProcessingExecutionOptions(1));
        var parallelFirst = FaceGenerationService.CompositeSourceShapeLampMask(parallel, width, height, shape, lamp, source, firstWindow, 96, new ImageProcessingExecutionOptions(4));
        var repeatedFirst = FaceGenerationService.CompositeSourceShapeLampMask(repeated, width, height, shape, lamp, source, firstWindow, 96, new ImageProcessingExecutionOptions(4));
        var serialOverlap = FaceGenerationService.CompositeSourceShapeLampMask(serial, width, height, shape, lamp, source, overlapWindow, 128, new ImageProcessingExecutionOptions(1));
        var parallelOverlap = FaceGenerationService.CompositeSourceShapeLampMask(parallel, width, height, shape, lamp, source, overlapWindow, 128, new ImageProcessingExecutionOptions(4));
        var repeatedOverlap = FaceGenerationService.CompositeSourceShapeLampMask(repeated, width, height, shape, lamp, source, overlapWindow, 128, new ImageProcessingExecutionOptions(4));
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
            new FaceLampWindowElement { Width = 4, Height = 4 }, 1, new ImageProcessingExecutionOptions(4, cancellation)));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Regenerate_OverwritesStableArtworkUsingCurrentSharpeningSettings(bool initiallyEnabled, bool regeneratedEnabled)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"oasis-face-sharpen-regeneration-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var backgroundPath = Path.Combine(directory, "background.png");
            using (var bitmap = new SkiaSharp.SKBitmap(100, 100, SkiaSharp.SKColorType.Rgba8888, SkiaSharp.SKAlphaType.Premul))
            {
                byte[] edge = [30, 45, 70, 105, 150, 190, 215, 225];
                for (var y = 0; y < bitmap.Height; y++)
                for (var x = 0; x < bitmap.Width; x++)
                {
                    var value = edge[Math.Clamp(x - 46, 0, edge.Length - 1)];
                    bitmap.SetPixel(x, y, new SkiaSharp.SKColor(value, value, value));
                }
                using var image = SkiaSharp.SKImage.FromBitmap(bitmap);
                using var data = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
                using var stream = File.Create(backgroundPath);
                data.SaveTo(stream);
            }

            var shape = CreateSourceShape();
            var panel = new Panel2DDocumentModel
            {
                FaceSourceShapes = [shape],
                Elements = [new PanelElementModel { Kind = PanelElementKind.Background, AssetPath = backgroundPath, Width = 100, Height = 100 }]
            };
            var initial = new FaceGenerationService().GenerateFromPanelFaceSourceShape(
                panel, shape, "Face", sourcePanel2DDocumentId: "panel-doc", projectDirectory: directory,
                faceAssetName: "Face", generationSettings: SharpeningSettings(initiallyEnabled));
            var initialPath = Path.Combine(directory, initial.Document.Artwork!.OutputAssetPath!.Replace('/', Path.DirectorySeparatorChar));
            var initialBytes = File.ReadAllBytes(initialPath);
            using var initialBitmap = SkiaSharp.SKBitmap.Decode(initialPath);
            var initialContrast = EdgeContrast(initialBitmap);

            var regenerated = new FaceRegenerationService().Regenerate(
                initial.Document, panel, directory, Path.Combine(directory, "Generated"),
                generationSettings: SharpeningSettings(regeneratedEnabled),
                documentPath: Path.Combine(directory, "Assets", "Faces", "Face", "asset.face"));
            var regeneratedPath = Path.Combine(directory, regenerated.Document.Artwork!.OutputAssetPath!.Replace('/', Path.DirectorySeparatorChar));
            using var regeneratedBitmap = SkiaSharp.SKBitmap.Decode(regeneratedPath);
            var regeneratedContrast = EdgeContrast(regeneratedBitmap);

            Assert.Equal(initial.Document.Artwork.OutputAssetPath, regenerated.Document.Artwork.OutputAssetPath);
            Assert.Equal(regeneratedEnabled, regenerated.Document.GenerationSettings.PostWarpSharpeningEnabled);
            Assert.False(initialBytes.SequenceEqual(File.ReadAllBytes(regeneratedPath)));
            if (regeneratedEnabled) Assert.True(regeneratedContrast > initialContrast);
            else Assert.True(regeneratedContrast < initialContrast);
        }
        finally { Directory.Delete(directory, recursive: true); }
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
            5,
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
        var authoredArtwork = Assert.IsType<FaceArtworkModel>(result.Document.Artwork);
        Assert.Equal(FaceArtworkSourceKind.Panel2DFaceSourceShape, authoredArtwork.Source.Kind);
        Assert.Equal("panel-doc-1", authoredArtwork.Source.Panel2DDocumentId);
        Assert.Equal("shape-1", authoredArtwork.Source.FaceSourceShapeId);
        Assert.Empty(authoredArtwork.ProcessingPipeline.Operations);
        Assert.Equal((int)artwork.Width, authoredArtwork.OutputWidth);
        Assert.Equal((int)artwork.Height, authoredArtwork.OutputHeight);
    }

    [Fact]
    public void Regenerate_PreservesFaceOwnedArtworkRecipeAndStableId()
    {
        var existingFace = new FaceDocumentModel
        {
            Id = "face-1",
            Title = "Face",
            SourcePanel2DDocumentId = "panel-doc-1",
            SourceFaceShapeId = "shape-1",
            SourceRegion = FaceSourceRegionModel.FromRect(new Rect(0, 0, 100, 100)),
            Artwork = new FaceArtworkModel
            {
                Id = "stable-artwork",
                Source = new FaceArtworkSourceModel { Kind = FaceArtworkSourceKind.Panel2DFaceSourceShape, Panel2DDocumentId = "panel-doc-1", FaceSourceShapeId = "shape-1" },
                ProcessingPipeline = new ImageProcessingPipelineModel
                {
                    Operations = [new ArtworkCalibrationOperationModel { Id = "operation-1", Enabled = false }]
                },
                OutputWidth = 100,
                OutputHeight = 100
            }
        };

        var result = new FaceRegenerationService().Regenerate(existingFace, CreatePanelWithFaceSourceShape(), documentPath: "Assets/Faces/Face/asset.face");

        var artwork = Assert.IsType<FaceArtworkModel>(result.Document.Artwork);
        Assert.Equal("stable-artwork", artwork.Id);
        Assert.Equal("operation-1", Assert.Single(artwork.ProcessingPipeline.Operations).Id);
        Assert.Equal(FaceArtworkSourceKind.Panel2DFaceSourceShape, artwork.Source.Kind);
    }

    [Fact]
    public void Regenerate_RecreatesCanonicalAndProcessesItWithPreservedCalibrationRecipe()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"oasis-face-regeneration-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var backgroundPath = Path.Combine(directory, "background.png");
            using (var bitmap = new SkiaSharp.SKBitmap(100, 100))
            {
                bitmap.Erase(new SkiaSharp.SKColor(80, 80, 80));
                using var image = SkiaSharp.SKImage.FromBitmap(bitmap);
                using var data = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
                using var stream = File.Create(backgroundPath);
                data.SaveTo(stream);
            }
            var panel = new Panel2DDocumentModel
            {
                FaceSourceShapes = [CreateSourceShape()],
                Elements = [new PanelElementModel { Kind = PanelElementKind.Background, AssetPath = backgroundPath, Width = 100, Height = 100 }]
            };
            var sample = new CalibrationSampleModel { Id = "sample-1", X = .25, Y = .75, SamplingMode = CalibrationSamplingMode.Area, RadiusNormalized = .05 };
            var operation = new ArtworkCalibrationOperationModel
            {
                Id = "calibration-1",
                CorrectSpatialBrightness = false,
                CorrectSpatialColor = false,
                NeutralizeWhite = false,
                BlackReference = new CalibrationReferenceModel { ManualEnabled = true, ManualColor = "#FF202020", Samples = [sample] },
                WhiteReference = new CalibrationReferenceModel { ManualEnabled = true, ManualColor = "#FF707070" },
                SameColorGroups = [new SameColorCalibrationGroupModel { Id = "group-1", Name = "Grey", Samples = [sample] }]
            };
            var existingFace = new FaceDocumentModel
            {
                Id = "face-1", Title = "Face", SourcePanel2DDocumentId = "panel-doc-1", SourceFaceShapeId = "shape-1",
                SourceRegion = FaceSourceRegionModel.FromRect(new Rect(0, 0, 100, 100)),
                Artwork = new FaceArtworkModel { Id = "stable-artwork", ProcessingPipeline = new ImageProcessingPipelineModel { Operations = [operation] } }
            };
            var documentPath = Path.Combine(directory, "Assets", "Faces", "Face", "asset.face");

            var result = new FaceRegenerationService().Regenerate(existingFace, panel, directory, Path.Combine(directory, "Generated"), documentPath: documentPath);

            var artwork = Assert.IsType<FaceArtworkModel>(result.Document.Artwork);
            var savedOperation = Assert.IsType<ArtworkCalibrationOperationModel>(Assert.Single(artwork.ProcessingPipeline.Operations));
            Assert.Equal("calibration-1", savedOperation.Id);
            Assert.Equal("sample-1", Assert.Single(savedOperation.BlackReference.Samples).Id);
            Assert.Equal("group-1", Assert.Single(savedOperation.SameColorGroups).Id);
            var processedPath = Path.Combine(directory, artwork.OutputAssetPath!.Replace('/', Path.DirectorySeparatorChar));
            var basePath = FaceArtworkGeneratedPathService.GetBasePathFromOutput(processedPath);
            Assert.True(File.Exists(basePath));
            using var baseArtwork = SkiaSharp.SKBitmap.Decode(basePath);
            using var processed = SkiaSharp.SKBitmap.Decode(processedPath);
            Assert.NotNull(baseArtwork);
            Assert.NotNull(processed);
            Assert.Equal(baseArtwork.GetPixel(50, 50), processed.GetPixel(50, 50));
        }
        finally { Directory.Delete(directory, recursive: true); }
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
