using System.Diagnostics;
using SkiaSharp;
using Xunit;
using Xunit.Abstractions;

namespace OasisEditor.Tests;

/// <summary>
/// Opt-in development timing harness. Run with OASIS_IMAGE_BENCHMARK=1; add
/// OASIS_IMAGE_BENCHMARK_16MP=1 for the memory-intensive 4096-square sharpening case.
/// It deliberately has no timing assertions because developer and CI hardware vary.
/// </summary>
public sealed class FaceArtworkPerformanceMeasurements(ITestOutputHelper output)
{
    [Fact]
    [Trait("Category", "Performance")]
    public void MeasureSingleThreadedArtworkOperations()
    {
        if (Environment.GetEnvironmentVariable("OASIS_IMAGE_BENCHMARK") != "1") return;

        Measure(1024, 1024);
        Measure(2048, 2048);
        if (Environment.GetEnvironmentVariable("OASIS_IMAGE_BENCHMARK_16MP") == "1") Measure(4096, 4096);
    }

    private void Measure(int width, int height)
    {
        using var source = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(source))
        using (var paint = new SKPaint())
        {
            paint.Shader = SKShader.CreateLinearGradient(new SKPoint(0, 0), new SKPoint(width, height),
                [new SKColor(20, 50, 100, 80), new SKColor(240, 190, 40)], null, SKShaderTileMode.Clamp);
            canvas.DrawRect(0, 0, width, height, paint);
        }
        var quad = new FacePointModel[] { new() { X = 0, Y = 0 }, new() { X = width, Y = 8 },
            new() { X = width - 12, Y = height }, new() { X = 6, Y = height - 5 } };
        var settings = new FaceGenerationSettingsModel
            { PostWarpSharpeningEnabled = true, PostWarpSharpeningAmount = 1, PostWarpSharpeningRadiusPixels = .75 };
        var pipeline = new ImageProcessingPipelineModel
        {
            Operations = [new ArtworkCalibrationOperationModel
            {
                CorrectSpatialBrightness = false, CorrectSpatialColor = false,
                BlackReference = new CalibrationReferenceModel { ManualEnabled = true, ManualColor = "#FF101010" },
                WhiteReference = new CalibrationReferenceModel { ManualEnabled = true, ManualColor = "#FFE0E0E0" }
            }]
        };

        Report("Rectify", width, height,
            () => LegacyPerspectiveRasterizer.Rectify(source, quad, width, height),
            () => PerspectiveRasterizer.Rectify(source, quad, width, height));
        Report("Sharpen", width, height,
            () => LegacyFaceArtworkSharpeningService.Apply(source, settings),
            () => FaceArtworkSharpeningService.Apply(source, settings));
        Report("Calibration", width, height,
            () => new LegacyFaceArtworkProcessingPipeline().Evaluate(source, pipeline),
            () => new FaceArtworkProcessingPipeline().Evaluate(source, pipeline));
    }

    private void Report(string operation, int width, int height, Func<SKBitmap> legacy, Func<SKBitmap> current)
    {
        using (legacy()) { } // JIT and native-code warm-up are excluded from both measurements.
        using (current()) { }
        var legacyMedian = MedianMilliseconds(legacy);
        var currentMedian = MedianMilliseconds(current);
        output.WriteLine($"{operation} {width}x{height}: legacy {legacyMedian:F1} ms, current {currentMedian:F1} ms, " +
            $"speed-up {legacyMedian / currentMedian:F2}x");
    }

    private static double MedianMilliseconds(Func<SKBitmap> operation)
    {
        var samples = new double[3];
        for (var iteration = 0; iteration < samples.Length; iteration++)
        {
            var watch = Stopwatch.StartNew();
            using var result = operation();
            watch.Stop();
            samples[iteration] = watch.Elapsed.TotalMilliseconds;
        }
        Array.Sort(samples);
        return samples[samples.Length / 2];
    }
}
