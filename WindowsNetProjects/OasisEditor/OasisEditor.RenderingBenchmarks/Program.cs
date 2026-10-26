using System.Diagnostics;
using System.IO;
using System.Text.Json;
using OasisEditor;
using OasisEditor.Automation;
using OasisEditor.Rendering;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using SkiaSharp;

// Opt-in harness. Only fresh disposable projects are accepted. No visible window or desktop input.
internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--wpf") return ViewportSmoke.Run(args.Skip(1).ToArray());
        if (args.Length > 0 && args[0] == "--wpf-alignment") return AlignmentSmoke.Run(args.Skip(1).ToArray());
        if (args.Length < 2) { Console.Error.WriteLine("Usage: <new-output-directory> <source.fml> [...]"); return 2; }
        var root = Path.GetFullPath(args[0]);
        if (Directory.Exists(root)) throw new IOException("Use a fresh benchmark output directory.");
        Directory.CreateDirectory(root);
        var reports = new List<object>();
        using var window = new NativeWindow(new NativeWindowSettings { ClientSize = new Vector2i(32, 32), StartVisible = false, StartFocused = false, API = ContextAPI.OpenGL, APIVersion = new Version(3, 3), Profile = ContextProfile.Core });
        window.Context.MakeCurrent();
        using var gl = GRGlInterface.Create();
        using var context = GRContext.CreateGl(gl) ?? throw new InvalidOperationException("GPU Skia context unavailable.");
        var hardware = new { Renderer = GL.GetString(StringName.Renderer), Vendor = GL.GetString(StringName.Vendor), Version = GL.GetString(StringName.Version), Skia = typeof(SKCanvas).Assembly.GetName().Version!.ToString() };
        Console.WriteLine(JsonSerializer.Serialize(hardware));
        if (args[1] == "--alignment") { AlignmentBenchmark.Run(context, root); return 0; }
        foreach (var source in args.Skip(1))
        {
            var workspace = Path.Combine(root, Path.GetFileNameWithoutExtension(source)); Directory.CreateDirectory(workspace);
            var copy = Path.Combine(workspace, Path.GetFileName(source)); File.Copy(source, copy);
            var session = new McpEditorSession(workspace); var project = session.CreateProject("Benchmark"); var panel = session.CreatePanel("Imported");
            var import = session.ImportFml(panel.DocumentId, copy);
            File.WriteAllText(Path.Combine(workspace, "import.json"), JsonSerializer.Serialize(import, new JsonSerializerOptions { WriteIndented = true }));
            if (!import.Succeeded || import.ImportedCount == 0) throw new InvalidOperationException("Import failed or empty: " + string.Join("; ", import.Errors));
            panel = session.SavePanel(panel.DocumentId);
            ProjectAssetPathResolver.ProjectDirectoryPath = Path.GetDirectoryName(project.ProjectFile);
            var opened = DocumentWorkspaceViewModel.BuildOpenDocumentData(panel.Path, File.ReadAllText(panel.Path));
            var document = new DocumentTabViewModel(EditorDocument.CreateFromFile(panel.Path, opened.Summary), opened.PanelLayoutJson);
            var elements = document.GetPanelElements(); var runtime = document.RuntimeState; var renderer = Panel2DRendererFactory.Create("Benchmark");
            foreach (var size in new[] { new SKSizeI(1280, 720), new SKSizeI(1920, 1080) })
                foreach (var zoom in new[] { 0.5, 1.0, 4.0 })
                {
                    var info = new SKImageInfo(size.Width, size.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
                    using var cpu = SKSurface.Create(info); using var gpu = SKSurface.Create(context, false, info) ?? throw new InvalidOperationException("GPU surface unavailable.");
                    foreach (var backend in new[] { "CPU", "GPU" })
                    {
                        var surface = backend == "CPU" ? cpu : gpu; var samples = new List<double>();
                        for (var frame = -15; frame < 100; frame++)
                        {
                            var viewport = new PanelViewportTransform(zoom, -(frame % 20) * 3, -(frame % 13) * 2);
                            var watch = Stopwatch.StartNew(); var canvas = surface.Canvas; canvas.Clear(SKColors.Black);
                            canvas.Save(); canvas.Translate((float)viewport.PanX, (float)viewport.PanY); canvas.Scale((float)zoom);
                            renderer.Render(canvas, elements, runtime, viewport); canvas.Restore(); canvas.Flush();
                            if (backend == "GPU") { context.Flush(); GL.Finish(); }
                            watch.Stop(); if (frame >= 0) samples.Add(watch.Elapsed.TotalMilliseconds);
                        }
                        samples.Sort(); var report = new { Source = Path.GetFileName(source), Elements = elements.Count, Width = size.Width, Height = size.Height, Zoom = zoom, Backend = backend, MedianMs = samples[50], P95Ms = samples[95] };
                        reports.Add(report); Console.WriteLine(JsonSerializer.Serialize(report));
                        using var image = surface.Snapshot(); using var png = image.Encode(SKEncodedImageFormat.Png, 100);
                        using var file = File.Create(Path.Combine(workspace, $"{backend}-{size.Width}-{zoom}.png")); png.SaveTo(file);
                    }
                }
        }
        File.WriteAllText(Path.Combine(root, "results.json"), JsonSerializer.Serialize(new { hardware, reports }, new JsonSerializerOptions { WriteIndented = true })); return 0;
    }
}
