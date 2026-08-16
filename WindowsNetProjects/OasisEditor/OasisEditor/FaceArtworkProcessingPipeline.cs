using SkiaSharp;

namespace OasisEditor;

/// <summary>Evaluates authored operations in list order on canonical rectangular Face artwork.</summary>
internal sealed class FaceArtworkProcessingPipeline
{
    private const double MinimumReferenceRange = 1d / 255d;

    public SKBitmap Evaluate(SKBitmap input, ImageProcessingPipelineModel pipeline, int? operationCount = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(pipeline);

        var result = input.Copy();
        var count = Math.Clamp(operationCount ?? pipeline.Operations.Count, 0, pipeline.Operations.Count);
        foreach (var operation in pipeline.Operations.Take(count))
        {
            if (!operation.Enabled) continue;
            var next = operation switch
            {
                BlackWhiteLevelsOperationModel levels => ApplyBlackWhiteLevels(result, levels.Normalize()),
                _ => throw new InvalidOperationException($"Unsupported image processing operation '{operation.GetType().Name}'.")
            };
            result.Dispose();
            result = next;
        }

        return result;
    }

    private static SKBitmap ApplyBlackWhiteLevels(SKBitmap input, BlackWhiteLevelsOperationModel operation)
    {
        if (operation.Strength <= 0d
            || !TryReferenceLuminance(input, operation.BlackSamples, out var black)
            || !TryReferenceLuminance(input, operation.WhiteSamples, out var white)
            || white - black < MinimumReferenceRange)
        {
            return input.Copy();
        }

        var blend = operation.Strength / 100d;
        var output = new SKBitmap(input.Width, input.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        for (var y = 0; y < input.Height; y++)
        for (var x = 0; x < input.Width; x++)
        {
            var source = input.GetPixel(x, y);
            var r = SrgbToLinear(source.Red / 255d);
            var g = SrgbToLinear(source.Green / 255d);
            var b = SrgbToLinear(source.Blue / 255d);
            var luminance = Luminance(r, g, b);
            var mappedLuminance = Math.Clamp((luminance - black) / (white - black), 0d, 1d);
            var delta = mappedLuminance - luminance;
            var correctedR = Math.Clamp(r + delta, 0d, 1d);
            var correctedG = Math.Clamp(g + delta, 0d, 1d);
            var correctedB = Math.Clamp(b + delta, 0d, 1d);
            output.SetPixel(x, y, new SKColor(
                ToByte(LinearToSrgb(Lerp(r, correctedR, blend))),
                ToByte(LinearToSrgb(Lerp(g, correctedG, blend))),
                ToByte(LinearToSrgb(Lerp(b, correctedB, blend))),
                source.Alpha));
        }
        return output;
    }

    private static bool TryReferenceLuminance(SKBitmap image, IReadOnlyList<NormalizedFacePointModel> samples, out double value)
    {
        value = 0d;
        if (samples.Count == 0 || image.Width == 0 || image.Height == 0) return false;
        var representatives = new List<double>(samples.Count);
        foreach (var sample in samples)
        {
            if (!double.IsFinite(sample.X) || !double.IsFinite(sample.Y) || sample.X < 0d || sample.X > 1d || sample.Y < 0d || sample.Y > 1d) continue;
            var centerX = (int)Math.Round(sample.X * (image.Width - 1));
            var centerY = (int)Math.Round(sample.Y * (image.Height - 1));
            var neighbourhood = new List<double>(25);
            for (var y = Math.Max(0, centerY - 2); y <= Math.Min(image.Height - 1, centerY + 2); y++)
            for (var x = Math.Max(0, centerX - 2); x <= Math.Min(image.Width - 1, centerX + 2); x++)
            {
                var pixel = image.GetPixel(x, y);
                if (pixel.Alpha == 0) continue;
                neighbourhood.Add(Luminance(SrgbToLinear(pixel.Red / 255d), SrgbToLinear(pixel.Green / 255d), SrgbToLinear(pixel.Blue / 255d)));
            }
            if (neighbourhood.Count > 0) representatives.Add(Median(neighbourhood));
        }
        if (representatives.Count == 0) return false;
        value = Median(representatives);
        return true;
    }

    private static double Median(List<double> values)
    {
        values.Sort();
        var middle = values.Count / 2;
        return values.Count % 2 == 0 ? (values[middle - 1] + values[middle]) / 2d : values[middle];
    }

    private static double Luminance(double r, double g, double b) => 0.2126d * r + 0.7152d * g + 0.0722d * b;
    private static double Lerp(double from, double to, double amount) => from + (to - from) * amount;
    private static byte ToByte(double value) => (byte)Math.Round(Math.Clamp(value, 0d, 1d) * 255d);
    private static double SrgbToLinear(double value) => value <= 0.04045d ? value / 12.92d : Math.Pow((value + 0.055d) / 1.055d, 2.4d);
    private static double LinearToSrgb(double value) => value <= 0.0031308d ? value * 12.92d : 1.055d * Math.Pow(value, 1d / 2.4d) - 0.055d;
}
