using OasisEditor;
using Xunit;

namespace OasisEditor.Tests;

public sealed class FacePreviewInvalidationTests
{
    [Fact]
    public void AddingCalibrationSample_PersistsInvalidatesAndIsUndoable_WithoutPreviewRefresh()
    {
        var (document, calibration) = CreateCalibrationDocument();
        var previewChanges = 0;
        var jsonChanges = 0;
        document.FacePreviewChanged += _ => previewChanges++;
        document.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(DocumentTabViewModel.FaceDocumentJson)) jsonChanges++;
        };
        var sample = new CalibrationSampleModel { Id = "added", X = .25, Y = .75 };
        var updated = CopyWithBlackSamples(calibration, [.. calibration.BlackReference.Samples, sample]);

        document.CommandService.Execute(FaceMutationCommands.CreateUpdateProcessingOperationCommand(
            document.DocumentId, document, updated, "Add black calibration sample"));

        var authored = Assert.IsType<ArtworkCalibrationOperationModel>(Assert.Single(
            document.GetFaceDocument().Artwork!.ProcessingPipeline.Operations));
        Assert.Contains(authored.BlackReference.Samples, candidate => candidate.Id == sample.Id);
        AssertArtworkProductsAreStale(document);
        Assert.True(document.IsDirty);
        Assert.True(document.CommandService.CanUndo);
        Assert.True(jsonChanges > 0);
        Assert.Equal(0, previewChanges);

        Assert.True(document.CommandService.TryUndo());
        authored = Assert.IsType<ArtworkCalibrationOperationModel>(Assert.Single(
            document.GetFaceDocument().Artwork!.ProcessingPipeline.Operations));
        Assert.Empty(authored.BlackReference.Samples);
        Assert.Equal(0, previewChanges);
    }

    [Fact]
    public void RemovingCalibrationSample_PersistsInvalidatesAndIsUndoable_WithoutPreviewRefresh()
    {
        var existing = new CalibrationSampleModel { Id = "existing", X = .5, Y = .5 };
        var (document, calibration) = CreateCalibrationDocument([existing]);
        var previewChanges = 0;
        document.FacePreviewChanged += _ => previewChanges++;
        var updated = CopyWithBlackSamples(calibration, []);

        document.CommandService.Execute(FaceMutationCommands.CreateUpdateProcessingOperationCommand(
            document.DocumentId, document, updated, "Remove black calibration sample"));

        Assert.Empty(Assert.IsType<ArtworkCalibrationOperationModel>(Assert.Single(
            document.GetFaceDocument().Artwork!.ProcessingPipeline.Operations)).BlackReference.Samples);
        AssertArtworkProductsAreStale(document);
        Assert.True(document.IsDirty);
        Assert.True(document.CommandService.TryUndo());
        Assert.Single(Assert.IsType<ArtworkCalibrationOperationModel>(Assert.Single(
            document.GetFaceDocument().Artwork!.ProcessingPipeline.Operations)).BlackReference.Samples);
        Assert.Equal(0, previewChanges);
    }

    [Fact]
    public void FaceVisualModelChange_RequestsPreviewRefresh()
    {
        var (document, _) = CreateCalibrationDocument();
        var previewChanges = 0;
        document.FacePreviewChanged += change =>
        {
            Assert.Equal(document.DocumentId, change.DocumentId);
            previewChanges++;
        };

        document.SetFaceElements([new FaceLampWindowElement { ObjectId = "lamp", Width = 100, Height = 50 }]);

        Assert.Equal(1, previewChanges);
    }

    private static (DocumentTabViewModel Document, ArtworkCalibrationOperationModel Calibration) CreateCalibrationDocument(
        IReadOnlyList<CalibrationSampleModel>? blackSamples = null)
    {
        var calibration = new ArtworkCalibrationOperationModel
        {
            Id = "calibration",
            BlackReference = new CalibrationReferenceModel { Samples = blackSamples ?? [] }
        };
        var face = new FaceDocumentModel
        {
            BuildState = FaceBuildStateFactory.CreateGeneratedState(true, false, false, true, true),
            Artwork = new FaceArtworkModel
            {
                ProcessingPipeline = new ImageProcessingPipelineModel { Operations = [calibration] }
            }
        };
        return (new DocumentTabViewModel(EditorDocument.CreateFaceStub("Face"),
            faceDocumentJson: FaceDocumentStorage.Serialize(face)), calibration);
    }

    private static ArtworkCalibrationOperationModel CopyWithBlackSamples(
        ArtworkCalibrationOperationModel operation,
        IReadOnlyList<CalibrationSampleModel> samples) => new()
    {
        Id = operation.Id,
        Enabled = operation.Enabled,
        Strength = operation.Strength,
        CorrectSpatialBrightness = operation.CorrectSpatialBrightness,
        CorrectSpatialColor = operation.CorrectSpatialColor,
        NormalizeBlackWhite = operation.NormalizeBlackWhite,
        NeutralizeWhite = operation.NeutralizeWhite,
        BlackReference = new CalibrationReferenceModel
        {
            ManualEnabled = operation.BlackReference.ManualEnabled,
            ManualColor = operation.BlackReference.ManualColor,
            Samples = samples
        },
        WhiteReference = operation.WhiteReference,
        SameColorGroups = operation.SameColorGroups
    };

    private static void AssertArtworkProductsAreStale(DocumentTabViewModel document)
    {
        var state = document.GetFaceDocument().BuildState;
        Assert.Equal(FaceBuildStatus.Current, state.Get(FaceGeneratedProduct.ArtworkCorrectionInput).Status);
        Assert.Equal(FaceBuildStatus.Stale, state.Get(FaceGeneratedProduct.BaseArtwork).Status);
        Assert.Equal(FaceBuildStatus.Stale, state.Get(FaceGeneratedProduct.ArtworkOutput).Status);
        Assert.Equal(FaceBuildStatus.Stale, state.Get(FaceGeneratedProduct.RuntimeAssets).Status);
    }
}
