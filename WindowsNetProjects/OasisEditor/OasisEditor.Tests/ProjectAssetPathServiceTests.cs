using OasisEditor;
using Xunit;

namespace OasisEditor.Tests;

public sealed class ProjectAssetPathServiceTests
{
    [Fact]
    public void ResolvesFixedManifestAndFaceAuthoredAndRuntimePaths()
    {
        var root = Path.Combine(Path.GetTempPath(), $"OasisAssetPaths-{Guid.NewGuid():N}");
        var project = new EditorProject
        {
            Name = "Test",
            ProjectFilePath = Path.Combine(root, "Test.oasisproj"),
            ProjectDirectory = root,
            AssetsDirectory = Path.Combine(root, "Assets"),
            MachinesDirectory = Path.Combine(root, "Machines"),
            GeneratedDirectory = Path.Combine(root, "Generated")
        };
        var service = new ProjectAssetPathService();

        Assert.Equal(Path.Combine(root, "Assets", "Panel2D", "Main Panel", "asset.panel2d"), service.GetPanel2DManifestPath(project, "Main Panel"));
        Assert.Equal(Path.Combine(root, "Assets", "Faces", "Top Glass", "asset.face"), service.GetFaceManifestPath(project, "Top Glass"));
        Assert.Equal(Path.Combine(root, "Assets", "Faces", "Top Glass", "artwork.png"), service.GetFaceArtworkPath(project, "Top Glass"));
        Assert.Equal(Path.Combine(root, "Assets", "Faces", "Top Glass", "mask.png"), service.GetFaceMaskPath(project, "Top Glass"));
        Assert.Equal(Path.Combine(root, "Assets", "Cabinet3D", "Vogue", "asset.cabinet3d"), service.GetCabinet3DManifestPath(project, "Vogue"));
        Assert.Equal(Path.Combine(root, "Generated", "Faces", "Top Glass", "runtime"), service.GetFaceRuntimeDirectory(project, "Top Glass"));
    }

    [Fact]
    public void EnsureUniqueAssetNameAddsPredictableSuffix()
    {
        var root = Path.Combine(Path.GetTempPath(), $"OasisAssetPaths-{Guid.NewGuid():N}");
        var project = new EditorProject
        {
            Name = "Test",
            ProjectFilePath = Path.Combine(root, "Test.oasisproj"),
            ProjectDirectory = root,
            AssetsDirectory = Path.Combine(root, "Assets"),
            MachinesDirectory = Path.Combine(root, "Machines"),
            GeneratedDirectory = Path.Combine(root, "Generated")
        };
        var service = new ProjectAssetPathService();
        Directory.CreateDirectory(service.GetAssetPackageDirectory(project, EditorAssetType.Face, "Top Glass"));

        Assert.Equal("Top Glass 2", service.EnsureUniqueAssetName(project, EditorAssetType.Face, "Top Glass"));
    }
}
