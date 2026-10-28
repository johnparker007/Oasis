using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using OasisEditor.Avalonia;
using Xunit;

namespace OasisEditor.Avalonia.Tests;

[Collection("Avalonia headless UI")]
public sealed class WorkspaceMenuTests
{
    [Fact]
    public void MainMenuRestoresLegacyRootsAndActionGroups()
    {
        HeadlessTestHost.Run(() =>
        {
            using var workspace = CreateWorkspace(withProject: true);
            var view = workspace.Capture.View;
            var menu = view.InspectMenus();

            Assert.Equal(new[] { "File", "Face", "Edit", "Window", "Emulation" }, menu.Select(item => item.Header));
            Assert.Equal(new[] { "Panel2D", "Face", "Cabinet3D", "Machine", "Reel", "Prefab" },
                view.FindMenu("File", "Create").Items.OfType<MenuItem>().Select(item => item.Header?.ToString()?.Replace("_", "")));
            Assert.Equal(new[] { "MFME .FML…", "GLB Model…" },
                view.FindMenu("File", "Import").Items.OfType<MenuItem>().Select(item => item.Header?.ToString()?.Replace("_", "")));
            var faceActions = DirectActionNames(view.FindMenu("Face"));
            Assert.Contains("Add Panel2D Face Source Shape", faceActions);
            Assert.Contains("Create Face from Selected Panel2D Source Shape", faceActions);
            Assert.Contains("Validate Face", faceActions);
            Assert.Equal(new[] { "Bring to front", "Bring forward", "Send backward", "Send to back" },
                DirectActionNames(view.FindMenu("Edit", "Arrange")));
            Assert.Equal(new[] { "Lock Transform", "Unlock Transform", "Hide", "Show" },
                DirectActionNames(view.FindMenu("Edit", "Object")));
            Assert.True(view.FindMenu("Edit", "Preferences").IsEnabled);
            Assert.True(view.FindMenu("Edit", "Project Settings").IsEnabled);
            Assert.Equal(new[] { "Start", "Stop", "Pause", "Unthrottled", "Reset" },
                DirectActionNames(view.FindMenu("Emulation")).Where(name => name is "Start" or "Stop" or "Pause" or "Unthrottled" or "Reset"));

            var fileActions = Flatten(menu.Single(item => item.Header == "File")).ToArray();
            Assert.DoesNotContain(fileActions, item => item.Header is "Validate Face" or "Emulation preview" or "Play View" or "Input Map");
        });
    }

    [Fact]
    public void MenuActionsUpdateSelectionHistoryAndSavedPanel()
    {
        HeadlessTestHost.Run(() =>
        {
            using var workspace = CreateWorkspace(withProject: true);
            var view = workspace.Capture.View;
            var session = workspace.Session;
            var first = session.AddRectangle();
            var second = session.AddRectangle();
            session.Save();
            session.SelectElements([first]);

            Click(view.FindMenu("Edit", "Object", "Hide"));
            Assert.False(session.Elements.Single(item => item.Id == first).Visible);
            Click(view.FindMenu("Edit", "Undo"));
            Assert.True(session.Elements.Single(item => item.Id == first).Visible);

            Click(view.FindMenu("Edit", "Object", "Lock Transform"));
            Assert.True(session.Elements.Single(item => item.Id == first).Locked);
            Click(view.FindMenu("Edit", "Object", "Unlock Transform"));
            Assert.False(session.Elements.Single(item => item.Id == first).Locked);

            Click(view.FindMenu("Edit", "Arrange", "Bring to front"));
            Assert.Equal(new[] { second, first }, session.Elements.Select(item => item.Id));
            Assert.True(session.IsDirty);
            Click(view.FindMenu("File", "Save Asset"));
            Assert.False(session.IsDirty);

            Click(view.FindMenu("Edit", "Arrange", "Send to back"));
            Assert.Equal(new[] { first, second }, session.Elements.Select(item => item.Id));
        });
    }

    [Fact]
    public void ProjectActionsAreDisabledOnWelcomeAndNoFaceOrPreviewActionsAreDuplicatedInFile()
    {
        HeadlessTestHost.Run(() =>
        {
            using var workspace = CreateWorkspace(withProject: false);
            var view = workspace.Capture.View;
            var file = view.FindMenu("File");

            Assert.False(view.FindMenu("File", "Create", "Panel2D").IsEnabled);
            Assert.False(view.FindMenu("Face", "Validate Face").IsEnabled);
            Assert.False(view.FindMenu("Window", "Play View").IsEnabled);
            Assert.False(view.FindMenu("Window", "Input Map").IsEnabled);
            Assert.False(view.FindMenu("Emulation", "Start").IsEnabled);
            Assert.DoesNotContain(Flatten(view.InspectMenus().Single(item => item.Header == "File")),
                item => item.Header is "Validate Face" or "Emulation preview" or "Start");
            Assert.Equal("File", file.Header?.ToString()?.Replace("_", ""));
        });
    }

    [Fact]
    public void MultiSelectionObjectCommandsUndoAtomicallyAndNoOpsDoNotAddHistory()
    {
        HeadlessTestHost.Run(() =>
        {
            using var workspace = CreateWorkspace(withProject: true);
            var view = workspace.Capture.View;
            var session = workspace.Session;
            var first = session.AddRectangle();
            var second = session.AddRectangle();
            session.Save();
            session.SelectElements([first, second]);

            Click(view.FindMenu("Edit", "Object", "Hide"));
            Assert.All(session.Elements, item => Assert.False(item.Visible));
            Click(view.FindMenu("Edit", "Object", "Hide"));
            Assert.All(session.Elements, item => Assert.False(item.Visible));
            Click(view.FindMenu("Edit", "Undo"));
            Assert.All(session.Elements, item => Assert.True(item.Visible));

            Click(view.FindMenu("Edit", "Object", "Lock Transform"));
            Assert.All(session.Elements, item => Assert.True(item.Locked));
            Click(view.FindMenu("Edit", "Object", "Lock Transform"));
            Assert.All(session.Elements, item => Assert.True(item.Locked));
            Click(view.FindMenu("Edit", "Undo"));
            Assert.All(session.Elements, item => Assert.False(item.Locked));
        });
    }

    [Fact]
    public void WindowMenuActivatesEachWorkspacePane()
    {
        HeadlessTestHost.Run(() =>
        {
            using var workspace = CreateWorkspace(withProject: true);
            var view = workspace.Capture.View;

            foreach (var (menuLabel, dockableId) in new[]
            {
                ("Assets", "Project"),
                ("Hierarchy", "Hierarchy"),
                ("Inspector", "Inspector"),
                ("Output", "Output")
            })
            {
                Click(view.FindMenu("Window", menuLabel));
                Assert.True(IsActiveDockable(view.InspectWorkspaceState().Layout, dockableId),
                    $"Window > {menuLabel} should activate the {dockableId} pane.");
            }
        });
    }

    [Fact]
    public async Task FloatingFaceUsesTheWorkspaceMenuAndKeepsProjectCreationAvailable()
    {
        await HeadlessTestHost.RunAsync(async () =>
        {
            using var workspace = CreateWorkspace(withProject: true);
            var view = workspace.Capture.View;
            var facePath = view.Faces.CreateFace("Floating face");

            await view.FloatDocumentAsync("Face", facePath);
            var floating = view.GetFloatingDocumentView("Face", facePath);
            var menu = floating.InspectMenus();

            Assert.Equal(new[] { "File", "Face", "Edit", "Window", "Emulation" }, menu.Select(item => item.Header));
            Assert.All(floating.FindMenu("File", "Create").Items.OfType<MenuItem>(), item => Assert.True(item.IsEnabled));
            Assert.Same(floating,view.CommandView);
            Assert.True(floating.FindMenu("Face", "Validate Face") is not null);
            Assert.True(floating.FindMenu("Emulation", "Start") is not null);
        });
    }

    [Fact]
    public void ProjectSettingsAndPanelCreationMenusOpenTheirRealDialogs()
    {
        HeadlessTestHost.Run(() =>
        {
            using var workspace = CreateWorkspace(withProject: true);
            var view = workspace.Capture.View;
            var session = workspace.Session;
            var owner = Assert.IsType<Window>(TopLevel.GetTopLevel(view));

            Click(view.FindMenu("Edit", "Project Settings"));
            Dispatcher.UIThread.RunJobs();
            var settings = Assert.Single(owner.OwnedWindows.Where(window => window.Title == "Project Settings"));
            var settingsText = settings.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text).ToArray();
            Assert.Contains("Menu test", settingsText);
            Assert.Contains(session.ProjectFilePath, settingsText);
            settings.Close();
            Dispatcher.UIThread.RunJobs();

            Click(view.FindMenu("File", "Create", "Panel2D"));
            Dispatcher.UIThread.RunJobs();
            var createPanel = Assert.Single(owner.OwnedWindows.Where(window => window.Title == "New Panel2D"));
            var name = Assert.Single(createPanel.GetVisualDescendants().OfType<TextBox>());
            name.Text = "Created from menu";
            createPanel.GetVisualDescendants().OfType<Button>()
                .Single(button => Equals(button.Content, "Create Panel2D"))
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();

            session=view.CommandSession;
            Assert.NotNull(session.PanelPath);
            Assert.True(File.Exists(session.PanelPath));
            Assert.Equal("Created from menu", session.PanelTitle);
            Assert.Contains(view.InspectOpenDocuments(), document => document.Kind == "Panel2D"
                && document.Path == session.PanelPath && document.IsActive);
            Assert.DoesNotContain(owner.OwnedWindows, window => window.Title == "New Panel2D");
        });
    }

    [Fact]
    public void RetainedMachineDoesNotEnableCommandsForAnActivePanel()
    {
        HeadlessTestHost.Run(() =>
        {
            using var workspace = CreateWorkspace(withProject: true);
            var view = workspace.Capture.View;
            view.Machines.CreateMachine("Retained machine");
            view.ShowPanel();
            view.InspectMenus();
            Assert.False(view.FindMenu("File", "Build Oasis Player Machine").IsEnabled);
            Assert.False(view.FindMenu("Window", "Input Map").IsEnabled);
            Assert.False(view.FindMenu("Emulation", "Start").IsEnabled);
        });
    }

    private static WorkspaceFixture CreateWorkspace(bool withProject)
    {
        var root = Path.Combine(Path.GetTempPath(), "OasisWorkspaceMenus-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var session = new AuthoringSession(root);
            if (withProject)
            {
                session.CreateProject("Menu test");
                session.CreatePanel("Panel");
            }

            var capture = new WorkspaceCapture(session);
            return new WorkspaceFixture(root, session, capture);
        }
        catch
        {
            Directory.Delete(root, recursive: true);
            throw;
        }
    }

    private static string[] DirectActionNames(MenuItem menu)
        => menu.Items.OfType<MenuItem>()
            .Select(item => item.Header?.ToString()?.Replace("_", ""))
            .Where(name => name is not null)
            .Cast<string>()
            .ToArray();

    private static IEnumerable<EditorMenuEntry> Flatten(EditorMenuEntry entry)
    {
        yield return entry;
        foreach (var child in entry.Items)
            foreach (var descendant in Flatten(child))
                yield return descendant;
    }

    private static bool IsActiveDockable(WorkspaceDockNode node, string dockableId)
    {
        if (node.Active >= 0 && node.Children is { } children && node.Active < children.Length
            && children[node.Active].Id == dockableId)
            return true;
        return node.Children?.Any(child => IsActiveDockable(child, dockableId)) == true
            || node.Hidden?.Any(child => IsActiveDockable(child, dockableId)) == true
            || node.Left?.Any(child => IsActiveDockable(child, dockableId)) == true
            || node.Right?.Any(child => IsActiveDockable(child, dockableId)) == true
            || node.Top?.Any(child => IsActiveDockable(child, dockableId)) == true
            || node.Bottom?.Any(child => IsActiveDockable(child, dockableId)) == true
            || node.Windows?.Any(window => IsActiveDockable(window.Layout, dockableId)) == true;
    }

    private static void Click(MenuItem item)
        => item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

    private sealed class WorkspaceFixture(string root, AuthoringSession session, WorkspaceCapture capture) : IDisposable
    {
        public AuthoringSession Session { get; } = session;
        public WorkspaceCapture Capture { get; } = capture;

        public void Dispose()
        {
            Capture.Dispose();
            Directory.Delete(root, recursive: true);
        }
    }
}
