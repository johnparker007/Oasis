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
    public void MachineAndNativeLibrarySettingsRestoreWithoutStartingPreview()
    {
        HeadlessTestHost.Run(() =>
        {
            var folder = Path.Combine(Path.GetTempPath(), "OasisMachineWorkspace-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                var session = new AuthoringSession(folder); session.CreateProject("Project");
                using var capture = new WorkspaceCapture(session);
                var view = capture.View;
                view.Machines.OpenMachine(view.Machines.ListMachines().Single().ManifestPath); view.ShowMachine();
                view.ConfigureNativePreview(new() { FabricRuntimeLibraryPath = "unused-native-library", AudioBufferLengthMilliseconds = 75 });
                var saved = view.InspectWorkspaceState();
                Assert.Equal("Machine", saved.ActiveDocument); Assert.NotNull(saved.Machine);
                view.ShowPanel(); view.ConfigureNativePreview(new());
                view.ApplyWorkspaceState(saved);
                Assert.Equal("Machine", view.InspectWorkspaceState().ActiveDocument);
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
    public void FloatingPaneReopensInAHeadlessWindowAndResetRetainsTheDocument()
    {
        HeadlessTestHost.Run(() =>
        {
            var folder = Path.Combine(Path.GetTempPath(), "OasisFloatingTest-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                var session = new AuthoringSession(folder); session.CreateProject("Project"); session.CreatePanel("Panel");
                var id = session.AddRectangle(); session.Save();
                using var capture = new WorkspaceCapture(session);
                capture.Capture();
                var state = capture.View.InspectWorkspaceState(); var main = state.Layout.Children![0];
                var floating = state with { Layout = state.Layout with { Children = [main with { Children = main.Children!.Take(3).ToArray() }],
                    Windows = [new(new(100, 100, 320, 520), new("Root", "Floating", Children: [main.Children![4]]))] } };
                capture.View.ApplyWorkspaceState(floating);
                capture.Capture();
                Assert.Single(capture.View.FloatingWorkspaceWindows);
                Assert.True(Assert.Single(capture.CaptureFloatingWindows()).Length > 1000);
                var path = Path.Combine(folder, "floating.json"); capture.View.ConfigureWorkspacePersistence(path, restore: false); capture.View.SaveWorkspace();
                capture.View.ResetLayout(); Assert.Empty(capture.View.FloatingWorkspaceWindows);
                capture.View.RestoreWorkspace(); Assert.Single(capture.View.FloatingWorkspaceWindows);
                Assert.Equal(id, Assert.Single(session.Elements).Id);
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
    public void LayoutRejectsMissingDuplicateUnknownAndUnboundedContent()
    {
        var known = new Dictionary<string, IDockable> { ["Inspector"] = new Tool { Id = "Inspector" } };
        Assert.Throws<InvalidDataException>(() => WorkspaceLayoutState.Restore(new("Root", Children: []), known));
        Assert.Throws<InvalidDataException>(() => WorkspaceLayoutState.Restore(new("Root", Children: [new("Tool", "Inspector"), new("Tool", "Inspector")]), known));
        Assert.Throws<InvalidDataException>(() => WorkspaceLayoutState.Restore(new("System.IO.FileInfo", "Inspector"), known));
        Assert.Throws<InvalidDataException>(() => WorkspaceLayoutState.Restore(new("Root", Proportion: double.NaN), known));
        Assert.Throws<InvalidDataException>(() => WorkspaceLayoutState.Deserialize("""{"Version":1,"Theme":"Dark","Layout":{},"$type":"Unexpected"}"""));
    }

    [Fact]
    public void SavedWorkspaceReopensDocumentsAndSelectionAndProtectsPendingEditsAndExternalSettings()
    {
        HeadlessTestHost.Run(() =>
        {
            var folder = Path.Combine(Path.GetTempPath(), "OasisWorkspaceTest-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                var session = new AuthoringSession(folder); session.CreateProject("Project"); session.CreatePanel("Panel");
                var id = session.AddRectangle(); session.UpdateElement(id, name: "Retained element"); session.Save();
                using var capture = new WorkspaceCapture(session);
                var view = capture.View;
                view.Faces.CreateFace("Face"); var lamp = view.Faces.AddLamp("Lamp", 10, 20, 30, 40, 7); view.Faces.Save(); view.ShowFace();
                var state = view.InspectWorkspaceState();
                var main = state.Layout.Children![0];
                var reversed = state with { Theme = "Light", AssetSearch = "Face", Viewport = new(.8, 12, 34, false), Layout = state.Layout with
                    { Children = [main with { Children = main.Children!.Reverse().ToArray() }] } };
                view.ApplyWorkspaceState(reversed);
                var file = Path.Combine(folder, "workspace.json");
                view.ConfigureWorkspacePersistence(file, restore: false); view.SaveWorkspace();
                var saved = File.ReadAllText(file);
                Assert.DoesNotContain(folder.Replace('\\', '/'), saved);
                Assert.Equal("Light", view.InspectWorkspaceState().Theme);
                session.UpdateElement(id, name: "Unsaved edit");
                Assert.Throws<InvalidOperationException>(view.RestoreWorkspace);
                view.ResetLayout();
                Assert.Equal("Unsaved edit", Assert.Single(session.Elements).Name);
                Assert.Equal(saved, File.ReadAllText(file));
                session.Undo();
                File.AppendAllText(file, "\n");
                Assert.Throws<IOException>(view.SaveWorkspace);
                view.RestoreWorkspace();
                var reopened = view.InspectWorkspaceState();
                Assert.Equal("Face", reopened.ActiveDocument); Assert.Equal("Face", reopened.AssetSearch);
                Assert.Equal("Light", reopened.Theme); Assert.Equal(lamp, view.Faces.SelectedId);
                Assert.Equal(new WorkspaceViewportState(.8, 12, 34, false), reopened.Viewport);
                Assert.Equal(id, session.SelectedId); Assert.Equal("Retained element", Assert.Single(session.Elements).Name);
                Assert.Equal("Inspector", reopened.Layout.Children![0].Children![0].Children![0].Id);
                view.Faces.UpdateElement(lamp, name: "Pending Face edit");
                Assert.Throws<InvalidOperationException>(view.RestoreWorkspace); view.Faces.Undo();
                var before = view.InspectWorkspaceState();
                Assert.Throws<InvalidDataException>(() => view.ApplyWorkspaceState(before with { Project = "../outside.oasisproj" }));
                Assert.Throws<InvalidDataException>(() => view.ApplyWorkspaceState(before with { Layout = null! }));
                Assert.Throws<InvalidDataException>(() => view.ApplyWorkspaceState(before with { Layout = before.Layout with { Children = [null!] } }));
                Assert.Equal(before.Project, view.InspectWorkspaceState().Project);
                Assert.Equal(ThemeVariant.Light, Application.Current!.RequestedThemeVariant);
                Assert.True(capture.Capture(theme: "Light").Length > 5000);
                view.ApplyWorkspaceState(view.InspectWorkspaceState() with { Project = null, Panel = null, Face = null });
                Assert.Null(session.ProjectFilePath); Assert.Empty(session.Elements); Assert.Empty(view.Faces.Elements);
            }
            finally { Directory.Delete(folder, recursive: true); }
        });
    }
}
