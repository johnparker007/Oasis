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
            || !TryResolveReferenceColor(input, operation.BlackManualEnabled, operation.BlackManualColor, operation.BlackSamples, out var blackColor)
            || !TryResolveReferenceColor(input, operation.WhiteManualEnabled, operation.WhiteManualColor, operation.WhiteSamples, out var whiteColor)
            || (whiteColor.Luminance - blackColor.Luminance) < MinimumReferenceRange)
        {
            return input.Copy();
        }

        var black = blackColor.Luminance;
        var white = whiteColor.Luminance;

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

    internal static bool TryResolveReferenceColors(SKBitmap image, BlackWhiteLevelsOperationModel operation, out string? blackColor, out string? whiteColor)
    {
        var blackResolved = TryResolveReferenceColor(image, operation.BlackManualEnabled, operation.BlackManualColor, operation.BlackSamples, out var black);
        var whiteResolved = TryResolveReferenceColor(image, operation.WhiteManualEnabled, operation.WhiteManualColor, operation.WhiteSamples, out var white);
        blackColor = blackResolved ? black.ToHex() : null;
        whiteColor = whiteResolved ? white.ToHex() : null;
        return blackResolved && whiteResolved;
    }

    private static bool TryResolveReferenceColor(SKBitmap image, bool manualEnabled, string manualColor, IReadOnlyList<NormalizedFacePointModel> samples, out LinearColor value)
    {
        if (manualEnabled) return TryParseColor(manualColor, out value);
        value = default;
        if (samples.Count == 0 || image.Width == 0 || image.Height == 0) return false;
        var red = 0d;
        var green = 0d;
        var blue = 0d;
        var count = 0;
        foreach (var sample in samples)
        {
            if (!double.IsFinite(sample.X) || !double.IsFinite(sample.Y) || sample.X < 0d || sample.X > 1d || sample.Y < 0d || sample.Y > 1d) continue;
            var x = Math.Clamp((int)Math.Round(sample.X * (image.Width - 1)), 0, image.Width - 1);
            var y = Math.Clamp((int)Math.Round(sample.Y * (image.Height - 1)), 0, image.Height - 1);
            var pixel = image.GetPixel(x, y);
            if (pixel.Alpha == 0) continue;
            red += SrgbToLinear(pixel.Red / 255d);
            green += SrgbToLinear(pixel.Green / 255d);
            blue += SrgbToLinear(pixel.Blue / 255d);
            count++;
        }
        if (count == 0) return false;
        value = new LinearColor(red / count, green / count, blue / count);
        return true;
    }

    private static bool TryParseColor(string? text, out LinearColor color)
    {
        color = default;
        var value = text?.Trim().TrimStart('#');
        if (value?.Length == 8) value = value[2..];
        if (value?.Length != 6 || !uint.TryParse(value, System.Globalization.NumberStyles.HexNumber, null, out var rgb)) return false;
        color = new LinearColor(SrgbToLinear(((rgb >> 16) & 255) / 255d), SrgbToLinear(((rgb >> 8) & 255) / 255d), SrgbToLinear((rgb & 255) / 255d));
        return true;
    }

    private static double Luminance(double r, double g, double b) => 0.2126d * r + 0.7152d * g + 0.0722d * b;
    private static double Lerp(double from, double to, double amount) => from + (to - from) * amount;
    private static byte ToByte(double value) => (byte)Math.Round(Math.Clamp(value, 0d, 1d) * 255d);
    private static double SrgbToLinear(double value) => value <= 0.04045d ? value / 12.92d : Math.Pow((value + 0.055d) / 1.055d, 2.4d);
    private static double LinearToSrgb(double value) => value <= 0.0031308d ? value * 12.92d : 1.055d * Math.Pow(value, 1d / 2.4d) - 0.055d;

    private readonly record struct LinearColor(double Red, double Green, double Blue)
    {
        public double Luminance => FaceArtworkProcessingPipeline.Luminance(Red, Green, Blue);
        public string ToHex() => $"#FF{ToByte(LinearToSrgb(Red)):X2}{ToByte(LinearToSrgb(Green)):X2}{ToByte(LinearToSrgb(Blue)):X2}";
    }
}
