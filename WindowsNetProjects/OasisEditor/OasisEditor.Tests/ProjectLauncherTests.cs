using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Dock.Avalonia.Controls;
using Xunit;

namespace OasisEditor.Avalonia.Tests;

[Collection("Avalonia headless UI")]
public sealed class ProjectLauncherTests
{
    [Fact]
    public async Task StartupIgnoresSavedProjectAndLauncherExitPreservesWorkspace()
    {
        await HeadlessTestHost.RunAsync(async () =>
        {
            var root = TempDirectory(); var previousTheme = Application.Current!.RequestedThemeVariant;
            try
            {
                var saved = new AuthoringSession(root); saved.CreateProject("Previous"); var panel = saved.CreatePanel("Panel");
                var settings = Path.Combine(root, "workspace.json");
                using (var old = new WorkspaceCapture(saved))
                {
                    await old.View.OpenNativeAssetAsync("Panel2D", panel);
                    Application.Current.RequestedThemeVariant = ThemeVariant.Light;
                    old.View.ConfigureWorkspacePersistence(settings, restore: false); old.View.SaveWorkspace();
                }
                var original = File.ReadAllBytes(settings);
                var session = new AuthoringSession(root);
                using var capture = new WorkspaceCapture(session);
                capture.View.ConfigureProjectLauncher(settings, new RecentProjectService(Path.Combine(root, "recent.json")));
                Assert.Equal(ThemeVariant.Light, Application.Current.RequestedThemeVariant);
                capture.Capture();
                AssertLauncher(capture.View, true);
                Assert.Null(session.ProjectFilePath); Assert.Empty(capture.View.InspectOpenDocuments());
                Assert.Empty(capture.View.FloatingWorkspaceWindows);
                Capture(capture, "launcher-startup.png");
                Assert.True(await capture.View.RequestEditorExitAsync());
                Assert.Equal(original, File.ReadAllBytes(settings));
            }
            finally { Application.Current.RequestedThemeVariant = previousTheme; Directory.Delete(root, true); }
        });
    }

    [Fact]
    public async Task RecentOpenAndCreateEnterEditorAndDirtyCloseCanCancelOrReturnToLauncher()
    {
        await HeadlessTestHost.RunAsync(async () =>
        {
            var root = TempDirectory();
            try
            {
                var saved = new AuthoringSession(root); var project = saved.CreateProject("Recent"); var panel = saved.CreatePanel("Panel");
                var recents = new RecentProjectService(Path.Combine(root, "recent.json")); recents.Record(root, project);
                var session = new AuthoringSession(root);
                using var capture = new WorkspaceCapture(session); var view = capture.View;
                view.ConfigureProjectLauncher(Path.Combine(root, "workspace.json"), recents); capture.Capture();
                AssertLauncher(view, true);
                var recent = view.GetVisualDescendants().OfType<Button>().Single(b => b.Content is StackPanel detail && detail.Children.OfType<TextBlock>().Any(t => t.Text == project));
                recent.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await Until(() => session.ProjectFilePath == project && !view.IsCloseWorkflowActive);
                capture.Capture(); AssertLauncher(view, false);
                await view.OpenNativeAssetAsync("Panel2D", panel);
                view.CommandSession.AddRectangle();
                var closing = view.RequestCloseProjectAsync(); await Until(() => view.UnsavedChangesWindow is not null);
                view.RespondToUnsavedChanges(UnsavedChangesChoice.Cancel);
                Assert.False(await closing); AssertLauncher(view, false); Assert.True(view.HasUnsavedChanges);
                closing = view.RequestCloseProjectAsync(); await Until(() => view.UnsavedChangesWindow is not null);
                view.RespondToUnsavedChanges(UnsavedChangesChoice.Discard);
                Assert.True(await closing); capture.Capture(); AssertLauncher(view, true);
                Assert.Null(session.ProjectFilePath); Assert.Empty(view.InspectOpenDocuments());
                Capture(capture, "launcher-after-close.png");
                Assert.True(await view.RequestCreateProjectAsync("New")); capture.Capture();
                Assert.Equal("New", session.ProjectName); AssertLauncher(view, false);
                var created = session.ProjectFilePath;
                Assert.True(await view.RequestCloseProjectAsync()); capture.Capture(); AssertLauncher(view, true);
                Assert.Contains(view.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == created && t.IsEffectivelyVisible);
            }
            finally { Directory.Delete(root, true); }
        });
    }

    [Fact]
    public async Task MalformedSavedWorkspaceStillShowsLauncherAndDoesNotOverwriteSettings()
    {
        await HeadlessTestHost.RunAsync(async () =>
        {
            var root = TempDirectory();
            try
            {
                var settings = Path.Combine(root, "workspace.json"); File.WriteAllText(settings, "broken workspace");
                using var capture = new WorkspaceCapture(new AuthoringSession(root));
                capture.View.ConfigureProjectLauncher(settings, new RecentProjectService(Path.Combine(root, "recent.json")));
                capture.Capture(); AssertLauncher(capture.View, true);
                Assert.True(await capture.View.RequestEditorExitAsync());
                Assert.Equal("broken workspace", File.ReadAllText(settings));
            }
            finally { Directory.Delete(root, true); }
        });
    }

    private static void AssertLauncher(WorkspaceView view, bool visible)
    {
        Assert.Equal(visible, view.IsProjectLauncherVisible);
        var page = view.GetVisualDescendants().Single(c => c.Name == "ProjectLauncher");
        Assert.Equal(visible, page.IsEffectivelyVisible);
        Assert.All(view.GetVisualDescendants().OfType<DockControl>(), dock => Assert.Equal(!visible, dock.IsEffectivelyVisible));
        Assert.All(view.GetVisualDescendants().OfType<Menu>(), menu => Assert.Equal(!visible, menu.IsEffectivelyVisible));
        if (visible) Assert.Contains(page.GetVisualDescendants().OfType<Button>(), b => Equals(b.Content, "New project…") && b.IsEffectivelyVisible);
    }
    private static async Task Until(Func<bool> condition)
    { for (var i = 0; i < 150 && !condition(); i++) await Task.Delay(10); Assert.True(condition()); }
    private static string TempDirectory()
    { var root = Path.Combine(Path.GetTempPath(), "OasisLauncher-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root); return root; }
    private static void Capture(WorkspaceCapture capture, string name)
    {
        var path = Environment.GetEnvironmentVariable("OASIS_LAUNCHER_VERIFICATION_OUTPUT");
        if (string.IsNullOrWhiteSpace(path)) return;
        Directory.CreateDirectory(path); File.WriteAllBytes(Path.Combine(path, name), capture.Capture());
    }
}
