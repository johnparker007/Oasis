using OasisEditor;
using OasisEditor.Authoring.Assets;
using Xunit;

namespace OasisEditor.Avalonia.Tests;

public sealed class ProjectAssetPackageServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "OasisPackageContext_" + Guid.NewGuid().ToString("N"));

    public ProjectAssetPackageServiceTests() => Directory.CreateDirectory(_root);
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }

    [Fact]
    public void RenameMovesWholePackageAndPreservesManifestContentAndDependencies()
    {
        var project = CreateProject();
        var package = Path.Combine(project.AssetsDirectory, "Reels", "Steel");
        Directory.CreateDirectory(package);
        var reel = ReelDocument.Create("Steel") with { Id = "17eef64a-06fd-4cff-a10b-d186aef53e87" };
        var manifest = Path.Combine(package, ProjectAssetPathService.ReelManifestFileName);
        var content = ReelDocumentStorage.Serialize(reel);
        File.WriteAllText(manifest, content);
        Directory.CreateDirectory(Path.Combine(package, "Artwork"));
        File.WriteAllText(Path.Combine(package, "Artwork", "note.txt"), "preserve me");

        var result = ProjectAssetPackageService.Rename(project, manifest, "Steel Mk II");

        Assert.True(result.Success, result.Error);
        Assert.Equal(Path.Combine(project.AssetsDirectory, "Reels", "Steel Mk II", ProjectAssetPathService.ReelManifestFileName), result.ManifestPath);
        Assert.Equal(content, File.ReadAllText(result.ManifestPath!));
        Assert.Equal("preserve me", File.ReadAllText(Path.Combine(project.AssetsDirectory, "Reels", "Steel Mk II", "Artwork", "note.txt")));
        Assert.False(Directory.Exists(package));
    }

    [Fact]
    public void RenameRefusesReferencedPackageAndDoesNotChangeIt()
    {
        var project = CreateReel(projectName: "Referenced");
        Directory.CreateDirectory(Path.Combine(project.Project.ProjectDirectory, "Assets", "Machines"));
        File.WriteAllText(Path.Combine(project.Project.ProjectDirectory, "Assets", "Machines", "machine.json"), "{\"asset\":\"Assets/Reels/Steel/asset.reel\"}");
        var result = ProjectAssetPackageService.Rename(project.Project, project.Manifest, "New Steel");
        Assert.False(result.Success);
        Assert.Contains("referenced", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(project.Manifest));
    }

    [Fact]
    public void RenameRefusesPackageWithProjectRelativeDependencyInsideItsOwnManifest()
    {
        var project = CreateProject();
        var package = Path.Combine(project.AssetsDirectory, "Panel2D", "Panel");
        Directory.CreateDirectory(package);
        var manifest = Path.Combine(package, ProjectAssetPathService.Panel2DManifestFileName);
        File.WriteAllText(manifest, "{\"assetPath\":\"Assets\\\\Panel2D\\\\Panel\\\\Artwork\\\\image.png\"}");
        var result = ProjectAssetPackageService.Rename(project, manifest, "Renamed");
        Assert.False(result.Success);
        Assert.Contains("referenced", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(manifest));
    }

    [Fact]
    public void DeleteMovesPackageToUniqueGeneratedTrashAndNeverRemovesIt()
    {
        var created = CreateReel("Unused");
        var result = ProjectAssetPackageService.MoveToTrash(created.Project, created.Manifest);
        Assert.True(result.Success, result.Error);
        Assert.Null(result.ManifestPath);
        Assert.StartsWith(Path.Combine(created.Project.GeneratedDirectory, "Trash"), result.RecoveryPath, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(Path.Combine(result.RecoveryPath!, ProjectAssetPathService.ReelManifestFileName)));
        Assert.False(Directory.Exists(Path.GetDirectoryName(created.Manifest)));
    }

    [Fact]
    public void RenameRejectsPackageChangedAfterSelection()
    {
        var created = CreateReel("Changed");
        var fingerprint = ProjectAssetPackageService.GetFingerprint(created.Project, created.Manifest);
        File.AppendAllText(created.Manifest, " ");
        var result = ProjectAssetPackageService.Rename(created.Project, created.Manifest, "Renamed", fingerprint);
        Assert.False(result.Success);
        Assert.Contains("changed after it was selected", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(created.Manifest));
    }

    [Fact]
    public void DeleteRefusesPackageReferencedByMachineText()
    {
        var created = CreateReel("Referenced Delete");
        Directory.CreateDirectory(Path.Combine(created.Project.ProjectDirectory, "Machines"));
        File.WriteAllText(Path.Combine(created.Project.ProjectDirectory, "Machines", "machine.json"), "Assets/Reels/Steel/asset.reel");
        var result = ProjectAssetPackageService.MoveToTrash(created.Project, created.Manifest);
        Assert.False(result.Success);
        Assert.True(File.Exists(created.Manifest));
        Assert.False(Directory.Exists(Path.Combine(created.Project.GeneratedDirectory, "Trash")));
    }

    [Fact]
    public void DeleteAllowsFacePackageWhoseOwnManifestReferencesItsArtworkAndMask()
    {
        var project = CreateProject();
        var package = Path.Combine(project.AssetsDirectory, "Faces", "Front");
        Directory.CreateDirectory(Path.Combine(package, "ArtworkSource"));
        var manifest = Path.Combine(package, ProjectAssetPathService.FaceManifestFileName);
        File.WriteAllText(manifest, "{\"Artwork\":{\"Source\":{\"AssetPath\":\"Assets/Faces/Front/ArtworkSource/front.png\"}},\"MaskLayer\":{\"AssetPath\":\"Assets/Faces/Front/mask.png\"}}");
        File.WriteAllText(Path.Combine(package, "ArtworkSource", "front.png"), "disposable fixture");
        File.WriteAllText(Path.Combine(package, "mask.png"), "disposable fixture");

        var result = ProjectAssetPackageService.MoveToTrash(project, manifest);

        Assert.True(result.Success, result.Error);
        Assert.True(File.Exists(Path.Combine(result.RecoveryPath!, ProjectAssetPathService.FaceManifestFileName)));
        Assert.False(Directory.Exists(package));
    }

    [Fact]
    public void TrashIsExcludedFromProjectReferenceChecks()
    {
        var project = CreateProject();
        var reels = Path.Combine(project.AssetsDirectory, "Reels");
        var first = Path.Combine(reels, "First");
        var second = Path.Combine(reels, "Second");
        Directory.CreateDirectory(first);
        Directory.CreateDirectory(second);
        var firstManifest = Path.Combine(first, ProjectAssetPathService.ReelManifestFileName);
        var secondManifest = Path.Combine(second, ProjectAssetPathService.ReelManifestFileName);
        File.WriteAllText(firstManifest, ReelDocumentStorage.Serialize(ReelDocument.Create("First")));
        File.WriteAllText(secondManifest, ReelDocumentStorage.Serialize(ReelDocument.Create("Second")));
        File.WriteAllText(Path.Combine(first, "note.txt"), "Assets/Reels/Second/asset.reel");

        Assert.True(ProjectAssetPackageService.MoveToTrash(project, firstManifest).Success);
        var rename = ProjectAssetPackageService.Rename(project, secondManifest, "Second Renamed");

        Assert.True(rename.Success, rename.Error);
        Assert.True(File.Exists(rename.ManifestPath!));
    }

    private EditorProject CreateProject()
    {
        var project = Path.Combine(_root, "Project");
        Directory.CreateDirectory(Path.Combine(project, "Assets"));
        return new EditorProject
        {
            Name = "Project", ProjectDirectory = project, ProjectFilePath = Path.Combine(project, "Project.oasisproject"),
            AssetsDirectory = Path.Combine(project, "Assets"), GeneratedDirectory = Path.Combine(project, "Generated")
        };
    }

    private (EditorProject Project, string Manifest) CreateReel(string projectName)
    {
        var project = CreateProject();
        var package = Path.Combine(project.AssetsDirectory, "Reels", "Steel");
        Directory.CreateDirectory(package);
        var manifest = Path.Combine(package, ProjectAssetPathService.ReelManifestFileName);
        File.WriteAllText(manifest, ReelDocumentStorage.Serialize(ReelDocument.Create(projectName)));
        return (project, manifest);
    }
}
