using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Xunit;

namespace OasisEditor.Avalonia.Tests;

[Collection("Avalonia headless UI")]
public sealed class WindowPlacementPersistenceTests
{
    [Fact]
    public async Task LauncherAndEditorRestoreIndependentlyIncludingNormalBoundsBehindMaximize()
    {
        await HeadlessTestHost.RunAsync(async () =>
        {
            var root = Temp(); var path = Path.Combine(root, "window-placement.json");
            try
            {
                var errors = new List<string>();
                var window = Window();
                using (var tracker = new WindowPlacementPersistence(window, path, true, errors.Add))
                {
                    window.Show(); window.Position = new PixelPoint(120, 90); window.Width = 1050; window.Height = 700; Dispatcher.UIThread.RunJobs();
                    tracker.Switch(false);
                    window.Position = new PixelPoint(250, 160); window.Width = 1300; window.Height = 800; Dispatcher.UIThread.RunJobs();
                    window.WindowState = WindowState.Maximized; Dispatcher.UIThread.RunJobs();
                    // Native maximize/minimize can report a different screen-sized viewport.
                    window.Position = new PixelPoint(0, 0); window.Width = 1900; window.Height = 1000; Dispatcher.UIThread.RunJobs();
                    window.WindowState = WindowState.Minimized; Dispatcher.UIThread.RunJobs();
                    tracker.Switch(true);
                    Assert.Equal(WindowState.Normal, window.WindowState); Assert.Equal(new PixelPoint(120, 90), window.Position);
                    Assert.Equal(1050, window.Width); Assert.Equal(700, window.Height);
                    tracker.Switch(false); Assert.Equal(WindowState.Maximized, window.WindowState);
                }
                window.Close();
                var saved = JsonSerializer.Deserialize<ApplicationWindowPlacements>(File.ReadAllText(path))!;
                Assert.Equal(new WorkspaceWindowState(250, 160, 1300, 800, "Maximized"), saved.Editor);
                var next = Window();
                using (var tracker = new WindowPlacementPersistence(next, path, true, errors.Add))
                {
                    next.Show(); Assert.Equal(new PixelPoint(120, 90), next.Position); Assert.Equal(WindowState.Normal, next.WindowState);
                    tracker.Switch(false); Assert.Equal(WindowState.Maximized, next.WindowState);
                    next.WindowState = WindowState.Normal; Dispatcher.UIThread.RunJobs();
                    Assert.Equal(1300, next.Width); Assert.Equal(800, next.Height); Assert.Equal(new PixelPoint(250, 160), next.Position);
                    next.Position = new PixelPoint(300, 180); Dispatcher.UIThread.RunJobs(); await Task.Delay(650);
                    Assert.Equal(300, JsonSerializer.Deserialize<ApplicationWindowPlacements>(File.ReadAllText(path))!.Editor!.X);
                }
                next.Close(); Assert.Empty(errors);
            }
            finally { Directory.Delete(root, true); }
        });
    }

    [Fact]
    public void CorruptSettingsArePreservedAndMissingMonitorPlacementIsClamped()
    {
        HeadlessTestHost.Run(() =>
        {
            var root = Temp(); var path = Path.Combine(root, "window-placement.json");
            try
            {
                File.WriteAllText(path, "broken placement"); var errors = new List<string>(); var window = Window();
                using (var tracker = new WindowPlacementPersistence(window, path, true, errors.Add)) { window.Show(); tracker.Switch(false); }
                window.Close(); Assert.NotEmpty(errors); Assert.Equal("broken placement", File.ReadAllText(path));
                File.WriteAllText(path, JsonSerializer.Serialize(new ApplicationWindowPlacements(new(90000, 90000, 1300, 800, "Minimized"))));
                window = Window();
                using (var tracker = new WindowPlacementPersistence(window, path, true, errors.Add))
                {
                    window.Show(); Assert.Equal(WindowState.Normal, window.WindowState);
                    if (window.Screens.All.Count > 0) Assert.Contains(window.Screens.All, screen => screen.WorkingArea.Contains(window.Position));
                }
                window.Close();
            }
            finally { Directory.Delete(root, true); }
        });
    }

    [Fact]
    public async Task ActualLauncherProjectTransitionsAndRestartUseTheirOwnProfiles()
    {
        await HeadlessTestHost.RunAsync(async () =>
        {
            var root = Temp(); var settings = Path.Combine(root, "workspace.json"); var recents = new RecentProjectService(Path.Combine(root, "recent.json"));
            try
            {
                using (var view = new WorkspaceView(new AuthoringSession(root)))
                {
                    var window = Window(); window.Content = view; view.ConfigureProjectLauncher(settings, recents, window); window.Show();
                    window.Position = new PixelPoint(100, 80); window.Width = 1100; Dispatcher.UIThread.RunJobs();
                    Assert.True(await view.RequestCreateProjectAsync("Project"));
                    window.Position = new PixelPoint(220, 140); window.Width = 1250; window.Height = 780; Dispatcher.UIThread.RunJobs();
                    window.WindowState = WindowState.Maximized; Dispatcher.UIThread.RunJobs();
                    Assert.True(await view.RequestCloseProjectAsync());
                    Assert.True(view.IsProjectLauncherVisible); Assert.Equal(new PixelPoint(100, 80), window.Position); Assert.Equal(1100, window.Width);
                    Assert.True(await view.RequestOpenProjectAsync(Path.Combine(root, "Project", "Project.oasisproj")));
                    Assert.Equal(WindowState.Maximized, window.WindowState);
                    window.Close();
                }
                using (var view = new WorkspaceView(new AuthoringSession(root)))
                {
                    var window = Window(); window.Content = view; view.ConfigureProjectLauncher(settings, recents, window); window.Show();
                    Assert.True(view.IsProjectLauncherVisible); Assert.Null(view.CommandSession.ProjectFilePath);
                    Assert.Equal(new PixelPoint(100, 80), window.Position); Assert.Equal(WindowState.Normal, window.WindowState);
                    Assert.True(await view.RequestOpenProjectAsync(Path.Combine(root, "Project", "Project.oasisproj")));
                    Assert.Equal(WindowState.Maximized, window.WindowState); window.Close();
                }
            }
            finally { Directory.Delete(root, true); }
        });
    }

    private static Window Window() => new() { Width = 1440, Height = 900, MinWidth = 960, MinHeight = 640 };
    private static string Temp() { var path = Path.Combine(Path.GetTempPath(), "OasisWindowPlacement-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(path); return path; }
}
