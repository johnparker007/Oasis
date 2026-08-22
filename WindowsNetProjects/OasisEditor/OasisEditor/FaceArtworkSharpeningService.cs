using SkiaSharp;

namespace OasisEditor;

/// <summary>Post-rectification, linear-light unsharp masking for visible Face artwork.</summary>
internal static class FaceArtworkSharpeningService
{
    public static SKBitmap Apply(SKBitmap source, FaceGenerationSettingsModel settings)
    {
        ArgumentNullException.ThrowIfNull(source);
        var normalized = (settings ?? FaceGenerationSettingsModel.Default).Normalize();
        var output = source.Copy();
        if (!normalized.PostWarpSharpeningEnabled || normalized.PostWarpSharpeningAmount <= 0d) return output;

        var width = source.Width;
        var height = source.Height;
        var count = width * height;
        var channels = new double[count * 4]; // premultiplied linear RGB and coverage
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var color = source.GetPixel(x, y);
            var i = ((y * width) + x) * 4;
            var alpha = color.Alpha / 255d;
            channels[i] = SrgbToLinear(color.Red / 255d) * alpha;
            channels[i + 1] = SrgbToLinear(color.Green / 255d) * alpha;
            channels[i + 2] = SrgbToLinear(color.Blue / 255d) * alpha;
            channels[i + 3] = alpha;
        }

        // Radius is Gaussian sigma in final output pixels; truncate the genuine Gaussian at 3 sigma.
        var sigma = normalized.PostWarpSharpeningRadiusPixels;
        var kernelRadius = Math.Max(1, (int)Math.Ceiling(3d * sigma));
        var kernel = CreateKernel(sigma, kernelRadius);
        var horizontal = new double[channels.Length];
        var blurred = new double[channels.Length];
        Convolve(channels, horizontal, width, height, kernel, kernelRadius, horizontalPass: true);
        Convolve(horizontal, blurred, width, height, kernel, kernelRadius, horizontalPass: false);

        var threshold = normalized.PostWarpSharpeningThreshold / 255d;
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var pixel = source.GetPixel(x, y);
            var i = ((y * width) + x) * 4;
            var alpha = channels[i + 3];
            if (alpha <= 0d) { output.SetPixel(x, y, SKColors.Transparent); continue; }
            var blurredAlpha = blurred[i + 3];
            var changed = false;
            var rgb = new double[3];
            for (var channel = 0; channel < 3; channel++)
            {
                var original = channels[i + channel] / alpha;
                var soft = blurredAlpha > 1e-12 ? blurred[i + channel] / blurredAlpha : original;
                var originalSrgb = LinearToSrgb(original);
                var softSrgb = LinearToSrgb(soft);
                if (Math.Abs(originalSrgb - softSrgb) >= threshold)
                {
                    rgb[channel] = Math.Clamp(original + normalized.PostWarpSharpeningAmount * (original - soft), 0d, 1d);
                    changed = true;
                }
                else rgb[channel] = original;
            }
            if (changed) output.SetPixel(x, y, new SKColor(ToByte(LinearToSrgb(rgb[0])), ToByte(LinearToSrgb(rgb[1])), ToByte(LinearToSrgb(rgb[2])), pixel.Alpha));
        }
        return output;
    }

    private static double[] CreateKernel(double sigma, int radius)
    {
        var kernel = new double[(radius * 2) + 1];
        var sum = 0d;
        for (var i = -radius; i <= radius; i++) { var value = Math.Exp(-(i * i) / (2d * sigma * sigma)); kernel[i + radius] = value; sum += value; }
        for (var i = 0; i < kernel.Length; i++) kernel[i] /= sum;
        return kernel;
    }

    private static void Convolve(double[] source, double[] destination, int width, int height, double[] kernel, int radius, bool horizontalPass)
    {
        for (var y = 0; y < height; y++) for (var x = 0; x < width; x++) for (var channel = 0; channel < 4; channel++)
        {
            var sum = 0d;
            for (var offset = -radius; offset <= radius; offset++)
            {
                var sx = horizontalPass ? Math.Clamp(x + offset, 0, width - 1) : x;
                var sy = horizontalPass ? y : Math.Clamp(y + offset, 0, height - 1);
                sum += source[((sy * width + sx) * 4) + channel] * kernel[offset + radius];
            }
            destination[((y * width + x) * 4) + channel] = sum;
        }
    }

    private static double SrgbToLinear(double value) => value <= 0.04045d ? value / 12.92d : Math.Pow((value + 0.055d) / 1.055d, 2.4d);
    private static double LinearToSrgb(double value) => value <= 0.0031308d ? value * 12.92d : (1.055d * Math.Pow(value, 1d / 2.4d)) - 0.055d;
    private static byte ToByte(double value) => (byte)Math.Clamp((int)Math.Round(value * 255d), 0, 255);
}
