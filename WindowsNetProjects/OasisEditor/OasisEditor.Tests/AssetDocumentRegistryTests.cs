using OasisEditor;
using Xunit;

namespace OasisEditor.Avalonia.Tests;

public sealed class AssetDocumentRegistryTests
{
    [Fact]
    public void OpenReusesCanonicalDocumentAndIdsSurviveProjectRelocation()
    {
        using var fixture = new ProjectFixture();
        var first = fixture.CreateSessions();
        var path = first.Project.CreatePanel("Panel");
        var registry = new AssetDocumentRegistry();

        var opened = registry.Open(fixture.Root, fixture.ProjectFile, "Panel2D", path);
        var again = registry.Open(fixture.Root, fixture.ProjectFile, "Panel", path);

        Assert.Same(opened, again);
        Assert.Single(registry.Documents);

        var movedRoot = Path.Combine(fixture.Root, "Relocated");
        Directory.CreateDirectory(movedRoot);
        var movedProjectFile = Path.Combine(movedRoot, Path.GetFileName(fixture.ProjectFile));
        File.Copy(fixture.ProjectFile, movedProjectFile);
        var sourceAsset = Path.GetDirectoryName(path)!;
        var relativeAsset = Path.GetRelativePath(fixture.ProjectRoot, sourceAsset);
        var movedAsset = Path.Combine(movedRoot, relativeAsset);
        Directory.CreateDirectory(Path.GetDirectoryName(movedAsset)!);
        CopyDirectory(sourceAsset, movedAsset);

        var moved = WorkspaceAssetDocument.Open(fixture.Root, movedProjectFile, "Panel2D", Path.Combine(movedAsset, Path.GetFileName(path)));
        Assert.Equal(opened.Id, moved.Id);
    }

    [Fact]
    public void AdoptPreservesDirtyContentsAndUndoHistory()
    {
        using var fixture = new ProjectFixture();
        var source = fixture.CreateSessions();
        var path = source.Project.CreatePanel("Panel");
        var elementId = source.Project.AddRectangle();
        source.Project.UpdateElement(elementId, name: "Unsaved name");

        var adopted = WorkspaceAssetDocument.Adopt("Panel2D", path, null, source);

        Assert.Equal("Panel2D", adopted.Kind);
        Assert.Equal(path, adopted.Path);
        Assert.True(adopted.IsDirty);
        Assert.Equal("Unsaved name", Assert.Single(adopted.Sessions.Project.Elements).Name);
        adopted.Undo();
        Assert.Equal("Rectangle", Assert.Single(adopted.Sessions.Project.Elements).Name);
        Assert.True(adopted.IsDirty);
        Assert.Null(source.Project.PanelPath);
    }

    [Fact]
    public void RegistryRefusesDirtyRemovalAndRemovesCleanDocument()
    {
        using var fixture = new ProjectFixture();
        var source = fixture.CreateSessions();
        var path = source.Project.CreatePanel("Panel");
        var registry = new AssetDocumentRegistry();
        var document = registry.Open(fixture.Root, fixture.ProjectFile, "Panel2D", path);

        document.Sessions.Project.AddRectangle();
        Assert.Throws<InvalidOperationException>(() => registry.RemoveClean("Panel2D", path));
        Assert.Single(registry.Documents);

        document.Undo();
        var removed = registry.RemoveClean("Panel2D", path);
        Assert.Same(document, removed);
        Assert.Empty(registry.Documents);
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source))
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
        foreach (var directory in Directory.EnumerateDirectories(source))
            CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
    }

    private sealed class ProjectFixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "OasisAssetRegistry-" + Guid.NewGuid().ToString("N"));
        public string ProjectFile { get; }
        public string ProjectRoot => Path.GetDirectoryName(ProjectFile)!;

        public ProjectFixture()
        {
            Directory.CreateDirectory(Root);
            var setup = new AuthoringSession(Root);
            ProjectFile = setup.CreateProject("Registry");
        }

        public AssetEditorSessions CreateSessions() => new(Root, ProjectFile);

        public void Dispose()
        {
            if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
        }
    }
}
