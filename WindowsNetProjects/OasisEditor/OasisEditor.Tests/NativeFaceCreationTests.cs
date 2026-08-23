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
        document.SetProjectAccessor(() => CreateProject());
        Assert.True(document.BuildFace().Succeeded);
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
        Assert.Equal(FaceBuildStatus.Stale, face.BuildState.Get(FaceGeneratedProduct.ArtworkCorrectionInput).Status);
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
    public void AddingImageLater_ConfiguresAndBuildsArtworkWithoutMovingNativeComponents()
    {
        var project = CreateProject();
        var document = Assert.IsType<DocumentTabViewModel>(new FaceDocumentCreationService().CreateFaceDocument(
            new FaceDocumentCreationOptions("Later", 1, FaceStartingArtworkKind.Blank), project).Document);
        document.SetProjectAccessor(() => project);
        var component = FaceComponentFactory.Create(FaceComponentKind.Button, 100, 120, 80, 40);
        document.CommandService.Execute(FaceMutationCommands.CreateAddComponentCommand(document.DocumentId, document, component));
        var before = document.GetFaceElements().Single(element => element.ObjectId == component.ObjectId);
        var source = Path.Combine(_root, "later.png"); WritePng(source, 400, 200);
        Assert.True(document.ImportArtworkImage(source, out var error), error);
        var configured = document.GetFaceDocument();
        Assert.Equal(FaceBuildStatus.Stale, configured.BuildState.Get(FaceGeneratedProduct.ArtworkCorrectionInput).Status);
        Assert.Equal(FaceBuildStatus.Stale, configured.BuildState.Get(FaceGeneratedProduct.BaseArtwork).Status);
        Assert.Equal(FaceBuildStatus.Stale, configured.BuildState.Get(FaceGeneratedProduct.ArtworkOutput).Status);
        Assert.Equal(FaceDocumentStorage.DefaultNativeLogicalWidth, configured.SourceRegion!.Width);
        Assert.Equal(FaceDocumentStorage.DefaultNativeLogicalHeight, configured.SourceRegion.Height);
        Assert.Equal((400, 200), (configured.Artwork!.OutputWidth, configured.Artwork.OutputHeight));
        var artworkElement = Assert.IsType<FaceArtworkElement>(configured.Elements.Single(element => element is FaceArtworkElement));
        Assert.Equal((0d, 0d, 1024d, 1024d), (artworkElement.X, artworkElement.Y, artworkElement.Width, artworkElement.Height));

        var build = document.BuildFace();
        Assert.True(build.Succeeded);
        Assert.Contains(FaceGeneratedProduct.ArtworkCorrectionInput, build.Built);
        Assert.Contains(FaceGeneratedProduct.BaseArtwork, build.Built);
        Assert.Contains(FaceGeneratedProduct.ArtworkOutput, build.Built);
        AssertArtworkOutput(document.GetFaceDocument(), project);

        var rebuild = document.BuildFace(force: true);
        Assert.True(rebuild.Succeeded);
        Assert.Contains(FaceGeneratedProduct.ArtworkOutput, rebuild.Built);
        AssertArtworkOutput(document.GetFaceDocument(), project);
        var after = document.GetFaceElements().Single(element => element.ObjectId == component.ObjectId);
        Assert.Equal((before.X, before.Y, before.Width, before.Height), (after.X, after.Y, after.Width, after.Height));
    }

    [Fact]
    public void ImageStartingAndBlankThenImage_HaveEquivalentArtworkConfigurability()
    {
        var project = CreateProject();
        var source = Path.Combine(_root, "equivalent.png"); WritePng(source, 80, 40);
        var direct = Assert.IsType<DocumentTabViewModel>(new FaceDocumentCreationService().CreateFaceDocument(
            new FaceDocumentCreationOptions("Direct", 1, FaceStartingArtworkKind.Image, source), project).Document);
        var later = Assert.IsType<DocumentTabViewModel>(new FaceDocumentCreationService().CreateFaceDocument(
            new FaceDocumentCreationOptions("Later Equivalent", 2, FaceStartingArtworkKind.Blank), project).Document);
        later.SetProjectAccessor(() => project);
        Assert.True(later.ImportArtworkImage(source, out var error), error);
        foreach (var product in new[] { FaceGeneratedProduct.ArtworkCorrectionInput, FaceGeneratedProduct.BaseArtwork, FaceGeneratedProduct.ArtworkOutput })
            Assert.Equal(direct.GetFaceDocument().BuildState.Get(product).Status, later.GetFaceDocument().BuildState.Get(product).Status);
    }

    private static void AssertArtworkOutput(FaceDocumentModel face, EditorProject project)
    {
        Assert.Equal(FaceBuildStatus.Current, face.BuildState.Get(FaceGeneratedProduct.ArtworkCorrectionInput).Status);
        Assert.Equal(FaceBuildStatus.Current, face.BuildState.Get(FaceGeneratedProduct.BaseArtwork).Status);
        Assert.Equal(FaceBuildStatus.Current, face.BuildState.Get(FaceGeneratedProduct.ArtworkOutput).Status);
        foreach (var relative in new[] { face.Artwork!.CorrectionInputAssetPath, face.Artwork.BaseAssetPath, face.Artwork.OutputAssetPath })
            Assert.True(File.Exists(Path.Combine(project.ProjectDirectory, relative!.Replace('/', Path.DirectorySeparatorChar))), relative);
        using var bitmap = SKBitmap.Decode(Path.Combine(project.ProjectDirectory, face.Artwork.OutputAssetPath!.Replace('/', Path.DirectorySeparatorChar)));
        Assert.NotNull(bitmap); Assert.Equal((400, 200), (bitmap.Width, bitmap.Height));
        var pixel = bitmap.GetPixel(bitmap.Width / 2, bitmap.Height / 2);
        Assert.True(pixel.Alpha > 0 && pixel.Blue > 100, $"Unexpected generated pixel {pixel}.");
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
