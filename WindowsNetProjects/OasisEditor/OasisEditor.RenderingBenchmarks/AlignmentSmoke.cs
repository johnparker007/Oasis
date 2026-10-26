using System.IO;
using System.Windows;
using System.Windows.Threading;
using OasisEditor;
using OasisEditor.Views;
using SkiaSharp;

internal static class AlignmentSmoke
{
    public static int Run(string[] args)
    {
        if (args.Length != 2) throw new ArgumentException("--wpf-alignment <source.png> <new-output-directory>");
        if (Directory.Exists(args[1])) throw new IOException("Use a fresh output directory."); Directory.CreateDirectory(args[1]);
        System.Diagnostics.Trace.Listeners.Add(new System.Diagnostics.TextWriterTraceListener(Console.Error)); System.Diagnostics.Trace.AutoFlush = true;
        var app = App.CreateAutomationHost(); app.ShutdownMode = ShutdownMode.OnExplicitShutdown; app.InitializeComponent();
        var themes = new ApplicationThemeService(); themes.EnsureFluentThemeResources(app); themes.ApplyTheme(app, ThemePreference.Dark);
        var source = Path.GetFullPath(args[0]); using var bitmap = SKBitmap.Decode(source);
        using var doc = new DocumentTabViewModel(EditorDocument.CreateFaceStub("Alignment benchmark"), faceDocumentJson: FaceDocumentStorage.Serialize(new FaceDocumentModel
        {
            Title = "Alignment benchmark",
            Artwork = new FaceArtworkModel
            {
                BaseAssetPath = source,
                Override = new FaceArtworkOverrideModel
                {
                    AssetPath = source,
                    PixelWidth = bitmap.Width,
                    PixelHeight = bitmap.Height,
                    PerspectiveRegistration = new()
                    { TopLeft = new() { X = .1, Y = .08 }, TopRight = new() { X = .95, Y = .03 }, BottomRight = new() { X = .9, Y = .96 }, BottomLeft = new() { X = .02, Y = .9 } }
                }
            }
        }));
        var view = new FaceArtworkAlignmentView { DataContext = doc };
        doc.SetProjectAccessor(() => new EditorProject { Name = "Disposable", ProjectDirectory = args[1], ProjectFilePath = Path.Combine(args[1], "Disposable.oasisproj"), AssetsDirectory = Path.Combine(args[1], "Assets"), GeneratedDirectory = Path.Combine(args[1], "Generated") });
        var window = new Window { Width = 1280, Height = 800, Left = -20000, Top = -20000, ShowActivated = false, ShowInTaskbar = false, Content = view };
        window.Show();
        var paints = 0; view.PreviewSurface.PaintSurface += (_, e) =>
        { if (view.LoadedOverrideWidth == 0) return; paints++; using var image = e.Surface.Snapshot(); using var png = image.Encode(SKEncodedImageFormat.Png, 100); using var file = File.Create(Path.Combine(args[1], "alignment.png")); png.SaveTo(file); };
        var end = DateTime.UtcNow.AddSeconds(15);
        while (paints == 0)
        {
            if (DateTime.UtcNow > end) throw new TimeoutException("Alignment preview did not load/paint.");
            var frame = new DispatcherFrame(); var timer = new DispatcherTimer(TimeSpan.FromMilliseconds(30), DispatcherPriority.Background, (_, _) => frame.Continue = false, Dispatcher.CurrentDispatcher); Dispatcher.PushFrame(frame); timer.Stop();
        }
        if (view.LoadedOverrideWidth != bitmap.Width) throw new InvalidOperationException("Source was downsampled.");
        if (!view.PreviewSurface.IsGpuBacked) throw new InvalidOperationException("Alignment preview did not use GPU.");
        window.Close(); app.Shutdown(); Console.WriteLine($"WPF alignment passed: full {bitmap.Width}x{bitmap.Height} source, GPU rendering, asynchronous loading."); return 0;
    }
}
