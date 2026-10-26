using System.Diagnostics;
using System.IO;
using System.Text.Json;
using OasisEditor;
using OasisEditor.Rendering;
using OpenTK.Graphics.OpenGL4;
using SkiaSharp;

internal static class AlignmentBenchmark
{
    public static void Run(GRContext context, string root)
    {
        using var bitmap = new SKBitmap(4096, 3072);
        using (var canvas = new SKCanvas(bitmap))
        using (var paint = new SKPaint { IsAntialias = true })
        {
            canvas.Clear(SKColors.White);
            for (var y = 0; y < 3072; y += 32) for (var x = 0; x < 4096; x += 32)
                { paint.Color = new SKColor((byte)(x % 256), (byte)(y % 256), (byte)((x + y) % 256)); canvas.DrawRect(x, y, 24, 24, paint); }
        }
        using var source = SKImage.FromBitmap(bitmap);
        using (var data = source.Encode(SKEncodedImageFormat.Png, 100)) using (var file = File.Create(Path.Combine(root, "source-4096.png"))) data.SaveTo(file);
        var value = new FaceArtworkOverrideModel
        {
            AssetPath = Path.Combine(root, "source-4096.png"),
            PixelWidth = 4096,
            PixelHeight = 3072,
            PerspectiveRegistration = new()
            { TopLeft = new() { X = .1, Y = .08 }, TopRight = new() { X = .95, Y = .03 }, BottomRight = new() { X = .9, Y = .96 }, BottomLeft = new() { X = .02, Y = .9 } }
        };
        var reports = new List<object>();
        foreach (var backend in new[] { "CPU", "GPU" })
        {
            var info = new SKImageInfo(1920, 1080);
            using var surface = backend == "CPU" ? SKSurface.Create(info) : SKSurface.Create(context, false, info);
            var samples = new List<double>();
            for (var frame = -15; frame < 100; frame++)
            {
                var watch = Stopwatch.StartNew(); surface.Canvas.Clear(SKColors.Black);
                FaceArtworkAlignmentRenderer.Draw(surface.Canvas, source, value, SKRect.Create(100 + frame % 10, 50, 1500, 950), .5 + frame % 5 * .1);
                surface.Canvas.Flush(); if (backend == "GPU") { context.Flush(); GL.Finish(); }
                watch.Stop(); if (frame >= 0) samples.Add(watch.Elapsed.TotalMilliseconds);
            }
            samples.Sort(); reports.Add(new { Backend = backend, SourceWidth = 4096, SourceHeight = 3072, MedianMs = samples[50], P95Ms = samples[95] });
            using var image = surface.Snapshot(); using var png = image.Encode(SKEncodedImageFormat.Png, 100); using var file = File.Create(Path.Combine(root, backend + ".png")); png.SaveTo(file);
        }
        var prepare = Stopwatch.StartNew(); var preview = FaceArtworkOverridePreviewService.Generate(value.AssetPath, value, default); prepare.Stop();
        reports.Add(new { Backend = "Previous bounded preview preparation (decode/rectify/PNG; not frame time)", preview.Width, preview.Height, Milliseconds = prepare.Elapsed.TotalMilliseconds });
        var json = JsonSerializer.Serialize(reports, new JsonSerializerOptions { WriteIndented = true }); File.WriteAllText(Path.Combine(root, "results.json"), json); Console.WriteLine(json);
    }
}
