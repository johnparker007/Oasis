using OasisEditor.Features.CabinetEditor.Models;
using OasisEditor.Features.CabinetEditor.Services;
using Xunit;

namespace OasisEditor.Tests;

public sealed class CabinetReflectionFaceCatalogTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "OasisReflectionCatalogTests", Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("relative-directory-that-does-not-exist")]
    public void Discover_IncompletePath_ReturnsEmpty(string? path)
    {
        Assert.Empty(CabinetReflectionFaceCatalog.Discover(path));
    }

    [Fact]
    public void Discover_MissingFacesDirectory_ReturnsEmpty()
    {
        var assets = Directory.CreateDirectory(Path.Combine(_root, "Assets")).FullName;
        Assert.Empty(CabinetReflectionFaceCatalog.Discover(assets));
    }

    [Fact]
    public void Discover_InvalidPath_ReturnsEmpty()
    {
        Assert.Empty(CabinetReflectionFaceCatalog.Discover("\0invalid"));
    }

    [Fact]
    public void UnsavedCabinetViewer_ConstructsWithoutFaceChoices()
    {
        var document = new DocumentTabViewModel(
            EditorDocument.CreateCabinet3DStub("Unsaved Cabinet"),
            cabinetDocumentJson: CabinetDocumentStorage.Serialize(CabinetDocument.FromModelPath("missing.glb")));

        var viewer = document.CabinetViewer;

        Assert.NotNull(viewer);
        Assert.Empty(viewer!.ReflectionEditor.FaceChoices);
        Assert.Equal("missing.glb", viewer.ModelPath);
        document.Dispose();
    }

    [Fact]
    public void ProjectContextRefresh_PopulatesFacePackageChoice()
    {
        var assets = Directory.CreateDirectory(Path.Combine(_root, "Assets")).FullName;
        var facePackage = Directory.CreateDirectory(Path.Combine(assets, "Faces", "TopGlass")).FullName;
        File.WriteAllText(Path.Combine(facePackage, ProjectAssetPathService.FaceManifestFileName), FaceDocumentStorage.Serialize(new FaceDocumentModel { Id = "face-top", Title = "Top Glass" }));
        var project = new EditorProject { Name = "Test", ProjectDirectory = _root, ProjectFilePath = Path.Combine(_root, "test.oasis"), AssetsDirectory = assets, MachinesDirectory = Path.Combine(_root, "Machines"), GeneratedDirectory = Path.Combine(_root, "Generated") };
        var document = new DocumentTabViewModel(EditorDocument.CreateCabinet3DStub("Cabinet"), cabinetDocumentJson: CabinetDocumentStorage.Serialize(CabinetDocument.FromModelPath("missing.glb")));
        document.SetProjectAccessor(() => project);

        var choice = Assert.Single(document.CabinetViewer!.ReflectionEditor.FaceChoices);
        Assert.Equal("face-top", choice.FaceId);
        Assert.Equal("TopGlass", choice.Label);
        document.Dispose();
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
