using System.IO;
using System.Windows;
using System.Windows.Threading;
using OasisEditor;
using OasisEditor.Views;
using SkiaSharp;

internal static class ViewportSmoke
{
    public static int Run(string[] args)
    {
        if (args.Length != 2) throw new ArgumentException("--wpf <asset.panel2d> <new-output-directory>");
        if (Directory.Exists(args[1])) throw new IOException("Use a fresh output directory.");
        Directory.CreateDirectory(args[1]);
        System.Diagnostics.Trace.Listeners.Add(new System.Diagnostics.TextWriterTraceListener(Console.Error));
        System.Diagnostics.Trace.AutoFlush = true;
        var app = App.CreateAutomationHost(); app.ShutdownMode = ShutdownMode.OnExplicitShutdown; app.InitializeComponent();
        var themes = new ApplicationThemeService(); themes.EnsureFluentThemeResources(app);
        var panel = Path.GetFullPath(args[0]);
        ProjectAssetPathResolver.ProjectDirectoryPath = Directory.GetParent(panel)!.Parent!.Parent!.Parent!.FullName;
        var opened = DocumentWorkspaceViewModel.BuildOpenDocumentData(panel, File.ReadAllText(panel));
        var doc = new DocumentTabViewModel(EditorDocument.CreateFromFile(panel, opened.Summary), opened.PanelLayoutJson);
        var window = new Window { Width = 1280, Height = 760, Left = -20000, Top = -20000, ShowActivated = false, ShowInTaskbar = false };
        var view = new SkiaPanel2DEditView { DataContext = doc };
        var surface = (EditorSkiaSurface)view.FindName("EditSkiaSurface");
        var paints = 0;
        surface.PaintSurface += (_, e) =>
        {
            paints++;
            using var image = e.Surface.Snapshot(); using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var file = File.Create(Path.Combine(args[1], "latest.png")); data.SaveTo(file);
        };
        window.Content = view; window.Show();
        foreach (var theme in new[] { ThemePreference.Dark, ThemePreference.Light, ThemePreference.System })
        {
            themes.ApplyTheme(app, theme);
            for (var cycle = 0; cycle < 3; cycle++)
            {
                window.Content = view; window.Width = 1280 + cycle * 100; window.Height = 760 + cycle * 40;
                view.FitToViewport(); doc.PanelZoom = cycle == 0 ? doc.PanelZoom : cycle; doc.PanelPanX = cycle * 31;
                var before = paints; surface.InvalidateVisual(); PumpUntil(() => paints > before); Pump();
                var expectGpu = !string.Equals(Environment.GetEnvironmentVariable("OASIS_RENDER_BACKEND"), "cpu", StringComparison.OrdinalIgnoreCase);
                if (surface.IsGpuBacked != expectGpu) throw new InvalidOperationException("Unexpected rendering backend.");
                File.Copy(Path.Combine(args[1], "latest.png"), Path.Combine(args[1], $"{theme}-{cycle}.png"));
                window.Content = null; Pump();
            }
        }
        window.Close(); app.Shutdown(); Console.WriteLine($"WPF viewport smoke passed: {paints} paints, resize/zoom/pan/unload/reload, all themes."); return 0;
    }
    private static void PumpUntil(Func<bool> done)
    {
        var timeout = DateTime.UtcNow.AddSeconds(10);
        while (!done()) { if (DateTime.UtcNow > timeout) throw new TimeoutException("Viewport did not paint."); Pump(); }
    }
    private static void Pump()
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(TimeSpan.FromMilliseconds(30), DispatcherPriority.Background, (_, _) => frame.Continue = false, Dispatcher.CurrentDispatcher);
        Dispatcher.PushFrame(frame); timer.Stop();
    }
}
