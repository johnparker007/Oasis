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
            var color = BitmapPixelBuffer.Read(source, x, y);
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
            var i = ((y * width) + x) * 4;
            var alpha = channels[i + 3];
            if (alpha <= 0d) { BitmapPixelBuffer.Write(output, x, y, 0, 0, 0, 0); continue; }
            var blurredAlpha = blurred[i + 3];
            var changed = false;
            var red = SharpenChannel(channels[i], blurred[i], alpha, blurredAlpha, threshold, normalized.PostWarpSharpeningAmount, ref changed);
            var green = SharpenChannel(channels[i + 1], blurred[i + 1], alpha, blurredAlpha, threshold, normalized.PostWarpSharpeningAmount, ref changed);
            var blue = SharpenChannel(channels[i + 2], blurred[i + 2], alpha, blurredAlpha, threshold, normalized.PostWarpSharpeningAmount, ref changed);
            if (changed) BitmapPixelBuffer.Write(output, x, y, ToByte(LinearToSrgb(red)), ToByte(LinearToSrgb(green)), ToByte(LinearToSrgb(blue)), ToByte(alpha));
        }
        return output;
    }

    private static double SharpenChannel(double value, double blurredValue, double alpha, double blurredAlpha,
        double threshold, double amount, ref bool changed)
    {
        var original = value / alpha;
        var soft = blurredAlpha > 1e-12 ? blurredValue / blurredAlpha : original;
        if (Math.Abs(LinearToSrgb(original) - LinearToSrgb(soft)) < threshold) return original;
        changed = true;
        return Math.Clamp(original + amount * (original - soft), 0d, 1d);
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
        if (horizontalPass)
        {
            for (var y = 0; y < height; y++) for (var x = 0; x < width; x++)
            {
                var r = 0d; var g = 0d; var b = 0d; var a = 0d;
                for (var offset = -radius; offset <= radius; offset++)
                {
                    var i = ((y * width + Math.Clamp(x + offset, 0, width - 1)) * 4);
                    var weight = kernel[offset + radius];
                    r += source[i] * weight; g += source[i + 1] * weight; b += source[i + 2] * weight; a += source[i + 3] * weight;
                }
                var d = ((y * width + x) * 4); destination[d] = r; destination[d + 1] = g; destination[d + 2] = b; destination[d + 3] = a;
            }
            return;
        }
        for (var x = 0; x < width; x++) for (var y = 0; y < height; y++)
        {
            var r = 0d; var g = 0d; var b = 0d; var a = 0d;
            for (var offset = -radius; offset <= radius; offset++)
            {
                var i = ((Math.Clamp(y + offset, 0, height - 1) * width + x) * 4);
                var weight = kernel[offset + radius];
                r += source[i] * weight; g += source[i + 1] * weight; b += source[i + 2] * weight; a += source[i + 3] * weight;
            }
            var d = ((y * width + x) * 4); destination[d] = r; destination[d + 1] = g; destination[d + 2] = b; destination[d + 3] = a;
        }
    }

    private static double SrgbToLinear(double value) => value <= 0.04045d ? value / 12.92d : Math.Pow((value + 0.055d) / 1.055d, 2.4d);
    private static double LinearToSrgb(double value) => value <= 0.0031308d ? value * 12.92d : (1.055d * Math.Pow(value, 1d / 2.4d)) - 0.055d;
    private static byte ToByte(double value) => (byte)Math.Clamp((int)Math.Round(value * 255d), 0, 255);
}
