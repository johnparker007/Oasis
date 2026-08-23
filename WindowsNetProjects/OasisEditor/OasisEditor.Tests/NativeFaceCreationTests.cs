using OasisEditor.Automation;
using SkiaSharp;
using Xunit;

namespace OasisEditor.Tests;

public sealed class NativeFaceCreationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"OasisNativeFace-{Guid.NewGuid():N}");

    [Fact]
    public void BlankCreation_IsAuthoredUnconfiguredAndOpensAtOverview()
    {
        var result = new FaceDocumentCreationService().CreateFaceDocument(
            new FaceDocumentCreationOptions("  Native Upper Glass  ", 1, FaceStartingArtworkKind.Blank), CreateProject());

        var document = Assert.IsType<DocumentTabViewModel>(result.Document);
        var face = document.GetFaceDocument();
        Assert.Equal(EditorDocumentType.Face, document.Document.DocumentType);
        Assert.Equal("Native Upper Glass", document.Document.Title);
        Assert.Null(face.SourcePanel2DDocumentId); Assert.Null(face.SourcePanel2DDocumentPath); Assert.Null(face.SourceFaceShapeId);
        Assert.Null(face.Artwork);
        Assert.Equal(FaceSubsystemOrigin.Authored, face.Provenance.Components.Origin);
        Assert.Equal(FaceSubsystemOrigin.Authored, face.Provenance.Illumination.Origin);
        Assert.All(face.BuildState.Products.Values, state => Assert.Equal(FaceBuildStatus.NotConfigured, state.Status));
        Assert.Equal(FaceDocumentStorage.DefaultNativeLogicalWidth, face.SourceRegion!.Width);
        Assert.Equal(FaceWorkspaceDestination.Overview, document.FaceWorkspace!.Destination);
        Assert.True(document.Document.IsDirty);
    }

    [Fact]
    public void ImageCreation_ImportsAuthoredSourceAndLeavesArtworkBuildStale()
    {
        var project = CreateProject();
        var source = Path.Combine(_root, "source.png");
        WritePng(source, 40, 25);
        var result = new FaceDocumentCreationService().CreateFaceDocument(
            new FaceDocumentCreationOptions("Photo Face", 1, FaceStartingArtworkKind.Image, source), project);

        var face = Assert.IsType<DocumentTabViewModel>(result.Document).GetFaceDocument();
        Assert.Equal(FaceArtworkSourceKind.Image, face.Artwork!.Source.Kind);
        Assert.Equal(40, face.Artwork.Source.PixelWidth); Assert.Equal(25, face.Artwork.Source.PixelHeight);
        Assert.True(File.Exists(Path.Combine(project.ProjectDirectory, face.Artwork.Source.AssetPath!.Replace('/', Path.DirectorySeparatorChar))));
        Assert.True(face.Artwork.Geometry.PerspectiveRegistration.IsValid());
        Assert.Equal(FaceSubsystemOrigin.Authored, face.Provenance.Artwork.Origin);
        Assert.Equal(FaceSubsystemOrigin.Authored, face.Provenance.Components.Origin);
        Assert.Equal(FaceSubsystemOrigin.Authored, face.Provenance.Illumination.Origin);
        Assert.Equal(FaceBuildStatus.Stale, face.BuildState.Get(FaceGeneratedProduct.BaseArtwork).Status);
        Assert.Equal(FaceBuildStatus.Stale, face.BuildState.Get(FaceGeneratedProduct.ArtworkOutput).Status);
        Assert.Null(face.Artwork.Override);
        Assert.Null(face.SourcePanel2DDocumentId);
    }

    [Fact]
    public void UndecodableImage_ReturnsFailureWithoutCreatingDocumentOrAssetDirectory()
    {
        var project = CreateProject();
        var source = Path.Combine(_root, "bad.png");
        File.WriteAllText(source, "not an image");
        var result = new FaceDocumentCreationService().CreateFaceDocument(
            new FaceDocumentCreationOptions("Broken", 1, FaceStartingArtworkKind.Image, source), project);
        Assert.False(result.Succeeded); Assert.Null(result.Document); Assert.False(Directory.Exists(Path.Combine(project.AssetsDirectory, "Faces", "Broken")));
    }

    [Fact]
    public void AddingImageLater_DoesNotMoveNativeComponentBounds()
    {
        var project = CreateProject();
        var document = Assert.IsType<DocumentTabViewModel>(new FaceDocumentCreationService().CreateFaceDocument(
            new FaceDocumentCreationOptions("Later", 1, FaceStartingArtworkKind.Blank), project).Document);
        document.SetProjectAccessor(() => project);
        var component = FaceComponentFactory.Create(FaceComponentKind.Button, 100, 120, 80, 40);
        document.CommandService.Execute(FaceMutationCommands.CreateAddComponentCommand(document.DocumentId, document, component));
        var before = document.GetFaceElements().Single(element => element.ObjectId == component.ObjectId);
        var source = Path.Combine(_root, "later.png"); WritePng(source, 4000, 2000);
        Assert.True(document.ImportArtworkImage(source, out var error), error);
        var after = document.GetFaceElements().Single(element => element.ObjectId == component.ObjectId);
        Assert.Equal((before.X, before.Y, before.Width, before.Height), (after.X, after.Y, after.Width, after.Height));
        Assert.NotNull(document.GetFaceDocument().Artwork);
    }

    private EditorProject CreateProject()
    {
        Directory.CreateDirectory(_root);
        var assets=Path.Combine(_root,"Assets"); var generated=Path.Combine(_root,"Generated");
        Directory.CreateDirectory(assets); Directory.CreateDirectory(generated);
        return new EditorProject { Name="Test",ProjectFilePath=Path.Combine(_root,"Test.oasisproj"),ProjectDirectory=_root,
            AssetsDirectory=assets,MachinesDirectory=Path.Combine(_root,"Machines"),GeneratedDirectory=generated };
    }
    private static void WritePng(string path,int width,int height)
    { using var bitmap=new SKBitmap(width,height);bitmap.Erase(SKColors.CornflowerBlue);using var image=SKImage.FromBitmap(bitmap);using var data=image.Encode(SKEncodedImageFormat.Png,100);using var stream=File.Create(path);data.SaveTo(stream); }
    public void Dispose(){if(Directory.Exists(_root))Directory.Delete(_root,true);}
}
