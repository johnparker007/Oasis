using System.Text.Json;
using OasisEditor;
using Xunit;

namespace OasisEditor.Avalonia.Tests;

public sealed class ProjectWorkflowTests : IDisposable
{
    private readonly string _workspace = Path.Combine(Path.GetTempPath(), "OasisProjectWorkflowTests", Guid.NewGuid().ToString("N"));

    public ProjectWorkflowTests() => Directory.CreateDirectory(_workspace);

    [Fact]
    public void ConcurrentScaffoldingCannotOverwriteAnotherCreatorsProject()
    {
        var created = new System.Collections.Concurrent.ConcurrentBag<string>();
        Parallel.For(0, 6, _ =>
        {
            try { created.Add(new ProjectScaffolder().CreateProject("Shared", _workspace)); }
            catch (InvalidOperationException) { }
        });
        var project = Assert.Single(created);
        Assert.True(File.Exists(Path.Combine(project, "Shared.oasisproj")));
        Assert.True(MachineDocumentStorage.TryRead(File.ReadAllText(Path.Combine(project, "Assets", "Machines", "Shared", "asset.machine")), out _, out _));
        Assert.DoesNotContain(Directory.EnumerateDirectories(_workspace), path => Path.GetFileName(path).StartsWith(".oasis-project-", StringComparison.Ordinal));
    }

    [Fact]
    public void CreateProject_CreatesStandardSchema9ProjectAndCurrentDefaultMachine()
    {
        var session = new AuthoringSession(_workspace);
        var projectFile = session.CreateProject("New Project");
        var projectDirectory = Path.GetDirectoryName(projectFile)!;

        Assert.Equal(Path.Combine(projectDirectory, "New Project.oasisproj"), projectFile);
        Assert.Equal("New Project", session.ProjectName);
        using var project = JsonDocument.Parse(File.ReadAllText(projectFile));
        Assert.Equal(9, project.RootElement.GetProperty("version").GetInt32());
        Assert.Equal("New Project", project.RootElement.GetProperty("name").GetString());
        Assert.Equal("Assets", project.RootElement.GetProperty("layout").GetProperty("assets").GetString());
        Assert.Equal("Generated", project.RootElement.GetProperty("layout").GetProperty("generated").GetString());

        foreach (var path in new[]
        {
            "Assets", "Assets/Panel2D", "Assets/Cabinet3D", "Assets/Faces", "Assets/Machines", "Assets/Reels",
            "Generated", "Generated/Build", "Generated/Preview"
        }) Assert.True(Directory.Exists(Path.Combine(projectDirectory, path.Replace('/', Path.DirectorySeparatorChar))), path);

        var machinePath = Path.Combine(projectDirectory, "Assets", "Machines", "New Project", ProjectAssetPathService.MachineManifestFileName);
        Assert.True(MachineDocumentStorage.TryRead(File.ReadAllText(machinePath), out var machine, out var error), error);
        Assert.Equal(MachineDocument.CurrentSchemaVersion, machine.SchemaVersion);
        Assert.Equal("New Project", machine.DisplayName);
        Assert.Empty(machine.SurfaceAssignments);
        Assert.Empty(machine.ReelAssignments);
        Assert.Empty(machine.ObjectInstances);
        Assert.Empty(machine.InputDefinitions);
        Assert.Empty(machine.Anchors);
        Assert.Equal("Emulation", machine.Runtime.Kind);
        Assert.Equal("Buttons", machine.Interaction.Kind);
    }

    [Fact]
    public void CreateProject_RefusesExistingNamesTraversalAndDirtyReplacement()
    {
        var session = new AuthoringSession(_workspace);
        session.CreateProject("Existing");
        Assert.Throws<InvalidOperationException>(() => session.CreateProject("Existing"));
        Assert.Throws<ArgumentException>(() => session.CreateProject("../outside"));

        session.CreatePanel("Panel");
        session.AddRectangle();
        Assert.True(session.IsDirty);
        Assert.Throws<InvalidOperationException>(() => session.CreateProject("Another"));
        Assert.False(Directory.Exists(Path.Combine(_workspace, "Another")));
    }

    [Fact]
    public void ListAssets_ListsOnlyStandardPackageManifestsAcrossEveryAssetFamily()
    {
        var session = new AuthoringSession(_workspace);
        session.CreateProject("Assets Project");
        var packageData = new (string Type, string Folder, string Manifest)[]
        {
            ("Panel2D", "Panel2D", ProjectAssetPathService.Panel2DManifestFileName),
            ("Face", "Faces", ProjectAssetPathService.FaceManifestFileName),
            ("Cabinet3D", "Cabinet3D", ProjectAssetPathService.Cabinet3DManifestFileName),
            ("Machine", "Machines", ProjectAssetPathService.MachineManifestFileName),
            ("Reel", "Reels", ProjectAssetPathService.ReelManifestFileName),
            ("Object3D", "Object3D", ProjectAssetPathService.Object3DManifestFileName)
        };
        var root = Path.GetDirectoryName(session.ProjectFilePath)!;
        foreach (var (type, folder, manifest) in packageData)
        {
            var package = Path.Combine(root, "Assets", folder, "Sample " + type);
            Directory.CreateDirectory(package);
            File.WriteAllText(Path.Combine(package, manifest), "{}");
        }
        var nested = Path.Combine(root, "Assets", "Panel2D", "Nested", "NotAPackage");
        Directory.CreateDirectory(nested);
        File.WriteAllText(Path.Combine(nested, ProjectAssetPathService.Panel2DManifestFileName), "{}");
        File.WriteAllText(Path.Combine(root, "Assets", "unrelated.txt"), "ignore");

        var assets = session.ListAssets();
        Assert.Equal(packageData.Length + 1, assets.Count);
        foreach (var (type, folder, manifest) in packageData)
        {
            var asset = Assert.Single(assets.Where(item => item.Type == type && item.Name == "Sample " + type));
            Assert.Equal($"Assets/{folder}/Sample {type}/{manifest}", asset.ManifestPath);
        }
        Assert.DoesNotContain(assets, asset => asset.ManifestPath.Contains("NotAPackage", StringComparison.Ordinal));
        Assert.Contains(assets, asset => asset.Type == "Machine" && asset.Name == "Assets Project");
    }

    public void Dispose() => Directory.Delete(_workspace, recursive: true);
}
