using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using OasisEditor.Avalonia;
using Dock.Avalonia.Controls;
using Dock.Model.Core;
using Xunit;

namespace OasisEditor.Avalonia.Tests;

[Collection("Avalonia headless UI")]
public sealed class UnsavedChangesEntryPointTests
{
    [Fact]
    public async Task FileCloseAssetPromptsAndSupportsCancelThenSave()
    {
        await HeadlessTestHost.RunAsync(async () =>
        {
            var project = CreateProject("FileClose");
            try
            {
                using var capture = new WorkspaceCapture(OpenProject(project));
                var view = capture.View;
                await view.OpenNativeAssetAsync("Panel2D", project.PanelA);
                var editor = view.GetFloatingDocumentView("Panel2D", project.PanelA);
                var session = editor.CommandSession;
                session.UpdateElement(session.AddRectangle(), name: "File close edit");
                var disk = File.ReadAllText(project.PanelA);

                Click(view.FindMenu("File", "Close asset"));
                await WaitForDialog(view);
                ClickDialog(view.UnsavedChangesWindow, "UnsavedChangesCancel");
                await WaitUntil(() => !view.IsCloseWorkflowActive);
                Assert.True(session.IsDirty);
                Assert.Equal(disk, File.ReadAllText(project.PanelA));
                Assert.Contains(view.InspectOpenDocuments(), tab => SamePath(tab.Path, project.PanelA));

                Click(view.FindMenu("File", "Close asset"));
                await WaitForDialog(view);
                ClickDialog(view.UnsavedChangesWindow, "UnsavedChangesSave");
                await WaitUntil(() => !view.IsCloseWorkflowActive);
                Assert.False(session.IsDirty);
                Assert.Contains("File close edit", File.ReadAllText(project.PanelA));
                Assert.DoesNotContain(view.InspectOpenDocuments(), tab => SamePath(tab.Path, project.PanelA));
            }
            finally { Directory.Delete(project.Root, recursive: true); }
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OpenedTabContextMenuClosePromptsAndKeepsTabOnCancel(bool closeButton)
    {
        await HeadlessTestHost.RunAsync(async () =>
        {
            var project = CreateProject("ContextClose");
            try
            {
                using var capture = new WorkspaceCapture(OpenProject(project));
                var view = capture.View;
                await view.OpenNativeAssetAsync("Panel2D", project.PanelA);
                var editor = view.GetFloatingDocumentView("Panel2D", project.PanelA);
                editor.CommandSession.UpdateElement(editor.CommandSession.AddRectangle(), name: "Context edit");

                if(closeButton)
                {
                    capture.Capture();
                    var tab=view.GetVisualDescendants().OfType<DocumentTabStripItem>().Single(t=>(t.DataContext as IDockable)?.Id==AssetTabId(view,project.PanelA));
                    var button=tab.GetVisualDescendants().OfType<Button>().Single(b=>b.GetVisualDescendants().OfType<global::Avalonia.Controls.Shapes.Path>().Any(p=>p.Name=="PART_ClosePath"));
                    var owner=(Window)TopLevel.GetTopLevel(view)!;
                    var point=button.TranslatePoint(new global::Avalonia.Point(button.Bounds.Width/2,button.Bounds.Height/2),owner)!.Value;
                    owner.MouseDown(point,MouseButton.Left);owner.MouseUp(point,MouseButton.Left);
                }
                else
                {
                    var menu = view.CreateTabContextMenu(AssetTabId(view, project.PanelA));
                    menu.Open(view);
                    Assert.True(menu.IsOpen);
                    Click(menu.Items.OfType<MenuItem>().Single(item => Equals(item.Header, "Close Tab")));
                }
                await WaitForDialog(view);
                ClickDialog(view.UnsavedChangesWindow, "UnsavedChangesCancel");
                await WaitUntil(() => !view.IsCloseWorkflowActive);
                Assert.Contains(view.InspectOpenDocuments(), tab => SamePath(tab.Path, project.PanelA));
                Assert.True(editor.CommandSession.IsDirty);
            }
            finally { Directory.Delete(project.Root, recursive: true); }
        });
    }

    [Fact]
    public async Task SaveAllMenuAndShortcutSaveEveryPanelAndKeepActiveDocument()
    {
        await HeadlessTestHost.RunAsync(async () =>
        {
            var project = CreateProject("SaveAll");
            try
            {
                using var capture = new WorkspaceCapture(OpenProject(project));
                var view = capture.View;
                await OpenBothAndEdit(view, project, "Menu saved");
                await view.ActivateDocumentAsync("Panel2D", project.PanelB);
                var activeBefore = view.InspectOpenDocuments().Single(tab => tab.IsActive).Path;
                Click(view.FindMenu("File", "Save All"));
                await WaitUntil(() => !view.HasUnsavedChanges);
                Assert.Contains("Menu saved A", File.ReadAllText(project.PanelA));
                Assert.Contains("Menu saved B", File.ReadAllText(project.PanelB));
                Assert.Equal(activeBefore, view.InspectOpenDocuments().Single(tab => tab.IsActive).Path);

                await OpenBothAndEdit(view, project, "Shortcut saved");
                await view.ActivateDocumentAsync("Panel2D", project.PanelA);
                capture.Capture();
                Assert.True(view.GetVisualDescendants().OfType<Button>().First(v=>v.IsEffectivelyVisible && v.Focusable).Focus());
                var window = Assert.IsAssignableFrom<TopLevel>(TopLevel.GetTopLevel(view));
                Press(window, Key.S, RawInputModifiers.Control | RawInputModifiers.Shift);
                await WaitUntil(() => !view.HasUnsavedChanges);
                Assert.Contains("Shortcut saved A", File.ReadAllText(project.PanelA));
                Assert.Contains("Shortcut saved B", File.ReadAllText(project.PanelB));
                Assert.Equal(Path.GetFullPath(project.PanelA), Path.GetFullPath(view.InspectOpenDocuments().Single(tab => tab.IsActive).Path));
            }
            finally { Directory.Delete(project.Root, recursive: true); }
        });
    }

    [Fact]
    public async Task FloatingWindowCloseRedocksDirtyAssetWithoutPromptAndFloatingTabClosePrompts()
    {
        await HeadlessTestHost.RunAsync(async () =>
        {
            var project = CreateProject("FloatingClose");
            try
            {
                using var capture = new WorkspaceCapture(OpenProject(project));
                var view = capture.View;
                await view.OpenNativeAssetAsync("Panel2D", project.PanelA);
                var editor = view.GetFloatingDocumentView("Panel2D", project.PanelA);
                editor.CommandSession.UpdateElement(editor.CommandSession.AddRectangle(), name: "Redock edit");
                await view.FloatDocumentAsync("Panel2D", project.PanelA);
                var nativeWindow = Assert.IsAssignableFrom<Window>(TopLevel.GetTopLevel(editor));
                nativeWindow.Close();
                await view.WaitForSceneAsync();
                Assert.Empty(view.FloatingWorkspaceWindows);
                Assert.Contains(view.InspectOpenDocuments(), tab => SamePath(tab.Path, project.PanelA) && tab.IsDirty);
                Assert.Null(view.UnsavedChangesWindow);

                await view.FloatDocumentAsync("Panel2D", project.PanelA);
                var floatingMenu = view.CreateTabContextMenu(AssetTabId(view, project.PanelA));
                floatingMenu.Open(editor);
                Click(floatingMenu.Items.OfType<MenuItem>().Single(item => Equals(item.Header, "Close Tab")));
                await WaitForDialog(view);
                ClickDialog(view.UnsavedChangesWindow, "UnsavedChangesCancel");
                await WaitUntil(() => !view.IsCloseWorkflowActive);
                Assert.Contains(view.InspectOpenDocuments(), tab => SamePath(tab.Path, project.PanelA) && tab.IsDirty);
            }
            finally { Directory.Delete(project.Root, recursive: true); }
        });
    }

    [Fact]
    public async Task FileCloseProjectAndNewProjectCancelPreserveCurrentDirtyProject()
    {
        await HeadlessTestHost.RunAsync(async () =>
        {
            var project = CreateProject("ProjectActions");
            try
            {
                using var capture = new WorkspaceCapture(OpenProject(project));
                var view = capture.View;
                await view.OpenNativeAssetAsync("Panel2D", project.PanelA);
                var editor = view.GetFloatingDocumentView("Panel2D", project.PanelA);
                editor.CommandSession.UpdateElement(editor.CommandSession.AddRectangle(), name: "Keep this edit");
                var disk = File.ReadAllText(project.PanelA);

                Click(view.FindMenu("File", "Close Project"));
                await WaitForDialog(view);
                ClickDialog(view.UnsavedChangesWindow, "UnsavedChangesCancel");
                await WaitUntil(() => !view.IsCloseWorkflowActive);
                Assert.Equal(Path.GetFullPath(project.ProjectFile), Path.GetFullPath(view.CommandSession.ProjectFilePath!));
                Assert.True(view.HasUnsavedChanges);
                Assert.Equal(disk, File.ReadAllText(project.PanelA));

                Click(view.FindMenu("File", "New project…"));
                await WaitForOwnedWindow(view, "New Oasis project");
                var owner = Assert.IsType<Window>(TopLevel.GetTopLevel(view));
                var createDialog = owner.OwnedWindows.Single(window => window.Title == "New Oasis project");
                createDialog.GetVisualDescendants().OfType<TextBox>().Single().Text = "Cancelled project";
                createDialog.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "Create project"))
                    .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await WaitForDialog(view);
                ClickDialog(view.UnsavedChangesWindow, "UnsavedChangesCancel");
                await WaitUntil(() => !view.IsCloseWorkflowActive);
                Assert.Equal(Path.GetFullPath(project.ProjectFile), Path.GetFullPath(view.CommandSession.ProjectFilePath!));
                Assert.True(view.HasUnsavedChanges);
                Assert.Equal(disk, File.ReadAllText(project.PanelA));
                Assert.False(File.Exists(Path.Combine(project.Root, "Cancelled project.oasisproj")));
                owner.OwnedWindows.Single(window => window.Title == "New Oasis project").Close();
            }
            finally { Directory.Delete(project.Root, recursive: true); }
        });
    }

    private static async Task OpenBothAndEdit(WorkspaceView view, TestProject project, string prefix)
    {
        await view.OpenNativeAssetAsync("Panel2D", project.PanelA);
        await view.OpenNativeAssetAsync("Panel2D", project.PanelB);
        foreach (var path in new[] { project.PanelA, project.PanelB })
        {
            var editor = view.GetFloatingDocumentView("Panel2D", path);
            var session = editor.CommandSession;
            session.UpdateElement(session.AddRectangle(), name: $"{prefix} {Path.GetFileName(Path.GetDirectoryName(path))}");
        }
        await view.SynchronizeAssetDocumentsAsync();
    }

    private static void Click(MenuItem item)
        => item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

    private static void ClickDialog(Window? dialog, string name)
        => LogicalButtons(Assert.IsType<Window>(dialog)).Single(button => button.Name == name)
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private static IEnumerable<Button> LogicalButtons(Control root)
        => root.GetLogicalDescendants().OfType<Button>();

    private static async Task WaitForDialog(WorkspaceView view)
    {
        for (var i = 0; i < 150 && view.UnsavedChangesWindow is null; i++)
            await Task.Delay(10);
        Assert.NotNull(view.UnsavedChangesWindow);
    }

    private static async Task WaitForOwnedWindow(WorkspaceView view, string title)
    {
        var owner = Assert.IsType<Window>(TopLevel.GetTopLevel(view));
        for (var i = 0; i < 150 && !owner.OwnedWindows.Any(window => window.Title == title); i++)
            await Task.Delay(10);
        Assert.Contains(owner.OwnedWindows, window => window.Title == title);
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        for (var i = 0; i < 150 && !condition(); i++) await Task.Delay(10);
        Assert.True(condition());
    }

    private static void Press(TopLevel window, Key key, RawInputModifiers modifiers)
    {
        var physicalKey = Enum.Parse<PhysicalKey>(key.ToString());
        window.KeyPress(key, modifiers, physicalKey, key.ToString());
        window.KeyRelease(key, modifiers, physicalKey, key.ToString());
    }

    private static TestProject CreateProject(string name)
    {
        var root = Path.Combine(Path.GetTempPath(), "OasisUnsavedEntryPoints-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var author = new AuthoringSession(root);
        var projectFile = author.CreateProject(name);
        var panelA = author.CreatePanel("A"); author.AddRectangle(); author.Save();
        var panelB = author.CreatePanel("B"); author.AddRectangle(); author.Save();
        return new TestProject(root, projectFile, panelA, panelB);
    }

    private static AuthoringSession OpenProject(TestProject project)
    {
        var session = new AuthoringSession(project.Root);
        session.OpenProject(project.ProjectFile);
        return session;
    }

    private static bool SamePath(string left, string right)
        => string.Equals(Path.GetFullPath(left), Path.GetFullPath(right),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private static string AssetTabId(WorkspaceView view, string path)
        => view.InspectWorkspaceState().AssetLayout!.Documents.Single(document =>
            document.Kind == "Panel2D" && SamePath(Path.GetFullPath(document.Path,Path.GetDirectoryName(view.CommandSession.ProjectFilePath!)!), path)).Id;

    private sealed record TestProject(string Root, string ProjectFile, string PanelA, string PanelB);
}
