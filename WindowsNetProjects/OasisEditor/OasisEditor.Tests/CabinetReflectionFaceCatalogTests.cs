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
    public void UnsavedCabinetViewer_ConstructsWithoutSurfaceTargetChoices()
    {
        var document = new DocumentTabViewModel(
            EditorDocument.CreateCabinet3DStub("Unsaved Cabinet"),
            cabinetDocumentJson: CabinetDocumentStorage.Serialize(CabinetDocument.FromModelPath("missing.glb")));

        var viewer = document.CabinetViewer;

        Assert.NotNull(viewer);
        Assert.Empty(viewer!.ReflectionEditor.SurfaceTargetChoices);
        Assert.Equal("missing.glb", viewer.ModelPath);
        document.Dispose();
    }

    [Fact]
    public void Discover_FindsFacePackagesFromAssetsDirectory()
    {
        var assets = Directory.CreateDirectory(Path.Combine(_root, "Assets")).FullName;
        var facePackage = Directory.CreateDirectory(Path.Combine(assets, "Faces", "TopGlass")).FullName;
        File.WriteAllText(Path.Combine(facePackage, ProjectAssetPathService.FaceManifestFileName), FaceDocumentStorage.Serialize(new FaceDocumentModel { Id = "face-top", Title = "Top Glass" }));
        var unrelatedPackage = Directory.CreateDirectory(Path.Combine(assets, "Faces", "Unused")).FullName;
        File.WriteAllText(Path.Combine(unrelatedPackage, ProjectAssetPathService.FaceManifestFileName), FaceDocumentStorage.Serialize(new FaceDocumentModel { Id = "face-unused", Title = "Unused" }));

        var choices = CabinetReflectionFaceCatalog.Discover(assets);

        Assert.Equal(2, choices.Count);
        Assert.Contains(choices, choice => choice.FaceId == "face-top" && choice.Label == "TopGlass");
        Assert.Contains(choices, choice => choice.FaceId == "face-unused" && choice.Label == "Unused");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
