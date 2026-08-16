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
            Levels("second", true, 40, [new() { X = .1, Y = .2 }], [new() { X = .9, Y = .8 }])]);

        Assert.True(FaceDocumentStorage.TryRead(FaceDocumentStorage.Serialize(model), out var file));
        var operations = FaceDocumentStorage.ToModel(file).Artwork!.ProcessingPipeline.Operations.Cast<BlackWhiteLevelsOperationModel>().ToArray();

        Assert.Equal(["first", "second"], operations.Select(operation => operation.Id));
        Assert.False(operations[0].Enabled);
        Assert.Equal(100, operations[0].Strength);
        Assert.Equal(0, operations[0].BlackSamples[0].X);
        Assert.Equal(1, operations[0].WhiteSamples[0].Y);
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
    public void MultipleSamples_UseMedianSoSingleOutlierDoesNotControlReference()
    {
        using var input = Gradient();
        var normal = Levels("normal", true, 100, [new() { X = .1, Y = .5 }, new() { X = .1, Y = .5 }], [new() { X = .9, Y = .5 }, new() { X = .9, Y = .5 }]);
        var outlier = Levels("outlier", true, 100, [.. normal.BlackSamples, new() { X = .9, Y = .5 }], [.. normal.WhiteSamples, new() { X = .1, Y = .5 }]);
        var evaluator = new FaceArtworkProcessingPipeline();
        using var expected = evaluator.Evaluate(input, new() { Operations = [normal] });
        using var actual = evaluator.Evaluate(input, new() { Operations = [outlier] });
        AssertPixelsEqual(expected, actual);
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
