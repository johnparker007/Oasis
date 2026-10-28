using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.VisualTree;
using Dock.Avalonia.Controls;
using Dock.Model.Core;
using Avalonia.Data;

namespace OasisEditor.Avalonia;

public sealed class EditorApplication : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        DataTemplates.Add(new FuncDataTemplate<IDockable>((item, _) =>
        {
            var presenter = new DockPanePresenter();
            presenter.Bind(DockPanePresenter.PaneContextProperty, new Binding(nameof(IDockable.Context)) { Source = item });
            return presenter;
        }));
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var session = new AuthoringSession(Program.Workspace);
            var window = new Window { Width = 1440, Height = 900, MinWidth = 960, MinHeight = 640 };
            var view = new WorkspaceView(session);
            window.Content = view;
            window.Title = "Oasis Editor";
            view.StateChanged += () => window.Title = $"{view.ActiveTitle}{(view.HasUnsavedChanges ? " *" : "")} — {session.ProjectName ?? "Oasis Editor"}";
            window.Closing += (_, e) => { if (view.HasUnsavedChanges || view.IsMovingAssetWindow) { e.Cancel = true; view.SetStatus(view.IsMovingAssetWindow?"Wait for document docking or workspace restoration to finish before closing.":"Save or undo your changes before closing."); } };
            window.Closing += (_, e) => { if (!e.Cancel && view.CanAutoSaveWorkspace) view.Try(view.SaveWorkspace); };
            window.Closed += (_, _) => view.Dispose();
            desktop.MainWindow = window;
            view.Try(() => view.ConfigureWorkspacePersistence(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Oasis", "AvaloniaEditor", "workspace.json")));
            if (Program.Project is not null && !string.Equals(session.ProjectFilePath, Program.Project, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                view.Try(() => view.OpenProject(Program.Project));
            if (!Program.IsHeadless) view.Try(() => view.ConfigureWelcomePage(new RecentProjectService(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Oasis", "AvaloniaEditor", "recent-projects.json"))));
        }
        base.OnFrameworkInitializationCompleted();
    }
}

public static class Program
{
    public static string Workspace { get; private set; } = Environment.CurrentDirectory;
    public static string? Project { get; private set; }
    internal static bool IsHeadless { get; private set; }
    [STAThread]
    public static void Main(string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--workspace" when i + 1 < args.Length: Workspace = Path.GetFullPath(args[++i]); break;
                case "--project" when i + 1 < args.Length: Project = Path.GetFullPath(args[++i]); break;
                default: throw new ArgumentException("Usage: OasisEditor [--workspace <directory>] [--project <file>]");
            }
        }
        BuildDesktop().StartWithClassicDesktopLifetime([]);
    }

    public static AppBuilder BuildDesktop()
    {
        IsHeadless = false;
        return AppBuilder.Configure<EditorApplication>().UsePlatformDetect().WithInterFont();
    }
    public static AppBuilder BuildHeadless()
    {
        IsHeadless = true;
        return AppBuilder.Configure<EditorApplication>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .UseSkia().WithInterFont();
    }
}

/// <summary>Renders the real workspace with the Skia backend on a headless window; never accesses the desktop.</summary>
public sealed class WorkspaceCapture : IDisposable
{
    private readonly Window _window;
    public WorkspaceView View { get; }
    public WorkspaceCapture(AuthoringSession session, FaceAuthoringSession? faces = null)
    {
        if (!Program.IsHeadless)
            throw new InvalidOperationException("WorkspaceCapture requires the headless platform; desktop capture is not supported here.");
        View = new WorkspaceView(session, faces);
        _window = new Window { Content = View, Width = 1400, Height = 900 };
        _window.Show(); // Headless platform only: no native window exists.
    }

    public byte[] Capture(int width = 1400, int height = 900, string theme = "Dark")
    {
        if (width is < 960 or > 1920 || height is < 640 or > 1200) throw new ArgumentOutOfRangeException(nameof(width));
        Application.Current!.RequestedThemeVariant = theme.ToLowerInvariant() switch
        {
            "dark" => ThemeVariant.Dark, "light" => ThemeVariant.Light, "system" => ThemeVariant.Default,
            _ => throw new ArgumentException("Theme must be System, Light or Dark.")
        };
        _window.Width = width; _window.Height = height;
        _window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(2);
        using var bitmap = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96, 96));
        bitmap.Render(_window);
        using var stream = new MemoryStream();
        bitmap.Save(stream, new PngBitmapEncoderOptions());
        return stream.ToArray();
    }

    /// <summary>Opens the tab menu through the live Dock tab's headless pointer path.</summary>
    public ContextMenu OpenTabMenu(string paneId)
    {
        if (!Program.IsHeadless)
            throw new InvalidOperationException("Live tab menu inspection requires the isolated headless host.");

        // Redocking can defer creation of the destination tab strip until the
        // next rendered frame. Inspect the same fully rendered surface we capture.
        _ = Capture(Math.Clamp((int)_window.ClientSize.Width, 960, 1920),
            Math.Clamp((int)_window.ClientSize.Height, 640, 1200),
            _window.ActualThemeVariant == ThemeVariant.Light ? "Light" : "Dark");
        _ = CaptureFloatingWindows();

        // Validate the id before searching the rendered surfaces, and account for a
        // tab temporarily represented by the workspace's maximised surface.
        _ = View.CreateTabContextMenu(paneId);
        var maximised = View.InspectTabPresentation().MaximisedPaneId == paneId;
        var hosts = new[] { _window }.Concat(View.FloatingWorkspaceWindows).Distinct().ToArray();
        foreach (var host in hosts)
        {
            host.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick(2);
            Control? target = null;
            if (maximised)
                target = host.GetVisualDescendants().OfType<Control>().FirstOrDefault(c => c.Name == "MaximisedTabHeader");

            target ??= host.GetVisualDescendants().OfType<Control>()
                .FirstOrDefault(c => c.IsEffectivelyVisible && c.Bounds.Width > 0 && c.Bounds.Height > 0
                    && c is DocumentTabStripItem or ToolTabStripItem
                    && (c.DataContext as IDockable)?.Id == paneId);

            Control? grip = null;
            if (target is null)
            {
                grip = host.GetVisualDescendants().OfType<Control>()
                    .FirstOrDefault(c => c.IsEffectivelyVisible && c.Bounds.Width > 0 && c.Bounds.Height > 0 && c.Name == "PART_Grip"
                        && c.GetVisualAncestors().OfType<ToolChromeControl>()
                            .Any(chrome => DockableId(chrome.DataContext) == paneId));
                if (grip is not null)
                    target = grip.GetVisualAncestors().OfType<ToolChromeControl>().FirstOrDefault();
            }

            if (target is null) continue;
            var inputTarget = grip ?? target;
            var point = inputTarget.TranslatePoint(
                new Point(inputTarget.Bounds.Width / 2, inputTarget.Bounds.Height / 2), host);
            if (point is null) continue;
            host.MouseDown(point.Value, MouseButton.Right);
            host.MouseUp(point.Value, MouseButton.Right);
            Dispatcher.UIThread.RunJobs();
            var menu = target.ContextMenu;
            if (menu?.IsOpen == true) return menu;
        }

        throw new InvalidOperationException($"No visible live tab menu opened for '{paneId}'.");

        static string? DockableId(object? context)
            => context is IDockable dockable ? dockable is IDock dock ? dock.ActiveDockable?.Id : dockable.Id : null;
    }

    public async Task<byte[]> CaptureFaceArtworkEditor(AuthoringSession session, FaceAuthoringSession faces, bool calibrationStage, string theme)
    {
        if (!Program.IsHeadless) throw new InvalidOperationException("Recipe capture requires the isolated headless host.");
        _ = Capture(theme: theme);
        Window? dialog = null;
        var task = FaceArtworkRecipeEditor.ShowAsync(_window, session, faces, window => dialog = window, calibrationStage);
        if (dialog is null) { await task; throw new InvalidOperationException("Artwork editor could not be opened."); }
        try
        {
            dialog.UpdateLayout(); Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(2);
            using var bitmap = new RenderTargetBitmap(new PixelSize((int)dialog.Width, (int)dialog.Height), new Vector(96, 96));
            bitmap.Render(dialog); using var stream = new MemoryStream(); bitmap.Save(stream); return stream.ToArray();
        }
        finally { dialog.Close(); await task; }
    }

    public IReadOnlyList<byte[]> CaptureFloatingWindows()
    {
        var images = new List<byte[]>();
        Dispatcher.UIThread.RunJobs();
        foreach (var window in View.FloatingWorkspaceWindows)
        {
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick(2);
            var width = Math.Clamp((int)Math.Ceiling(window.ClientSize.Width), 1, 1920);
            var height = Math.Clamp((int)Math.Ceiling(window.ClientSize.Height), 1, 1200);
            using var bitmap = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96, 96));
            bitmap.Render(window);
            using var output = new MemoryStream(); bitmap.Save(output, new PngBitmapEncoderOptions()); images.Add(output.ToArray());
        }
        return images;
    }

    public void Dispose() { View.Dispose(); _window.Close(); }
}
