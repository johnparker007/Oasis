using Avalonia;
using Avalonia.Styling;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm.Controls;
using Xunit;

namespace OasisEditor.Avalonia.Tests;

public sealed class WorkspacePersistenceTests
{
    [Fact]
    public async Task ThreeDSceneAssetsCameraVisibilityAndSelectionRestore()
    {
        await HeadlessTestHost.RunAsync(async () =>
        {
            var folder = Path.Combine(Path.GetTempPath(), "Oasis3DWorkspace-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                var session = new AuthoringSession(folder);
                session.CreateProject("Project");
                var source = Path.Combine(Path.GetDirectoryName(session.ProjectFilePath!)!, "Assets", "Source.glb");
                File.WriteAllBytes(source, CreateMinimalGlb());
                using var capture = new WorkspaceCapture(session);
                var view = capture.View;
                view.ConfigureWorkspacePersistence(Path.Combine(folder, "workspace.json"), restore: false);

                var cabinetPath = view.Cabinets.Create("Cabinet", "Assets/Source.glb");
                view.Cabinets.Save();
                await view.OpenCabinet3DAsync(cabinetPath);
                var cabinetCamera = new OasisEditor.Avalonia.Rendering3D.SceneCameraState(new(1, 2, 3), 8, 1.2f, .4f, 55);
                view.SceneView.SetCamera(cabinetCamera);
                view.SceneView.ShowVisuals = false; view.SceneView.ShowColliders = true; view.SceneView.ShowTriggers = true;
                var savedCabinet = view.InspectWorkspaceState();
                var savedCabinetDocument = Assert.Single(Assert.IsType<AssetWorkspaceLayout>(savedCabinet.AssetLayout).Documents);
                Assert.Equal("Cabinet3D", savedCabinetDocument.Scene?.Mode);
                Assert.Equal(Path.GetRelativePath(Path.GetDirectoryName(session.ProjectFilePath!)!, cabinetPath).Replace('\\', '/'), savedCabinetDocument.Scene?.Cabinet);

                view.SaveWorkspace();
                view.RestoreWorkspace();
                await view.WaitForSceneAsync();
                Assert.Equal("Cabinet3D", view.SceneKind);
                Assert.Equal(cabinetCamera, view.SceneView.Camera);
                Assert.False(view.SceneView.ShowVisuals); Assert.True(view.SceneView.ShowColliders); Assert.True(view.SceneView.ShowTriggers);

                var prefabPath = view.Prefabs.Create("Prefab");
                var child = view.Prefabs.AddNode(view.Prefabs.Document.Root.Id, "Screen");
                view.Prefabs.Save();
                await view.OpenPrefab3DAsync(prefabPath);
                view.SelectScene3DNode(child);
                var prefabCamera = new OasisEditor.Avalonia.Rendering3D.SceneCameraState(new(-1, 4, 2), 3.5f, -.2f, .6f, 72);
                view.SceneView.SetCamera(prefabCamera);
                var savedPrefab = view.InspectWorkspaceState();
                var savedPrefabLayout = Assert.IsType<AssetWorkspaceLayout>(savedPrefab.AssetLayout);
                var savedPrefabDocument = Assert.Single(savedPrefabLayout.Documents.Where(document => document.Kind == "Prefab"));
                Assert.Equal("Prefab", savedPrefabDocument.Scene?.Mode);
                Assert.Equal(Path.GetRelativePath(Path.GetDirectoryName(session.ProjectFilePath!)!, prefabPath).Replace('\\', '/'), savedPrefabDocument.Scene?.Prefab);

                view.SaveWorkspace();
                view.RestoreWorkspace();
                await view.WaitForSceneAsync();
                Assert.Equal("Prefab", view.SceneKind);
                Assert.Equal(child, Assert.Single(Assert.IsType<AssetWorkspaceLayout>(view.InspectWorkspaceState().AssetLayout).Documents.Where(document => document.Kind == "Prefab")).Scene?.Selection);
                Assert.Equal(prefabCamera, view.SceneView.Camera);
                Assert.Equal("Screen", view.Prefabs.Document.Root.Children.Single().Name);

                var activePath = view.Prefabs.FilePath;
                var invalidPrefab = savedPrefab with
                {
                    AssetLayout = savedPrefabLayout with
                    {
                        Documents = savedPrefabLayout.Documents.Select(document => document.Id == savedPrefabDocument.Id
                            ? savedPrefabDocument with { Scene = savedPrefabDocument.Scene! with { Prefab = "../outside/asset.object3d" } }
                            : document).ToArray()
                    }
                };
                Assert.Throws<InvalidDataException>(() => view.ApplyWorkspaceState(invalidPrefab));
                Assert.Equal(activePath, view.Prefabs.FilePath);
                Assert.Equal("Prefab", view.SceneKind);
            }
            finally { Directory.Delete(folder, recursive: true); }
        });
    }

    [Fact]
    public async Task MachineAndNativeLibrarySettingsRestoreWithoutStartingPreview()
    {
        await HeadlessTestHost.RunAsync(async () =>
        {
            var folder = Path.Combine(Path.GetTempPath(), "OasisMachineWorkspace-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                var session = new AuthoringSession(folder); session.CreateProject("Project");
                using var capture = new WorkspaceCapture(session);
                var view = capture.View;
                view.Machines.OpenMachine(view.Machines.ListMachines().Single().ManifestPath); view.ShowMachine();
                await view.WaitForSceneAsync();
                view.ConfigureNativePreview(new() { FabricRuntimeLibraryPath = "unused-native-library", AudioBufferLengthMilliseconds = 75 });
                var saved = view.InspectWorkspaceState();
                var savedMachine = Assert.Single(Assert.IsType<AssetWorkspaceLayout>(saved.AssetLayout).Documents);
                Assert.Equal("Machine", savedMachine.Kind); Assert.Equal("Machine", savedMachine.Scene?.Mode);
                view.ShowPanel(); view.ConfigureNativePreview(new());
                view.ApplyWorkspaceState(saved);
                await view.WaitForSceneAsync();
                Assert.Equal("Machine", Assert.Single(Assert.IsType<AssetWorkspaceLayout>(view.InspectWorkspaceState().AssetLayout).Documents).Kind);
                Assert.Equal("unused-native-library", view.NativePreviewPreferences.FabricRuntimeLibraryPath);
                Assert.Equal(75, view.NativePreviewPreferences.AudioBufferLengthMilliseconds);
                Assert.Null(view.Preview.Backend);
                view.Machines.SetDisplayName("Pending edit");
                Assert.Throws<InvalidOperationException>(() => view.ApplyWorkspaceState(saved));
            }
            finally { Directory.Delete(folder, recursive: true); }
        });
    }

    [Fact]
    public async Task FloatingPaneReopensInAHeadlessWindowAndResetRetainsTheDocument()
    {
        await HeadlessTestHost.RunAsync(async () =>
        {
            var folder = Path.Combine(Path.GetTempPath(), "OasisFloatingTest-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                var session = new AuthoringSession(folder); session.CreateProject("Project"); session.CreatePanel("Panel");
                var id = session.AddRectangle(); session.Save();
                using var capture = new WorkspaceCapture(session);
                capture.Capture();
                // Move the real Project pane through Dock.Model so both the native
                // layout and the v2 asset-document graph stay in sync.
                capture.View.MoveWorkspacePane("Project", "float");
                var state = capture.View.InspectWorkspaceState();
                Assert.Single(Assert.IsType<AssetWorkspaceLayout>(state.AssetLayout).Layout.Windows!);
                await capture.View.WaitForSceneAsync();
                capture.Capture();
                Assert.Single(capture.View.FloatingWorkspaceWindows);
                Assert.True(Assert.Single(capture.CaptureFloatingWindows()).Length > 1000);
                var path = Path.Combine(folder, "floating.json"); capture.View.ConfigureWorkspacePersistence(path, restore: false); capture.View.SaveWorkspace();
                capture.View.ResetLayout(); Assert.Empty(capture.View.FloatingWorkspaceWindows);
                capture.View.RestoreWorkspace(); await capture.View.WaitForSceneAsync(); Assert.Single(capture.View.FloatingWorkspaceWindows);
                Assert.Equal(id, Assert.Single(capture.View.GetDocumentEditor("Panel2D").CommandSession.Elements).Id);
                capture.View.ResetLayout();
            }
            finally { Directory.Delete(folder, recursive: true); }
        });
    }

    [Fact]
    public void LayoutDataRestoresFloatingPinnedAndReorderedPanesWithoutSerializingControls()
    {
        var hierarchy = new Tool { Id = "Hierarchy", Context = new object() };
        var inspector = new Tool { Id = "Inspector" };
        var project = new Tool { Id = "Project" };
        var document = new Document { Id = "Authoring" };
        var known = new Dictionary<string, IDockable> { ["Hierarchy"] = hierarchy, ["Inspector"] = inspector, ["Project"] = project, ["Authoring"] = document };
        var node = new WorkspaceDockNode("Root", "Root", Children:
        [new("Split", Orientation: "Horizontal", Children:
            [new("Tools", Alignment: "Right", Children: [new("Tool", "Inspector")]), new("Splitter"), new("Documents", Children: [new("Document", "Authoring")])])],
            Left: [new("Tool", "Project")], Windows:
            [new(new(100, 200, 400, 500), new("Root", "Floating", Children: [new("Tools", Alignment: "Left", Children: [new("Tool", "Hierarchy")])]))]);
        var json = WorkspaceLayoutState.Serialize(new(1, "Dark", node));
        Assert.DoesNotContain("Context", json); Assert.DoesNotContain("$type", json);
        var restored = WorkspaceLayoutState.Restore(WorkspaceLayoutState.Deserialize(json).Layout, known);
        Assert.Same(project, Assert.Single(restored.LeftPinnedDockables!));
        var floating = Assert.Single(restored.Windows!);
        Assert.Equal(100, floating.X); Assert.Equal(500, floating.Height);
        Assert.Same(hierarchy, ((IDock)Assert.Single(floating.Layout!.VisibleDockables!)).VisibleDockables![0]);
        var split = Assert.IsAssignableFrom<IProportionalDock>(Assert.Single(restored.VisibleDockables!));
        Assert.Same(inspector, ((IDock)split.VisibleDockables![0]).VisibleDockables![0]);
    }

    [Fact]
    public void LegacyLayoutAddsOutputBesideProjectWithoutChangingActiveProjectTab()
    {
        var hierarchy = new Tool { Id = "Hierarchy" };
        var inspector = new Tool { Id = "Inspector" };
        var project = new Tool { Id = "Project" };
        var output = new Tool { Id = "Output" };
        var document = new Document { Id = "Authoring" };
        var known = new Dictionary<string, IDockable>
        {
            ["Hierarchy"] = hierarchy, ["Inspector"] = inspector, ["Project"] = project,
            ["Output"] = output, ["Authoring"] = document
        };
        var legacy = new WorkspaceDockNode("Root", "Root", Children:
        [new("Split", Orientation: "Horizontal", Children:
            [new("Tools", Alignment: "Left", Children: [new("Tool", "Hierarchy")]), new("Splitter"),
             new("Split", Orientation: "Vertical", Children:
                 [new("Documents", Children: [new("Document", "Authoring")]), new("Splitter"),
                  new("Tools", "ProjectDock", Alignment: "Left", Children: [new("Tool", "Project")])]),
             new("Splitter"), new("Tools", Alignment: "Right", Children: [new("Tool", "Inspector")])])]);

        var restored = WorkspaceLayoutState.Restore(legacy, known);
        IDock? FindProjectDock(IDockable item)
        {
            if (item is not IDock dock) return null;
            if (dock.VisibleDockables?.Any(child => child.Id == "Project") == true) return dock;
            foreach (var child in dock.VisibleDockables ?? [])
                if (FindProjectDock(child) is { } match) return match;
            return null;
        }
        var projectDock = FindProjectDock(restored) ?? throw new InvalidDataException("Project tab was not restored.");
        Assert.Equal(new[] { "Project", "Output" }, projectDock.VisibleDockables!.Select(item => item.Id));
        Assert.Same(project, projectDock.ActiveDockable);
    }

    [Fact]
    public void LayoutRejectsMissingDuplicateUnknownAndUnboundedContent()
    {
        var known = new Dictionary<string, IDockable> { ["Inspector"] = new Tool { Id = "Inspector" } };
        Assert.Throws<InvalidDataException>(() => WorkspaceLayoutState.Restore(new("Root", Children: []), known));
        Assert.Throws<InvalidDataException>(() => WorkspaceLayoutState.Restore(new("Root", Children: [new("Tool", "Inspector"), new("Tool", "Inspector")]), known));
        Assert.Throws<InvalidDataException>(() => WorkspaceLayoutState.Restore(new("System.IO.FileInfo", "Inspector"), known));
        Assert.Throws<InvalidDataException>(() => WorkspaceLayoutState.Restore(new("Root", Proportion: double.NaN), known));
        Assert.Throws<InvalidDataException>(() => WorkspaceLayoutState.Deserialize("""{"Version":1,"Theme":"Dark","Layout":{},"$type":"Unexpected"}"""));
        var legacy = WorkspaceLayoutState.Deserialize("""{"Version":1,"Theme":"Dark","Layout":{"Kind":"Root"}}""");
        Assert.Null(legacy.Scene3D);
    }

    private static byte[] CreateMinimalGlb()
    {
        var json = System.Text.Encoding.UTF8.GetBytes("{\"asset\":{\"version\":\"2.0\"},\"scene\":0,\"scenes\":[{\"nodes\":[]}]}");
        var paddedLength = (json.Length + 3) & ~3;
        var result = new byte[20 + paddedLength];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(0, 4), 0x46546C67);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(4, 4), 2);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(8, 4), (uint)result.Length);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(12, 4), (uint)paddedLength);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(16, 4), 0x4E4F534A);
        json.CopyTo(result.AsSpan(20)); result.AsSpan(20 + json.Length).Fill(0x20);
        return result;
    }

    [Fact]
    public async Task SavedWorkspaceReopensDocumentsAndSelectionAndProtectsPendingEditsAndExternalSettings()
    {
        await HeadlessTestHost.RunAsync(async () =>
        {
            var folder = Path.Combine(Path.GetTempPath(), "OasisWorkspaceTest-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                var session = new AuthoringSession(folder); session.CreateProject("Project"); session.CreatePanel("Panel");
                var id = session.AddRectangle(); session.UpdateElement(id, name: "Retained element"); session.Save();
                using var capture = new WorkspaceCapture(session);
                var view = capture.View;
                await view.WaitForSceneAsync();
                var initialEditor = view.GetDocumentEditor("Panel2D");
                var facePath = initialEditor.Faces.CreateFace("Face");
                var lamp = initialEditor.Faces.AddLamp("Lamp", 10, 20, 30, 40, 7);
                initialEditor.Faces.Save();
                await view.SynchronizeAssetDocumentsAsync();
                await view.ActivateDocumentAsync("Face", facePath);
                var state = view.InspectWorkspaceState();
                var assetLayout = Assert.IsType<AssetWorkspaceLayout>(state.AssetLayout);
                var faceState = Assert.Single(assetLayout.Documents.Where(document => document.Kind == "Face"));
                var reversed = state with { Theme = "Light", AssetSearch = "Face", AssetLayout = assetLayout with
                    { Documents = assetLayout.Documents.Select(document => document.Id == faceState.Id
                        ? document with { Viewport = new(.8, 12, 34, false) } : document).ToArray() } };
                view.ApplyWorkspaceState(reversed);
                await view.WaitForSceneAsync();
                var file = Path.Combine(folder, "workspace.json");
                view.ConfigureWorkspacePersistence(file, restore: false); view.SaveWorkspace();
                var saved = File.ReadAllText(file);
                Assert.DoesNotContain(folder.Replace('\\', '/'), saved);
                Assert.Equal("Light", view.InspectWorkspaceState().Theme);
                var panelEditor = view.GetDocumentEditor("Panel2D");
                panelEditor.CommandSession.UpdateElement(id, name: "Unsaved edit");
                Assert.Throws<InvalidOperationException>(view.RestoreWorkspace);
                view.ResetLayout();
                Assert.Equal("Unsaved edit", Assert.Single(panelEditor.CommandSession.Elements).Name);
                Assert.Equal(saved, File.ReadAllText(file));
                panelEditor.CommandSession.Undo();
                File.AppendAllText(file, "\n");
                Assert.Throws<IOException>(view.SaveWorkspace);
                view.ConfigureWorkspacePersistence(file, restore: false);
                Assert.Throws<IOException>(view.SaveWorkspace);
                view.RestoreWorkspace();
                await view.WaitForSceneAsync();
                var reopened = view.InspectWorkspaceState();
                var reopenedAssets = Assert.IsType<AssetWorkspaceLayout>(reopened.AssetLayout);
                var reopenedPanel = Assert.Single(reopenedAssets.Documents.Where(document => document.Kind == "Panel2D"));
                var reopenedFace = Assert.Single(reopenedAssets.Documents.Where(document => document.Kind == "Face"));
                Assert.Equal(reopenedFace.Id, reopenedAssets.ActiveId); Assert.Equal("Face", reopened.AssetSearch);
                Assert.Equal("Light", reopened.Theme); Assert.Equal(lamp, Assert.Single(reopenedFace.Selection ?? []));
                Assert.Equal(new WorkspaceViewportState(.8, 12, 34, false), reopenedFace.Viewport);
                Assert.Equal(new[] { id }, reopenedPanel.Selection);
                panelEditor = view.GetDocumentEditor("Panel2D");
                Assert.Equal("Retained element", Assert.Single(panelEditor.CommandSession.Elements).Name);
                Assert.False(panelEditor.CommandSession.IsDirty);
                var faceEditor = view.GetDocumentEditor("Face");
                Assert.Equal("Retained element", Assert.Single(panelEditor.CommandSession.Elements).Name);
                faceEditor.Faces.UpdateElement(lamp, name: "Pending Face edit");
                Assert.Throws<InvalidOperationException>(view.RestoreWorkspace); faceEditor.Faces.Undo();
                var before = view.InspectWorkspaceState();
                Assert.Throws<InvalidDataException>(() => view.ApplyWorkspaceState(before with { Project = "../outside.oasisproj" }));
                Assert.Throws<InvalidDataException>(() => view.ApplyWorkspaceState(before with { Layout = null! }));
                var beforeAssetLayout = Assert.IsType<AssetWorkspaceLayout>(before.AssetLayout);
                Assert.Throws<InvalidDataException>(() => view.ApplyWorkspaceState(before with
                {
                    AssetLayout = beforeAssetLayout with { Layout = beforeAssetLayout.Layout with { Children = [null!] } }
                }));
                Assert.Equal(before.Project, view.InspectWorkspaceState().Project);
                Assert.Equal(ThemeVariant.Light, Application.Current!.RequestedThemeVariant);
                Assert.True(capture.Capture(theme: "Light").Length > 5000);
                await view.CloseProjectAsync();
                Assert.Null(session.ProjectFilePath); Assert.Empty(session.Elements); Assert.Empty(view.Faces.Elements);
            }
            finally { Directory.Delete(folder, recursive: true); }
        });
    }
}
