using SkiaSharp;
using Xunit;

namespace OasisEditor.Tests;

public sealed class FaceArtworkProcessingPipelineTests
{
    [Fact]
    public void Serialization_RoundTripsOrderedTypedOperationsAndClampsParameters()
    {
        var model = DocumentWithPipeline([
            Levels("first", false, 120, [new() { X = -1, Y = .25 }], [new() { X = .8, Y = 2 }]),
            new BlackWhiteLevelsOperationModel
            {
                Id = "second", Enabled = true, Strength = 40,
                BlackSamples = [new() { X = .1, Y = .2 }], WhiteSamples = [new() { X = .9, Y = .8 }],
                BlackManualEnabled = true, BlackManualColor = "#FF123456",
                WhiteManualEnabled = true, WhiteManualColor = "#FFABCDEF"
            }]);

        Assert.True(FaceDocumentStorage.TryRead(FaceDocumentStorage.Serialize(model), out var file));
        var operations = FaceDocumentStorage.ToModel(file).Artwork!.ProcessingPipeline.Operations.Cast<BlackWhiteLevelsOperationModel>().ToArray();

        Assert.Equal(["first", "second"], operations.Select(operation => operation.Id));
        Assert.False(operations[0].Enabled);
        Assert.Equal(100, operations[0].Strength);
        Assert.Equal(0, operations[0].BlackSamples[0].X);
        Assert.Equal(1, operations[0].WhiteSamples[0].Y);
        Assert.True(operations[1].BlackManualEnabled);
        Assert.Equal("#FF123456", operations[1].BlackManualColor);
        Assert.True(operations[1].WhiteManualEnabled);
        Assert.Equal("#FFABCDEF", operations[1].WhiteManualColor);
    }

    [Fact]
    public void Evaluate_IncompleteDisabledAndZeroStrengthOperationsAreNoOps()
    {
        using var input = Gradient();
        var evaluator = new FaceArtworkProcessingPipeline();
        foreach (var operation in new[]
        {
            Levels("incomplete", true, 100, [], []),
            Levels("disabled", false, 100, [new() { X = 0, Y = 0 }], [new() { X = 1, Y = 0 }]),
            Levels("zero", true, 0, [new() { X = 0, Y = 0 }], [new() { X = 1, Y = 0 }])
        })
        {
            using var output = evaluator.Evaluate(input, new ImageProcessingPipelineModel { Operations = [operation] });
            AssertPixelsEqual(input, output);
        }
    }

    [Fact]
    public void Evaluate_ValidBoundarySamplesExpandTonalRangeAndPreserveAlpha()
    {
        using var input = Gradient();
        using var output = new FaceArtworkProcessingPipeline().Evaluate(input, new ImageProcessingPipelineModel
        {
            Operations = [Levels("levels", true, 100, [new() { X = 0, Y = 0 }], [new() { X = 1, Y = 1 }])]
        });

        Assert.True(output.GetPixel(0, 0).Red < input.GetPixel(0, 0).Red);
        Assert.True(output.GetPixel(8, 8).Red > input.GetPixel(8, 8).Red);
        Assert.Equal(input.GetPixel(4, 4).Alpha, output.GetPixel(4, 4).Alpha);
    }

    [Fact]
    public void Evaluate_CanReturnIntermediatePipelineResultsInAuthoredOrder()
    {
        using var input = Gradient();
        // Partial strengths avoid both operations saturating the synthetic gradient to the
        // same endpoints, while still proving that each operation consumes its predecessor.
        var first = Levels("first", true, 40, [new() { X = 0, Y = 0 }], [new() { X = .65, Y = 0 }]);
        var second = Levels("second", true, 75, [new() { X = .35, Y = 0 }], [new() { X = 1, Y = 0 }]);
        var pipeline = new ImageProcessingPipelineModel { Operations = [first, second] };
        var evaluator = new FaceArtworkProcessingPipeline();
        using var afterFirst = evaluator.Evaluate(input, pipeline, 1);
        using var final = evaluator.Evaluate(input, pipeline);
        using var reverse = evaluator.Evaluate(input, new ImageProcessingPipelineModel { Operations = [second, first] });

        AssertBitmapsDiffer(afterFirst, final);
        AssertBitmapsDiffer(reverse, final);
    }

    [Fact]
    public void ReferenceColors_SampleExactPixelsAndAverageMultipleMarkersInLinearLight()
    {
        using var input = new SKBitmap(3, 1);
        input.SetPixel(0, 0, SKColors.Red);
        input.SetPixel(1, 0, SKColors.Lime); // A deliberately different immediate neighbour.
        input.SetPixel(2, 0, SKColors.Blue);
        var operation = Levels("exact", true, 100,
            [new() { X = 0, Y = 0 }, new() { X = 1, Y = 0 }],
            [new() { X = .5, Y = 0 }]);

        Assert.True(FaceArtworkProcessingPipeline.TryResolveReferenceColors(input, operation, out var black, out var white));
        Assert.Equal("#FFBC00BC", black);
        Assert.Equal("#FF00FF00", white);
    }

    [Fact]
    public void ReferenceColors_OneMarkerUsesOnlyThatExactEdgePixel()
    {
        using var input = new SKBitmap(2, 2);
        input.Erase(SKColors.Magenta);
        input.SetPixel(0, 0, new SKColor(12, 34, 56));
        input.SetPixel(1, 1, new SKColor(210, 220, 230));
        var operation = Levels("edges", true, 100, [new() { X = 0, Y = 0 }], [new() { X = 1, Y = 1 }]);

        Assert.True(FaceArtworkProcessingPipeline.TryResolveReferenceColors(input, operation, out var black, out var white));
        Assert.Equal("#FF0C2238", black);
        Assert.Equal("#FFD2DCE6", white);
    }

    [Fact]
    public void ReferenceColors_ManualOverridesAreIndependentAndTakePrecedence()
    {
        using var input = Gradient();
        var operation = new BlackWhiteLevelsOperationModel
        {
            BlackSamples = [new() { X = 0, Y = 0 }], WhiteSamples = [new() { X = 1, Y = 1 }],
            BlackManualEnabled = true, BlackManualColor = "#FF112233",
            WhiteManualEnabled = false, WhiteManualColor = "#FF445566"
        };

        Assert.True(FaceArtworkProcessingPipeline.TryResolveReferenceColors(input, operation, out var black, out var white));
        Assert.Equal("#FF112233", black);
        Assert.Equal("#FFC8C8C8", white);

        operation = new BlackWhiteLevelsOperationModel
        {
            BlackSamples = operation.BlackSamples, WhiteSamples = operation.WhiteSamples,
            BlackManualEnabled = false, BlackManualColor = operation.BlackManualColor,
            WhiteManualEnabled = true, WhiteManualColor = "#FF445566"
        };
        Assert.True(FaceArtworkProcessingPipeline.TryResolveReferenceColors(input, operation, out black, out white));
        Assert.Equal("#FF282828", black);
        Assert.Equal("#FF445566", white);
    }

    [Fact]
    public void ApplyProcessing_UsesStoredRectifiedOriginalOnlyWhenExplicitlyRequested()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"oasis-face-processing-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var generatedPath = Path.Combine(directory, "artwork.png");
            var originalPath = FaceArtworkRebuildService.GetOriginalArtworkPath(generatedPath);
            using (var original = Gradient())
            using (var image = SKImage.FromBitmap(original))
            using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
            using (var stream = File.Create(originalPath)) data.SaveTo(stream);
            File.Copy(originalPath, generatedPath);
            var before = File.ReadAllBytes(generatedPath);
            var artwork = new FaceArtworkModel
            {
                GeneratedAssetPath = generatedPath,
                ProcessingPipeline = new ImageProcessingPipelineModel
                {
                    Operations = [new BlackWhiteLevelsOperationModel
                    {
                        BlackManualEnabled = true, BlackManualColor = "#FF404040",
                        WhiteManualEnabled = true, WhiteManualColor = "#FFA0A0A0"
                    }]
                }
            };

            Assert.Equal(before, File.ReadAllBytes(generatedPath));
            Assert.True(new FaceArtworkRebuildService().ApplyProcessing(artwork, directory));
            Assert.False(before.SequenceEqual(File.ReadAllBytes(generatedPath)));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static BlackWhiteLevelsOperationModel Levels(string id, bool enabled, double strength, IReadOnlyList<NormalizedFacePointModel> black, IReadOnlyList<NormalizedFacePointModel> white) =>
        new() { Id = id, Enabled = enabled, Strength = strength, BlackSamples = black, WhiteSamples = white };

    private static FaceDocumentModel DocumentWithPipeline(IReadOnlyList<ImageProcessingOperationModel> operations) => new()
    {
        Artwork = new FaceArtworkModel { ProcessingPipeline = new ImageProcessingPipelineModel { Operations = operations } }
    };

    private static SKBitmap Gradient()
    {
        var bitmap = new SKBitmap(9, 9);
        for (var y = 0; y < bitmap.Height; y++) for (var x = 0; x < bitmap.Width; x++)
        {
            var value = (byte)(40 + x * 20);
            bitmap.SetPixel(x, y, new SKColor(value, value, value, 180));
        }
        return bitmap;
    }

    private static void AssertPixelsEqual(SKBitmap expected, SKBitmap actual)
    {
        Assert.Equal(expected.Width, actual.Width);
        Assert.Equal(expected.Height, actual.Height);
        for (var y = 0; y < expected.Height; y++) for (var x = 0; x < expected.Width; x++) Assert.Equal(expected.GetPixel(x, y), actual.GetPixel(x, y));
    }

    private static void AssertBitmapsDiffer(SKBitmap expected, SKBitmap actual)
    {
        Assert.Equal(expected.Width, actual.Width);
        Assert.Equal(expected.Height, actual.Height);
        Assert.Contains(
            Enumerable.Range(0, expected.Width * expected.Height),
            index => expected.GetPixel(index % expected.Width, index / expected.Width) != actual.GetPixel(index % actual.Width, index / actual.Width));
    }
}
