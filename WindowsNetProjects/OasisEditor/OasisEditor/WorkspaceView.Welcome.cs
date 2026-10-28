using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace OasisEditor.Avalonia;

public sealed partial class WorkspaceView
{
    /// <summary>
    /// Creates the empty-workspace landing page using the existing project dialogs and an explicitly configured
    /// recents service; this method never discovers or reads a personal settings location itself.
    /// </summary>
    public void ConfigureWelcomePage(RecentProjectService recentProjects)
    {
        if (_isAssetWindow) return;
        if (Content is Grid root) { if (_welcomePage is not null) root.Children.Remove(_welcomePage); var page = CreateWelcomePage(recentProjects); Grid.SetRow(page, 1); Grid.SetRowSpan(page, 2); root.Children.Add(page); RecordCurrentProject(); }
    }

    public Control CreateWelcomePage(RecentProjectService recentProjects)
    {
        ArgumentNullException.ThrowIfNull(recentProjects);
        _recentProjectService = recentProjects;

        var snapshot = recentProjects.Load(_session.WorkspacePath);
        var page = new Grid { Background = this.Background, RowDefinitions = new RowDefinitions("*,Auto,*") };
        page.Bind(Panel.BackgroundProperty, page.GetResourceObservable("WorkspaceBrush"));
        var content = new StackPanel { Width = 620, Spacing = 16, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        content.Children.Add(new TextBlock { Text = "OASIS EDITOR", FontSize = 13, FontWeight = global::Avalonia.Media.FontWeight.SemiBold, Opacity = .7 });
        content.Children.Add(new TextBlock { Text = "Start with a project", FontSize = 30, FontWeight = global::Avalonia.Media.FontWeight.SemiBold });
        content.Children.Add(new TextBlock { Text = "Create a project in the active workspace, open one already there, or continue a recent project.", TextWrapping = global::Avalonia.Media.TextWrapping.Wrap, Opacity = .78 });
        content.Children.Add(new TextBlock { Text = "Workspace  ·  " + _session.WorkspacePath, TextWrapping = global::Avalonia.Media.TextWrapping.Wrap, FontSize = 12, Opacity = .66 });

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        actions.Children.Add(ActionButton("New project…", async () =>
        {
            await NewProjectDialog();
            RecordCurrentProject();
            RefreshWelcomePage();
        }));
        actions.Children.Add(ActionButton("Open project…", async () =>
        {
            await OpenProjectDialog();
            RecordCurrentProject();
            RefreshWelcomePage();
        }));
        content.Children.Add(actions);

        content.Children.Add(new TextBlock { Text = "RECENT PROJECTS", FontSize = 11, FontWeight = global::Avalonia.Media.FontWeight.SemiBold, Margin = new global::Avalonia.Thickness(0, 8, 0, 0) });
        if (snapshot.Diagnostics.Count > 0)
        {
            foreach (var diagnostic in snapshot.Diagnostics)
                content.Children.Add(new TextBlock { Text = diagnostic, TextWrapping = global::Avalonia.Media.TextWrapping.Wrap });
        }
        else if (snapshot.Projects.Count == 0)
        {
            content.Children.Add(new TextBlock { Text = "No recent projects yet.", Opacity = .7 });
        }
        else
        {
            var recentList = new StackPanel { Spacing = 4 };
            foreach (var project in snapshot.Projects)
            {
                var button = new Button { HorizontalContentAlignment = HorizontalAlignment.Stretch, IsEnabled = project.CanOpen, Padding = new global::Avalonia.Thickness(10, 7) };
                var detail = new StackPanel { Spacing = 2 };
                detail.Children.Add(new TextBlock { Text = project.DisplayName, FontWeight = global::Avalonia.Media.FontWeight.SemiBold });
                detail.Children.Add(new TextBlock { Text = project.Path, TextWrapping = global::Avalonia.Media.TextWrapping.Wrap, FontSize = 11, Opacity = .7 });
                if (project.Diagnostic is not null)
                    detail.Children.Add(new TextBlock { Text = project.Diagnostic, TextWrapping = global::Avalonia.Media.TextWrapping.Wrap, FontSize = 11 });
                button.Content = detail;
                button.Click += async (_, _) =>
                {
                    if (!project.CanOpen) return;
                    try { await RequestOpenProjectAsync(project.Path); } catch(Exception error) when(error is not OutOfMemoryException){await ShowCloseErrorAsync(error);}
                    RecordCurrentProject();
                    RefreshWelcomePage();
                };
                recentList.Children.Add(button);
            }
            content.Children.Add(new ScrollViewer { Content = recentList, MaxHeight = 260, VerticalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Auto });
        }

        Grid.SetRow(content, 1);
        page.Children.Add(content);
        _welcomePage = page;
        RefreshWelcomePage();
        return page;
    }

    private Control? _welcomePage;

    /// <summary>Updates welcome visibility after project open/close operations; call from the workspace refresh hook.</summary>
    public void RefreshWelcomePage()
    {
        if (_welcomePage is not null) _welcomePage.IsVisible = _session.ProjectFilePath is null;
    }

    private void RecordCurrentProject()
    {
        if (_session.ProjectFilePath is null || _welcomePage is null || _recentProjectService is null) return;
        try { _recentProjectService.Record(_session.WorkspacePath, _session.ProjectFilePath); }
        catch (Exception error) when (error is IOException or InvalidOperationException or ArgumentException or UnauthorizedAccessException)
        { SetStatus($"Project opened, but recent-project settings could not be updated: {error.Message}"); }
    }

    private RecentProjectService? _recentProjectService;
}
