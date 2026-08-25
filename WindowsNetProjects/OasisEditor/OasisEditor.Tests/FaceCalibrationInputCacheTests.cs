using SkiaSharp;
using Xunit;

namespace OasisEditor.Tests;

public sealed class FaceCalibrationInputCacheTests
{
    [Fact]
    public void RepeatedMeasurements_ReuseOperationInputEvaluation()
    {
        using var fixture = CacheFixture.Create();

        fixture.Document.GetArtworkCalibrationMeasurements(fixture.Target);
        fixture.Document.GetArtworkCalibrationMeasurements(fixture.Target);

        Assert.Equal(1, fixture.Document.CalibrationInputEvaluationCount);
        Assert.Equal(1, fixture.Document.CachedCalibrationInputCount);
    }

    [Fact]
    public void EditingCurrentOperationSamples_ReusesOperationInput()
    {
        using var fixture = CacheFixture.Create();
        fixture.Document.GetArtworkCalibrationMeasurements(fixture.Target);
        var editedTarget = Copy(fixture.Target, samples: [new CalibrationSampleModel { Id = "new", X = .5, Y = .5 }]);
        fixture.SetPipeline([fixture.Preceding, editedTarget]);

        fixture.Document.GetArtworkCalibrationMeasurements(editedTarget);

        Assert.Equal(1, fixture.Document.CalibrationInputEvaluationCount);
    }

    [Fact]
    public void EditingOperationBeforeTarget_InvalidatesTargetInput()
    {
        using var fixture = CacheFixture.Create();
        fixture.Document.GetArtworkCalibrationMeasurements(fixture.Target);
        var editedPreceding = Copy(fixture.Preceding, strength: 25);
        fixture.SetPipeline([editedPreceding, fixture.Target]);

        fixture.Document.GetArtworkCalibrationMeasurements(fixture.Target);

        Assert.Equal(2, fixture.Document.CalibrationInputEvaluationCount);
        Assert.Equal(1, fixture.Document.CachedCalibrationInputCount);
    }

    [Fact]
    public void ReorderingOperations_InvalidatesAffectedInput()
    {
        using var fixture = CacheFixture.Create();
        fixture.Document.GetArtworkCalibrationMeasurements(fixture.Target);
        var other = new ArtworkCalibrationOperationModel { Id = "other", Strength = 10 };
        fixture.SetPipeline([other, fixture.Target, fixture.Preceding]);

        fixture.Document.GetArtworkCalibrationMeasurements(fixture.Target);

        Assert.Equal(2, fixture.Document.CalibrationInputEvaluationCount);
    }

    [Fact]
    public void CorrectionInputInvalidation_DropsAllOwnedOperationInputs()
    {
        using var fixture = CacheFixture.Create();
        fixture.Document.GetArtworkCalibrationMeasurements(fixture.Target);
        Assert.Equal(1, fixture.Document.CachedCalibrationInputCount);

        fixture.Document.InvalidateFaceBuild(FaceBuildInput.ArtworkPreprocessing);

        Assert.Equal(0, fixture.Document.CachedCalibrationInputCount);
    }

    [Fact]
    public void DocumentDisposal_DropsAllOwnedOperationInputs()
    {
        using var fixture = CacheFixture.Create();
        fixture.Document.GetArtworkCalibrationMeasurements(fixture.Target);

        fixture.Document.Dispose();

        Assert.Equal(0, fixture.Document.CachedCalibrationInputCount);
    }

    [Fact]
    public void InspectorStyleMeasurement_DoesNotEvaluateOnCacheMiss()
    {
        using var fixture = CacheFixture.Create();

        var measurements = fixture.Document.GetArtworkCalibrationMeasurements(
            fixture.Target, allowInputEvaluation: false);

        Assert.Empty(measurements.SampleColors);
        Assert.Equal(0, fixture.Document.CalibrationInputEvaluationCount);
        Assert.Equal(0, fixture.Document.CachedCalibrationInputCount);
    }

    private static ArtworkCalibrationOperationModel Copy(
        ArtworkCalibrationOperationModel operation,
        double? strength = null,
        IReadOnlyList<CalibrationSampleModel>? samples = null) => new()
    {
        Id = operation.Id,
        Enabled = operation.Enabled,
        Strength = strength ?? operation.Strength,
        CorrectSpatialBrightness = operation.CorrectSpatialBrightness,
        CorrectSpatialColor = operation.CorrectSpatialColor,
        NormalizeBlackWhite = operation.NormalizeBlackWhite,
        NeutralizeWhite = operation.NeutralizeWhite,
        BlackReference = new CalibrationReferenceModel
        {
            ManualEnabled = operation.BlackReference.ManualEnabled,
            ManualColor = operation.BlackReference.ManualColor,
            Samples = samples ?? operation.BlackReference.Samples
        },
        WhiteReference = operation.WhiteReference,
        SameColorGroups = operation.SameColorGroups
    };

    private sealed class CacheFixture : IDisposable
    {
        private CacheFixture(string directory, DocumentTabViewModel document,
            ArtworkCalibrationOperationModel preceding, ArtworkCalibrationOperationModel target)
        {
            Directory = directory;
            Document = document;
            Preceding = preceding;
            Target = target;
        }

        private string Directory { get; }
        public DocumentTabViewModel Document { get; }
        public ArtworkCalibrationOperationModel Preceding { get; }
        public ArtworkCalibrationOperationModel Target { get; }

        public static CacheFixture Create()
        {
            var directory = Path.Combine(Path.GetTempPath(), $"oasis-calibration-cache-{Guid.NewGuid():N}");
            System.IO.Directory.CreateDirectory(directory);
            var inputPath = Path.Combine(directory, "correction-input.png");
            using (var bitmap = new SKBitmap(8, 8))
            {
                bitmap.Erase(new SKColor(40, 80, 120));
                using var image = SKImage.FromBitmap(bitmap);
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                using var stream = File.Create(inputPath);
                data.SaveTo(stream);
            }

            var preceding = new ArtworkCalibrationOperationModel { Id = "preceding", Strength = 50 };
            var target = new ArtworkCalibrationOperationModel
            {
                Id = "target",
                BlackReference = new CalibrationReferenceModel
                {
                    Samples = [new CalibrationSampleModel { Id = "sample", X = .5, Y = .5 }]
                }
            };
            var face = new FaceDocumentModel
            {
                BuildState = FaceBuildStateFactory.CreateGeneratedState(true, false, false, false, false),
                Artwork = new FaceArtworkModel
                {
                    CorrectionInputAssetPath = inputPath,
                    ProcessingPipeline = new ImageProcessingPipelineModel { Operations = [preceding, target] }
                }
            };
            var document = new DocumentTabViewModel(EditorDocument.CreateFaceStub("Face"),
                faceDocumentJson: FaceDocumentStorage.Serialize(face));
            document.SetProjectAccessor(() => new EditorProject
            {
                Name = "Test",
                ProjectDirectory = directory,
                ProjectFilePath = Path.Combine(directory, "test.oasisproj"),
                AssetsDirectory = Path.Combine(directory, "Assets"),
                MachinesDirectory = Path.Combine(directory, "Machines"),
                GeneratedDirectory = Path.Combine(directory, "Generated")
            });
            return new CacheFixture(directory, document, preceding, target);
        }

        public void SetPipeline(IReadOnlyList<ImageProcessingOperationModel> operations)
        {
            var face = Document.GetFaceDocument();
            var artwork = face.Artwork!;
            Document.SetFaceDocument(FaceDocumentCopy.WithArtwork(face, new FaceArtworkModel
            {
                Id = artwork.Id,
                Source = artwork.Source,
                Geometry = artwork.Geometry,
                ProcessingPipeline = new ImageProcessingPipelineModel { Operations = operations },
                CorrectionInputAssetPath = artwork.CorrectionInputAssetPath,
                BaseAssetPath = artwork.BaseAssetPath,
                OutputAssetPath = artwork.OutputAssetPath,
                OutputWidth = artwork.OutputWidth,
                OutputHeight = artwork.OutputHeight,
                Override = artwork.Override,
                FinalOutputWidth = artwork.FinalOutputWidth,
                FinalOutputHeight = artwork.FinalOutputHeight
            }, face.Provenance.Artwork), affectsFacePreview: false);
        }

        public void Dispose()
        {
            Document.Dispose();
            System.IO.Directory.Delete(Directory, true);
        }
    }
}
