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
    public void MeasureArtworkOperationsAcrossWorkerPolicies()
    {
        if (Environment.GetEnvironmentVariable("OASIS_IMAGE_BENCHMARK") != "1") return;

        Measure(1024, 1024);
        Measure(2048, 2048);
        if (Environment.GetEnvironmentVariable("OASIS_IMAGE_BENCHMARK_16MP") == "1") Measure(4096, 4096);
    }

    [Fact]
    [Trait("Category", "Performance")]
    public void MeasureGeneratedLampMaskAcrossWorkerPolicies()
    {
        if (Environment.GetEnvironmentVariable("OASIS_IMAGE_BENCHMARK") != "1") return;
        const int width = 2048, height = 2048, lampCount = 40;
        using var source = new SKBitmap(320, 240, SKColorType.Rgba8888, SKAlphaType.Premul);
        source.Erase(new SKColor(255, 255, 255, 180));
        var shape = new PanelFaceSourceShapeModel { TopLeft = new() { X = 0, Y = 0 }, TopRight = new() { X = width, Y = 24 }, BottomRight = new() { X = width - 18, Y = height }, BottomLeft = new() { X = 12, Y = height - 16 } };
        var lamp = new PanelElementModel { X = 0, Y = 0, Width = width, Height = height };
        var windows = Enumerable.Range(0, lampCount).Select(index => new FaceLampWindowElement
        {
            X = (index % 8) * 240 - 20, Y = (index / 8) * 390 - 10, Width = 320, Height = 480
        }).ToArray();
        ReportLampMask("Generated lamp mask", width, height, lampCount, options =>
        {
            var mask = new byte[width * height];
            foreach (var window in windows)
                FaceGenerationService.CompositeSourceShapeLampMask(mask, width, height, shape, lamp, source, window, 32, options);
        });
    }

    private void ReportLampMask(string operation, int width, int height, int lampCount, Action<ImageProcessingExecutionOptions> run)
    {
        var policies = new[] { ("1 worker", new ImageProcessingExecutionOptions(1)),
            ("Auto", ImageProcessingExecutionPolicy.Resolve(new ProcessingPreferences(), Environment.ProcessorCount)),
            ("Maximum", ImageProcessingExecutionPolicy.Resolve(new ProcessingPreferences { CpuMode = CpuImageProcessingMode.Maximum }, Environment.ProcessorCount)) };
        run(policies[0].Item2);
        var baseline = MedianMilliseconds(() => { run(policies[0].Item2); return new SKBitmap(); });
        output.WriteLine($"{operation} {width}x{height}; {lampCount} lamps; logical processors {Environment.ProcessorCount}");
        foreach (var (mode, options) in policies)
        {
            var median = mode == "1 worker" ? baseline : MedianMilliseconds(() => { run(options); return new SKBitmap(); });
            output.WriteLine($"  {mode}: {options.MaxDegreeOfParallelism} workers, median {median:F1} ms, {baseline / median:F2}x vs 1 worker");
        }
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

        Report("Rectify", width, height, options => PerspectiveRasterizer.Rectify(source, quad, width, height, options));
        Report("Sharpen", width, height, options => FaceArtworkSharpeningService.Apply(source, settings, options));
        Report("Calibration", width, height, options => new FaceArtworkProcessingPipeline().Evaluate(source, pipeline, executionOptions: options));
    }

    private void Report(string operation, int width, int height, Func<ImageProcessingExecutionOptions, SKBitmap> run)
    {
        var policies = new[] { ("1 worker", new ImageProcessingExecutionOptions(1)),
            ("Auto", ImageProcessingExecutionPolicy.Resolve(new ProcessingPreferences(), Environment.ProcessorCount)),
            ("Maximum", ImageProcessingExecutionPolicy.Resolve(new ProcessingPreferences { CpuMode = CpuImageProcessingMode.Maximum }, Environment.ProcessorCount)) };
        using (run(policies[0].Item2)) { }
        var baseline = MedianMilliseconds(() => run(policies[0].Item2));
        output.WriteLine($"{operation} {width}x{height}; logical processors {Environment.ProcessorCount}");
        foreach (var (mode, options) in policies)
        {
            var median = mode == "1 worker" ? baseline : MedianMilliseconds(() => run(options));
            output.WriteLine($"  {mode}: {options.MaxDegreeOfParallelism} workers, median {median:F1} ms, {baseline / median:F2}x vs 1 worker");
        }
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
