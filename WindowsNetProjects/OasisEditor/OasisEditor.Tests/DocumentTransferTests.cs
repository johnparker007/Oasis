using OasisEditor;
using Xunit;

namespace OasisEditor.Avalonia.Tests;

public sealed class DocumentTransferTests
{
    [Fact]
    public void TransferNotificationsObserveTheCompletedOwnershipMove()
    {
        using var fixture=new Fixture();
        var source=new PrefabAuthoringSession(fixture.ProjectSession);source.EnableDocumentRetention();
        var destination=new PrefabAuthoringSession(fixture.ProjectSession);destination.EnableDocumentRetention();
        var path=source.Create("Notification Prefab");source.AddNode(source.Document.Root.Id,"Retained child");
        var notifications=0;
        void Changed(){notifications++;Assert.Null(source.FilePath);Assert.Equal(path,destination.FilePath);Assert.Single(destination.Document.Root.Children);}
        source.Changed+=Changed;destination.Changed+=Changed;
        source.MoveDocumentTo(path,destination);
        Assert.Equal(2,notifications);
    }

    [Fact]
    public void DirtyPanelFaceMachinePrefabAndReelMoveFromInactiveTabsWithHistory()
    {
        using var fixture = new Fixture();

        var panels = new AuthoringSession(fixture.Root);
        panels.OpenProject(fixture.ProjectPath);
        panels.EnableDocumentRetention();
        var panel = panels.CreatePanel("Panel");
        var rectangle = panels.AddRectangle();
        var panelOther = panels.CreatePanel("Panel Other");
        var panelsDestination = new AuthoringSession(fixture.Root);
        panelsDestination.OpenProject(fixture.ProjectPath);
        panelsDestination.EnableDocumentRetention();
        panels.MoveDocumentTo(panel, panelsDestination);
        Assert.Equal(panel, panelsDestination.PanelPath);
        Assert.Null(panels.SelectedId);
        Assert.Single(panelsDestination.Elements);
        panelsDestination.Undo();
        Assert.Empty(panelsDestination.Elements);
        panelsDestination.Redo();
        Assert.Equal(rectangle, Assert.Single(panelsDestination.Elements).Id);
        panelsDestination.MoveDocumentTo(panel, panels);
        panels.OpenPanel(panelOther);
        var activeRectangle = panels.AddRectangle();
        panels.MoveDocumentTo(panelOther, panelsDestination);
        Assert.Equal(activeRectangle, Assert.Single(panelsDestination.Elements).Id);

        var faces = new FaceAuthoringSession(fixture.ProjectSession);
        faces.EnableDocumentRetention();
        var face = faces.CreateFace("Face");
        var faceElement = faces.AddReelMount("Reel", 0, 0, 30, 40, 0);
        var faceOther = faces.CreateFace("Face Other");
        var facesDestination = new FaceAuthoringSession(fixture.ProjectSession);
        facesDestination.EnableDocumentRetention();
        faces.MoveDocumentTo(face, facesDestination);
        Assert.Equal(face, facesDestination.FacePath);
        Assert.Single(facesDestination.Elements);
        facesDestination.Undo();
        Assert.Empty(facesDestination.Elements);
        facesDestination.Redo();
        Assert.Equal(faceElement, Assert.Single(facesDestination.Elements).Id);
        facesDestination.MoveDocumentTo(face, faces);
        faces.OpenFace(faceOther);
        var activeFaceElement = faces.AddReelMount("Active Reel", 0, 0, 30, 40, 1);
        faces.MoveDocumentTo(faceOther, facesDestination);
        Assert.Equal(activeFaceElement, Assert.Single(facesDestination.Elements).Id);

        var machines = new MachineAuthoringSession(fixture.ProjectSession);
        machines.EnableDocumentRetention();
        var machine = machines.CreateMachine("Machine");
        machines.SetDisplayName("Unsaved Machine");
        var machineOther = machines.CreateMachine("Machine Other");
        var machinesDestination = new MachineAuthoringSession(fixture.ProjectSession);
        machinesDestination.EnableDocumentRetention();
        machines.MoveDocumentTo(machine, machinesDestination);
        Assert.Equal(machine, machinesDestination.MachinePath);
        Assert.Equal("Unsaved Machine", machinesDestination.MachineTitle);
        machinesDestination.Undo();
        Assert.Equal("Machine", machinesDestination.MachineTitle);
        machinesDestination.Redo();
        Assert.Equal("Unsaved Machine", machinesDestination.MachineTitle);
        machinesDestination.MoveDocumentTo(machine, machines);
        machines.OpenMachine(machineOther);
        machines.SetDisplayName("Unsaved Other Machine");
        machines.MoveDocumentTo(machineOther, machinesDestination);
        Assert.Equal("Unsaved Other Machine", machinesDestination.MachineTitle);

        var prefabs = new PrefabAuthoringSession(fixture.ProjectSession);
        prefabs.EnableDocumentRetention();
        var prefab = prefabs.Create("Prefab");
        var node = prefabs.AddNode(prefabs.Document.Root.Id, "Unsaved node");
        var prefabOther = prefabs.Create("Prefab Other");
        var prefabsDestination = new PrefabAuthoringSession(fixture.ProjectSession);
        prefabsDestination.EnableDocumentRetention();
        prefabs.MoveDocumentTo(prefab, prefabsDestination);
        Assert.Equal(prefab, prefabsDestination.FilePath);
        Assert.Equal(node, Assert.Single(prefabsDestination.Document.Root.Children).Id);
        prefabsDestination.Undo();
        Assert.Empty(prefabsDestination.Document.Root.Children);
        prefabsDestination.Redo();
        Assert.Equal(node, Assert.Single(prefabsDestination.Document.Root.Children).Id);
        prefabsDestination.MoveDocumentTo(prefab, prefabs);
        prefabs.Open(prefabOther);
        var activeNode = prefabs.AddNode(prefabs.Document.Root.Id, "Active unsaved node");
        prefabs.MoveDocumentTo(prefabOther, prefabsDestination);
        Assert.Equal(activeNode, Assert.Single(prefabsDestination.Document.Root.Children).Id);

        var reels = new ReelAuthoringSession(fixture.ProjectSession);
        reels.EnableDocumentRetention();
        var reel = reels.Create("Reel");
        reels.Update(displayName: "Unsaved Reel");
        var reelOther = reels.Create("Reel Other");
        var reelsDestination = new ReelAuthoringSession(fixture.ProjectSession);
        reelsDestination.EnableDocumentRetention();
        reels.MoveDocumentTo(reel, reelsDestination);
        Assert.Equal(reel, reelsDestination.FilePath);
        Assert.Equal("Unsaved Reel", reelsDestination.Document.DisplayName);
        reelsDestination.Undo();
        Assert.Equal("Reel", reelsDestination.Document.DisplayName);
        reelsDestination.Redo();
        Assert.Equal("Unsaved Reel", reelsDestination.Document.DisplayName);
        File.Delete(reel);
        Assert.Throws<IOException>(reelsDestination.Save);
        reelsDestination.MoveDocumentTo(reel, reels);
        reels.Open(reelOther);
        reels.Update(displayName: "Unsaved Other Reel");
        reels.MoveDocumentTo(reelOther, reelsDestination);
        Assert.Equal("Unsaved Other Reel", reelsDestination.Document.DisplayName);
    }

    [Fact]
    public void ActiveTransfersPreserveConflictGuardsAndRejectInvalidMovesWithoutMutation()
    {
        using var fixture = new Fixture();
        var source = new ReelAuthoringSession(fixture.ProjectSession);
        var path = source.Create("Active Reel");
        source.Update(displayName: "Unsaved Reel");
        source.EnableDocumentRetention();
        var destination = new ReelAuthoringSession(fixture.ProjectSession);
        destination.EnableDocumentRetention();

        Assert.Throws<KeyNotFoundException>(() => source.MoveDocumentTo(Path.Combine(fixture.Assets, "Reels", "Missing", "asset.reel"), destination));
        Assert.Equal(path, source.FilePath);
        Assert.Equal("Unsaved Reel", source.Document.DisplayName);

        var otherProject = fixture.CreateOtherProject();
        var otherProjectSession = new AuthoringSession(fixture.Root);
        otherProjectSession.OpenProject(otherProject);
        var crossProjectDestination = new ReelAuthoringSession(otherProjectSession);
        crossProjectDestination.EnableDocumentRetention();
        Assert.Throws<InvalidOperationException>(() => source.MoveDocumentTo(path, crossProjectDestination));
        Assert.Equal(path, source.FilePath);

        source.MoveDocumentTo(path, destination);
        Assert.Null(source.FilePath);
        Assert.Equal(path, destination.FilePath);
        File.AppendAllText(path, " ");
        Assert.Throws<IOException>(destination.Save);

        var duplicateSource = new ReelAuthoringSession(fixture.ProjectSession);
        duplicateSource.EnableDocumentRetention();
        duplicateSource.Open(path);
        Assert.Throws<InvalidOperationException>(() => duplicateSource.MoveDocumentTo(path, destination));
        Assert.Equal(path, duplicateSource.FilePath);
        Assert.Equal(path, destination.FilePath);
    }

    [Fact]
    public void LibraryReelTransferKeepsLibraryBindingAndSurvivesARoundTrip()
    {
        using var fixture = new Fixture();
        var libraryRoot = Path.Combine(fixture.Root, "Library");
        var relativePath = Path.Combine("Reels", "Shared", ProjectAssetPathService.ReelManifestFileName);
        var manifest = Path.Combine(libraryRoot, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(manifest)!);
        File.WriteAllText(manifest, ReelDocumentStorage.Serialize(ReelDocument.Create("Shared Reel")));

        var source = new ReelAuthoringSession(fixture.ProjectSession);
        var destination = new ReelAuthoringSession(fixture.ProjectSession);
        source.EnableDocumentRetention();
        destination.EnableDocumentRetention();
        source.OpenLibrary(libraryRoot, relativePath);
        source.Update(displayName: "Unsaved Shared Reel");
        source.MoveDocumentTo(manifest, destination);
        Assert.Equal(Path.GetFullPath(libraryRoot), destination.LibraryRoot);
        Assert.Equal("Unsaved Shared Reel", destination.Document.DisplayName);
        destination.Undo();
        Assert.Equal("Shared Reel", destination.Document.DisplayName);
        destination.Redo();

        destination.MoveDocumentTo(manifest, source);
        Assert.Equal(Path.GetFullPath(libraryRoot), source.LibraryRoot);
        Assert.Equal("Unsaved Shared Reel", source.Document.DisplayName);
    }

    [Fact]
    public void PanelClipboardIsSharedInBothDirectionsOnlyForSameProjectSessions()
    {
        using var fixture = new Fixture();
        var source = new AuthoringSession(fixture.Root);
        source.OpenProject(fixture.ProjectPath);
        source.EnableDocumentRetention();
        var panelPath = source.CreatePanel("Floating Panel");
        source.AddRectangle();
        Assert.True(source.CopySelectedToClipboard());

        var destination = new AuthoringSession(fixture.Root);
        destination.OpenProject(fixture.ProjectPath);
        destination.EnableDocumentRetention();
        source.MoveDocumentTo(panelPath, destination);
        var floatingPaste = Assert.Single(destination.PasteClipboard());
        Assert.Equal(2,destination.Elements.Count);
        Assert.True(destination.CopySelectedToClipboard());

        var rootPanelPath = source.CreatePanel("Root Panel");
        var rootPaste = Assert.Single(source.PasteClipboard());
        Assert.Equal(rootPaste, Assert.Single(source.Elements).Id);
        Assert.NotEqual(floatingPaste, rootPaste);

        var otherProject = fixture.CreateOtherProject();
        var differentProject = new AuthoringSession(fixture.Root);
        differentProject.OpenProject(otherProject);
        differentProject.EnableDocumentRetention();
        Assert.Throws<InvalidOperationException>(() => source.MoveDocumentTo(rootPanelPath, differentProject));
        Assert.False(differentProject.CanPasteClipboard);
    }

    [Fact]
    public void RecoveredPanelInputsFollowOwnershipInBothDirections()
    {
        using var fixture = new Fixture();
        var source = new AuthoringSession(fixture.Root);
        source.OpenProject(fixture.ProjectPath);
        source.EnableDocumentRetention();
        var path = source.CreatePanel("Imported Panel");
        // Recovered inputs now belong to persisted control groups, rather than
        // the retired private cache this fixture originally injected into.
        PanelControlTests.InstallControl(source, 1);
        source.EnableDocumentRetention();
        var recovered = Assert.Single(source.ImportedInputDefinitions);

        var destination = new AuthoringSession(fixture.Root);
        destination.OpenProject(fixture.ProjectPath);
        destination.EnableDocumentRetention();
        source.MoveDocumentTo(path, destination);
        Assert.Equal(recovered.Id, Assert.Single(destination.ImportedInputDefinitions).Id);
        Assert.Equal(recovered.ButtonNumber, Assert.Single(destination.ImportedInputDefinitions).ButtonNumber);
        Assert.Empty(source.ImportedInputDefinitions);

        destination.MoveDocumentTo(path, source);
        Assert.Equal(recovered.Id, Assert.Single(source.ImportedInputDefinitions).Id);
        Assert.Equal(recovered.ButtonNumber, Assert.Single(source.ImportedInputDefinitions).ButtonNumber);
        Assert.Empty(destination.ImportedInputDefinitions);
    }

    private sealed class Fixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "OasisDocumentTransfer-" + Guid.NewGuid().ToString("N"));
        public string ProjectPath => Path.Combine(Root, "Project", "Project.oasisproj");
        public string Assets => Path.Combine(Root, "Project", "Assets");
        public AuthoringSession ProjectSession { get; }

        public Fixture()
        {
            Directory.CreateDirectory(Assets);
            Directory.CreateDirectory(Path.Combine(Root, "Project", "Generated"));
            File.WriteAllText(ProjectPath, """{"name":"Project","version":9,"layout":{"assets":"Assets","generated":"Generated"}}""");
            ProjectSession = new AuthoringSession(Root);
            ProjectSession.OpenProject(ProjectPath);
        }

        public string CreateOtherProject()
        {
            var projectRoot = Path.Combine(Root, "Other");
            Directory.CreateDirectory(Path.Combine(projectRoot, "Assets"));
            var path = Path.Combine(projectRoot, "Other.oasisproj");
            File.WriteAllText(path, """{"name":"Other","version":9,"layout":{"assets":"Assets","generated":"Generated"}}""");
            return path;
        }

        public void Dispose() { try { Directory.Delete(Root, recursive: true); } catch { } }
    }
}
