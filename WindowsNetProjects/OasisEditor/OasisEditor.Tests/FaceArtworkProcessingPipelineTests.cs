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

    [Theory]
    [InlineData(100, 40, 5)]   // orange
    [InlineData(5, 15, 100)]   // blue
    [InlineData(70, 5, 90)]    // purple
    public void Evaluate_BrighteningPreservesSaturatedLinearChromaticity(byte red, byte green, byte blue)
    {
        using var input = new SKBitmap(1, 1);
        input.SetPixel(0, 0, new SKColor(red, green, blue, 173));
        using var output = EvaluateWithManualNeutralReferences(input, 100);
        var sourceChromaticity = LinearChromaticity(input.GetPixel(0, 0));
        var outputChromaticity = LinearChromaticity(output.GetPixel(0, 0));

        Assert.True(output.GetPixel(0, 0).Red > red || output.GetPixel(0, 0).Green > green || output.GetPixel(0, 0).Blue > blue);
        Assert.InRange(Math.Abs(sourceChromaticity.R - outputChromaticity.R), 0d, .015d);
        Assert.InRange(Math.Abs(sourceChromaticity.G - outputChromaticity.G), 0d, .015d);
        Assert.InRange(Math.Abs(sourceChromaticity.B - outputChromaticity.B), 0d, .015d);
        Assert.Equal(173, output.GetPixel(0, 0).Alpha);
    }

    [Fact]
    public void Evaluate_NeutralAndIntermediateStrengthRemainNeutralAndChromaticityPreserving()
    {
        using var neutral = new SKBitmap(1, 1);
        neutral.SetPixel(0, 0, new SKColor(70, 70, 70));
        using var correctedNeutral = EvaluateWithManualNeutralReferences(neutral, 100);
        Assert.Equal(correctedNeutral.GetPixel(0, 0).Red, correctedNeutral.GetPixel(0, 0).Green);
        Assert.Equal(correctedNeutral.GetPixel(0, 0).Green, correctedNeutral.GetPixel(0, 0).Blue);

        using var colour = new SKBitmap(1, 1);
        colour.SetPixel(0, 0, new SKColor(70, 5, 90));
        using var intermediate = EvaluateWithManualNeutralReferences(colour, 50);
        var sourceChromaticity = LinearChromaticity(colour.GetPixel(0, 0));
        var outputChromaticity = LinearChromaticity(intermediate.GetPixel(0, 0));
        Assert.InRange(Math.Abs(sourceChromaticity.R - outputChromaticity.R), 0d, .02d);
        Assert.InRange(Math.Abs(sourceChromaticity.B - outputChromaticity.B), 0d, .02d);
    }

    [Fact]
    public void Evaluate_BlackNearBlackAndOutOfGamutScalingAreSafe()
    {
        using var input = new SKBitmap(3, 1);
        input.SetPixel(0, 0, SKColors.Black);
        input.SetPixel(1, 0, new SKColor(1, 0, 1));
        input.SetPixel(2, 0, new SKColor(220, 20, 5));
        using var output = EvaluateWithManualNeutralReferences(input, 100);

        Assert.Equal(SKColors.Black, output.GetPixel(0, 0));
        Assert.True(output.GetPixel(1, 0).Red >= input.GetPixel(1, 0).Red);
        Assert.Equal(byte.MaxValue, output.GetPixel(2, 0).Red);
        Assert.True(output.GetPixel(2, 0).Green < output.GetPixel(2, 0).Red);
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

    [Fact]
    public void ApplyProcessingCommand_UndoRedoRestoresOnlyProcessedArtwork()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"oasis-face-apply-command-{Guid.NewGuid():N}");
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
            var originalBytes = File.ReadAllBytes(originalPath);
            var previousProcessed = File.ReadAllBytes(generatedPath);
            var operation = new BlackWhiteLevelsOperationModel
            {
                Id = "levels", Strength = 50,
                BlackManualEnabled = true, BlackManualColor = "#FF404040",
                WhiteManualEnabled = true, WhiteManualColor = "#FFA0A0A0"
            };
            var face = DocumentWithPipeline([operation]);
            face = new FaceDocumentModel
            {
                Artwork = new FaceArtworkModel
                {
                    GeneratedAssetPath = generatedPath,
                    ProcessingPipeline = face.Artwork!.ProcessingPipeline
                }
            };
            var document = new DocumentTabViewModel(EditorDocument.CreateFaceStub("Face"), faceDocumentJson: FaceDocumentStorage.Serialize(face));
            document.SetProjectAccessor(() => CreateProject(directory));
            var updated = new BlackWhiteLevelsOperationModel
            {
                Id = operation.Id, Strength = 100,
                BlackManualEnabled = operation.BlackManualEnabled, BlackManualColor = operation.BlackManualColor,
                WhiteManualEnabled = operation.WhiteManualEnabled, WhiteManualColor = operation.WhiteManualColor
            };
            document.CommandService.Execute(FaceMutationCommands.CreateUpdateProcessingOperationCommand(document.DocumentId, document, updated, "Change strength"));
            document.CommandService.Execute(FaceMutationCommands.CreateApplyArtworkProcessingCommand(document.DocumentId, document));
            var applied = File.ReadAllBytes(generatedPath);

            Assert.False(previousProcessed.SequenceEqual(applied));
            Assert.Equal(100, Assert.IsType<BlackWhiteLevelsOperationModel>(Assert.Single(document.GetFaceDocument().Artwork!.ProcessingPipeline.Operations)).Strength);
            Assert.True(document.CommandService.TryUndo());
            Assert.Equal(previousProcessed, File.ReadAllBytes(generatedPath));
            Assert.Equal(originalBytes, File.ReadAllBytes(originalPath));
            Assert.Equal(100, Assert.IsType<BlackWhiteLevelsOperationModel>(Assert.Single(document.GetFaceDocument().Artwork!.ProcessingPipeline.Operations)).Strength);
            Assert.True(document.CommandService.TryUndo());
            Assert.Equal(50, Assert.IsType<BlackWhiteLevelsOperationModel>(Assert.Single(document.GetFaceDocument().Artwork!.ProcessingPipeline.Operations)).Strength);
            Assert.True(document.CommandService.TryRedo());
            Assert.True(document.CommandService.TryRedo());
            Assert.Equal(applied, File.ReadAllBytes(generatedPath));
            Assert.Equal(originalBytes, File.ReadAllBytes(originalPath));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static EditorProject CreateProject(string directory) => new()
    {
        Name = "Test", ProjectFilePath = Path.Combine(directory, "test.oasis"), ProjectDirectory = directory,
        AssetsDirectory = directory, MachinesDirectory = directory, GeneratedDirectory = directory
    };

    private static SKBitmap EvaluateWithManualNeutralReferences(SKBitmap input, double strength)
    {
        return new FaceArtworkProcessingPipeline().Evaluate(input, new ImageProcessingPipelineModel
        {
            Operations = [new BlackWhiteLevelsOperationModel
            {
                Strength = strength,
                BlackManualEnabled = true,
                BlackManualColor = "#FF000000",
                WhiteManualEnabled = true,
                WhiteManualColor = "#FFB0B0B0"
            }]
        });
    }

    private static (double R, double G, double B) LinearChromaticity(SKColor color)
    {
        static double Linear(byte value)
        {
            var srgb = value / 255d;
            return srgb <= .04045d ? srgb / 12.92d : Math.Pow((srgb + .055d) / 1.055d, 2.4d);
        }
        var red = Linear(color.Red);
        var green = Linear(color.Green);
        var blue = Linear(color.Blue);
        var sum = red + green + blue;
        return sum <= 0d ? (0d, 0d, 0d) : (red / sum, green / sum, blue / sum);
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
