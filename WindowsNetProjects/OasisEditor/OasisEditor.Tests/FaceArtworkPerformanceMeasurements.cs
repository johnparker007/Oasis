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
        var watch = Stopwatch.StartNew();
        using var rectified = PerspectiveRasterizer.Rectify(source, quad, width, height);
        watch.Stop(); output.WriteLine($"Rectify {width}x{height}: {watch.ElapsedMilliseconds} ms");

        watch.Restart();
        using var sharpened = FaceArtworkSharpeningService.Apply(rectified, new FaceGenerationSettingsModel
            { PostWarpSharpeningEnabled = true, PostWarpSharpeningAmount = 1, PostWarpSharpeningRadiusPixels = .75 });
        watch.Stop(); output.WriteLine($"Sharpen {width}x{height}: {watch.ElapsedMilliseconds} ms");

        watch.Restart();
        using var calibrated = new FaceArtworkProcessingPipeline().Evaluate(sharpened, new ImageProcessingPipelineModel
        {
            Operations = [new ArtworkCalibrationOperationModel
            {
                CorrectSpatialBrightness = false, CorrectSpatialColor = false,
                BlackReference = new CalibrationReferenceModel { ManualEnabled = true, ManualColor = "#FF101010" },
                WhiteReference = new CalibrationReferenceModel { ManualEnabled = true, ManualColor = "#FFE0E0E0" }
            }]
        });
        watch.Stop(); output.WriteLine($"Calibration {width}x{height}: {watch.ElapsedMilliseconds} ms");
    }
}
