using OasisEditor;
using SkiaSharp;
using Xunit;

namespace OasisEditor.Tests;

public sealed class FaceArtworkSourceSwitchingTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"oasis-artwork-switch-{Guid.NewGuid():N}");

    public FaceArtworkSourceSwitchingTests() => Directory.CreateDirectory(_root);

    [Fact]
    public void ImageToPanel2D_UndoRedoPreservesImageRecipeAndUnrelatedProvenance()
    {
        var registration = new FacePerspectiveRegistrationModel
        {
            TopLeft = new() { X = .1, Y = .2 }, TopRight = new() { X = .9, Y = .1 },
            BottomRight = new() { X = .8, Y = .9 }, BottomLeft = new() { X = .05, Y = .8 }
        };
        var document = CreateDocument(registration);
        var before = document.GetFaceDocument();

        Assert.True(document.UsePanel2DArtworkSource(out var error), error);
        var panel = document.GetFaceDocument();
        Assert.Equal(FaceArtworkSourceKind.Panel2DFaceSourceShape, panel.Artwork!.Source.Kind);
        Assert.Equal(FaceSubsystemOrigin.Derived, panel.Provenance.Artwork.Origin);
        Assert.Equal(FaceSubsystemOrigin.Derived, panel.Provenance.Components.Origin);
        Assert.Equal("components.panel2d", panel.Provenance.Components.SourceDocumentPath);
        Assert.Equal(FaceSubsystemOrigin.Derived, panel.Provenance.Illumination.Origin);
        Assert.Equal("illumination.panel2d", panel.Provenance.Illumination.SourceDocumentPath);
        Assert.Equal(800, panel.Artwork.OutputWidth);
        Assert.Equal(600, panel.Artwork.OutputHeight);
        Assert.Same(before.Artwork.ProcessingPipeline, panel.Artwork.ProcessingPipeline);
        AssertArtworkStale(panel);

        Assert.True(document.CommandService.TryUndo());
        var undone = document.GetFaceDocument();
        Assert.Equal(FaceArtworkSourceKind.Image, undone.Artwork!.Source.Kind);
        Assert.Equal("Assets/photo-a.jpg", undone.Artwork.Source.AssetPath);
        Assert.Equal(FaceSubsystemOrigin.Authored, undone.Provenance.Artwork.Origin);
        Assert.Equal(.1, undone.Artwork.Geometry.PerspectiveRegistration.TopLeft.X, 6);
        Assert.Equal(.2, undone.Artwork.Geometry.PerspectiveRegistration.TopLeft.Y, 6);
        Assert.Equal(3024, undone.Artwork.OutputWidth);
        Assert.Equal(4032, undone.Artwork.OutputHeight);

        Assert.True(document.CommandService.TryRedo());
        Assert.Equal(FaceArtworkSourceKind.Panel2DFaceSourceShape, document.GetFaceDocument().Artwork!.Source.Kind);
        Assert.Equal(FaceSubsystemOrigin.Derived, document.GetFaceDocument().Provenance.Artwork.Origin);
    }

    [Fact]
    public void SavedImageFace_CanResolveRetainedPanel2DSourceAfterReload()
    {
        var first = CreateDocument(FacePerspectiveRegistrationModel.FullImage);
        var saved = FaceDocumentStorage.Serialize(first.GetFaceDocument());
        var reopened = new DocumentTabViewModel(EditorDocument.CreateFaceStub("Face"), faceDocumentJson: saved);
        reopened.SetProjectAccessor(Project);

        Assert.True(reopened.CanUsePanel2DArtworkSource(out var reason), reason);
        Assert.True(reopened.UsePanel2DArtworkSource(out var error), error);
        Assert.Equal(FaceArtworkSourceKind.Panel2DFaceSourceShape, reopened.GetFaceDocument().Artwork!.Source.Kind);
    }

    [Fact]
    public void BrokenPanel2DBackground_IsValidatedOnlyWhenSwitchIsInvoked()
    {
        var document = CreateDocument(FacePerspectiveRegistrationModel.FullImage);
        File.Delete(Path.Combine(_root, "Assets", "panel-background.png"));

        Assert.True(document.CanUsePanel2DArtworkSource(out var reason), reason);
        Assert.False(document.UsePanel2DArtworkSource(out var useError));
        Assert.Contains("background artwork", useError, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(FaceArtworkSourceKind.Image, document.GetFaceDocument().Artwork!.Source.Kind);
        Assert.False(document.CommandService.TryUndo());
    }

    [Fact]
    public void RepeatedPanelAndImageSwitches_UseNewImageRecipeThenReturnToRetainedPanel()
    {
        var document = CreateDocument(FacePerspectiveRegistrationModel.FullImage);
        Assert.True(document.UsePanel2DArtworkSource(out var firstError), firstError);
        var imageB = Path.Combine(_root, "photo-b.png");
        WritePng(imageB, 1200, 900);

        Assert.True(document.ImportArtworkImage(imageB, out var importError), importError);
        Assert.Equal(FaceArtworkSourceKind.Image, document.GetFaceDocument().Artwork!.Source.Kind);
        Assert.Equal(1200, document.GetFaceDocument().Artwork.OutputWidth);
        Assert.Equal(900, document.GetFaceDocument().Artwork.OutputHeight);
        Assert.True(document.UsePanel2DArtworkSource(out var secondError), secondError);
        Assert.Equal(FaceArtworkSourceKind.Panel2DFaceSourceShape, document.GetFaceDocument().Artwork!.Source.Kind);
        Assert.Equal("shape-1", document.GetFaceDocument().Artwork.Source.FaceSourceShapeId);
        Assert.Equal(800, document.GetFaceDocument().Artwork.OutputWidth);
        Assert.Equal(600, document.GetFaceDocument().Artwork.OutputHeight);
    }

    private DocumentTabViewModel CreateDocument(FacePerspectiveRegistrationModel registration)
    {
        var assets = Path.Combine(_root, "Assets");
        Directory.CreateDirectory(assets);
        var background = Path.Combine(assets, "panel-background.png");
        if (!File.Exists(background)) WritePng(background, 1000, 800);
        var panelPath = Path.Combine(assets, "source.panel2d");
        var backgroundElement = new PanelElementModel
        {
            ObjectId = "background", Name = "Background", Kind = PanelElementKind.Background,
            AssetPath = "Assets/panel-background.png", Width = 1000, Height = 800
        };
        var shape = new PanelFaceSourceShapeModel
        {
            Id = "shape-1", Name = "Face", TopLeft = new() { X = 100, Y = 100 },
            TopRight = new() { X = 900, Y = 100 }, BottomRight = new() { X = 900, Y = 700 },
            BottomLeft = new() { X = 100, Y = 700 }
        };
        File.WriteAllText(panelPath, Panel2DDocumentStorage.Serialize("Source", string.Empty,
            [Panel2DDocumentStorage.ToStorageElement(backgroundElement)],
            [Panel2DDocumentStorage.ToStorageFaceSourceShape(shape)]));
        var state = FaceBuildStateFactory.CreateGeneratedState(true, false, false, false, false);
        var calibration = new ArtworkCalibrationOperationModel
        {
            BlackReference = new CalibrationReferenceModel { Samples = [new CalibrationSampleModel { X = .2, Y = .3 }] }
        };
        var face = new FaceDocumentModel
        {
            Title = "Face", SourcePanel2DDocumentPath = "Assets/source.panel2d", SourcePanel2DDocumentId = "panel-1",
            SourceFaceShapeId = shape.Id,
            Provenance = new FaceProvenanceModel
            {
                Artwork = new() { Origin = FaceSubsystemOrigin.Authored },
                Components = new() { Origin = FaceSubsystemOrigin.Derived, SourceDocumentPath = "components.panel2d" },
                Illumination = new() { Origin = FaceSubsystemOrigin.Derived, SourceDocumentPath = "illumination.panel2d" }
            },
            BuildState = state,
            Artwork = new FaceArtworkModel
            {
                Source = new FaceArtworkSourceModel { Kind = FaceArtworkSourceKind.Image, AssetPath = "Assets/photo-a.jpg", PixelWidth = 3024, PixelHeight = 4032 },
                Geometry = new FaceArtworkGeometryModel { PerspectiveRegistration = registration },
                ProcessingPipeline = new ImageProcessingPipelineModel { Operations = [calibration] },
                CorrectionInputAssetPath = "Generated/correction.png", BaseAssetPath = "Generated/base.png",
                OutputAssetPath = "Generated/output.png", OutputWidth = 3024, OutputHeight = 4032
            }
        };
        var document = new DocumentTabViewModel(EditorDocument.CreateFaceStub("Face"), faceDocumentJson: FaceDocumentStorage.Serialize(face));
        document.SetProjectAccessor(Project);
        return document;
    }

    private EditorProject Project() => new()
    {
        Name = "Test", ProjectDirectory = _root, ProjectFilePath = Path.Combine(_root, "test.oasis"),
        AssetsDirectory = Path.Combine(_root, "Assets"), MachinesDirectory = Path.Combine(_root, "Machines"),
        GeneratedDirectory = Path.Combine(_root, "Generated")
    };

    private static void AssertArtworkStale(FaceDocumentModel face)
    {
        Assert.Equal(FaceBuildStatus.Stale, face.BuildState.Get(FaceGeneratedProduct.ArtworkCorrectionInput).Status);
        Assert.Equal(FaceBuildStatus.Stale, face.BuildState.Get(FaceGeneratedProduct.BaseArtwork).Status);
        Assert.Equal(FaceBuildStatus.Stale, face.BuildState.Get(FaceGeneratedProduct.ArtworkOutput).Status);
    }

    private static void WritePng(string path, int width, int height)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(SKColors.White);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 90);
        using var stream = File.Create(path);
        data.SaveTo(stream);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
