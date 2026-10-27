using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.Controls.Templates;
using Dock.Model.Core;

namespace OasisEditor.Avalonia;

public sealed class EditorApplication : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        DataTemplates.Add(new FuncDataTemplate<IDockable>((item, _) => new ContentControl { Content = item?.Context }));
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var session = new AuthoringSession(Program.Workspace);
            var window = new Window { Width = 1440, Height = 900, MinWidth = 960, MinHeight = 640 };
            var view = new WorkspaceView(session);
            window.Content = view;
            window.Title = "Oasis Editor — Avalonia";
            view.StateChanged += () => window.Title = $"{view.ActiveTitle}{(view.HasUnsavedChanges ? " *" : "")} — {session.ProjectName ?? "Oasis Editor"}";
            window.Closing += (_, e) => { if (view.HasUnsavedChanges) { e.Cancel = true; view.SetStatus("Save or undo your changes before closing."); } };
            desktop.MainWindow = window;
            if (Program.Project is not null) view.Try(() => view.OpenProject(Program.Project));
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
                default: throw new ArgumentException("Usage: OasisEditor.Avalonia [--workspace <directory>] [--project <file>]");
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

    public void Dispose() { View.Dispose(); _window.Close(); }
}
