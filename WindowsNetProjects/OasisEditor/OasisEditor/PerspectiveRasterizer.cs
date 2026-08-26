using SkiaSharp;

namespace OasisEditor;

/// <summary>High-quality projective rasterization for offline generated assets.</summary>
internal static class PerspectiveRasterizer
{
    private const int SupersampleGridSize = 2;

    public static SKBitmap Rectify(SKBitmap source, IReadOnlyList<FacePointModel> sourceQuad, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (sourceQuad.Count != 4) throw new ArgumentException("A perspective quad must have four corners.", nameof(sourceQuad));
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));

        var output = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        output.Erase(SKColors.Transparent);
        if (!FaceSourceShapeTransformService.TryCreateHomography(
                FaceSourceShapeTransformService.CreateFaceCorners(width, height), sourceQuad, out var destinationToSource))
        {
            return output;
        }

        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var red = 0d;
            var green = 0d;
            var blue = 0d;
            var alpha = 0d;
            for (var sampleY = 0; sampleY < SupersampleGridSize; sampleY++)
            for (var sampleX = 0; sampleX < SupersampleGridSize; sampleX++)
            {
                // Images occupy [0, width] x [0, height]; these are quarter-points in the destination pixel area.
                var destinationX = x + ((sampleX + 0.5d) / SupersampleGridSize);
                var destinationY = y + ((sampleY + 0.5d) / SupersampleGridSize);
                if (!FaceSourceShapeTransformService.TryApplyHomography(destinationToSource, destinationX, destinationY, out var point)) continue;
                var sample = SampleBicubicPremultiplied(source, point.X, point.Y);
                red += sample.Red;
                green += sample.Green;
                blue += sample.Blue;
                alpha += sample.Alpha;
            }

            const double sampleCount = SupersampleGridSize * SupersampleGridSize;
            output.SetPixel(x, y, ToColor(red / sampleCount, green / sampleCount, blue / sampleCount, alpha / sampleCount));
        }

        return output;
    }

    /// <summary>Responsive display-only warp: one bilinear sample per pixel, with cancellation.</summary>
    public static SKBitmap RectifyPreview(SKBitmap source, IReadOnlyList<FacePointModel> sourceQuad, int width, int height,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (sourceQuad.Count != 4) throw new ArgumentException("A perspective quad must have four corners.", nameof(sourceQuad));
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        var output = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        output.Erase(SKColors.Transparent);
        if (!FaceSourceShapeTransformService.TryCreateHomography(
                FaceSourceShapeTransformService.CreateFaceCorners(width, height), sourceQuad, out var destinationToSource)) return output;
        for (var y = 0; y < height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (var x = 0; x < width; x++)
            {
                if (!FaceSourceShapeTransformService.TryApplyHomography(destinationToSource, x + .5d, y + .5d, out var point)) continue;
                output.SetPixel(x, y, SampleBilinearPremultiplied(source, point.X, point.Y));
            }
        }
        return output;
    }

    private static SKColor SampleBilinearPremultiplied(SKBitmap source, double x, double y)
    {
        if (x < 0d || y < 0d || x > source.Width || y > source.Height) return SKColors.Transparent;
        var sx=x-.5d; var sy=y-.5d; var x0=(int)Math.Floor(sx); var y0=(int)Math.Floor(sy);
        var fx=sx-x0; var fy=sy-y0;
        var w0=(1d-fx)*(1d-fy);var w1=fx*(1d-fy);var w2=fx*fy;var w3=(1d-fx)*fy;
        var c0=source.GetPixel(Math.Clamp(x0,0,source.Width-1),Math.Clamp(y0,0,source.Height-1));
        var c1=source.GetPixel(Math.Clamp(x0+1,0,source.Width-1),Math.Clamp(y0,0,source.Height-1));
        var c2=source.GetPixel(Math.Clamp(x0+1,0,source.Width-1),Math.Clamp(y0+1,0,source.Height-1));
        var c3=source.GetPixel(Math.Clamp(x0,0,source.Width-1),Math.Clamp(y0+1,0,source.Height-1));
        double red=0,green=0,blue=0,alpha=0;
        Add(c0,w0);Add(c1,w1);Add(c2,w2);Add(c3,w3);
        return ToColor(red,green,blue,alpha);
        void Add(SKColor color,double weight){var a=color.Alpha/255d;alpha+=a*weight;red+=(color.Red/255d)*a*weight;green+=(color.Green/255d)*a*weight;blue+=(color.Blue/255d)*a*weight;}
    }

    internal static SKColor SampleBicubic(SKBitmap source, double x, double y)
    {
        var sample = SampleBicubicPremultiplied(source, x, y);
        return ToColor(sample.Red, sample.Green, sample.Blue, sample.Alpha);
    }

    private static PremultipliedColor SampleBicubicPremultiplied(SKBitmap source, double x, double y)
    {
        // Pixel (i,j) is centred at (i + .5,j + .5) in the continuous image extent.
        if (x < 0d || y < 0d || x > source.Width || y > source.Height) return default;
        var sampleX = x - 0.5d;
        var sampleY = y - 0.5d;
        var baseX = (int)Math.Floor(sampleX);
        var baseY = (int)Math.Floor(sampleY);
        var red = 0d;
        var green = 0d;
        var blue = 0d;
        var alpha = 0d;

        for (var row = -1; row <= 2; row++)
        {
            var weightY = CubicKernel(sampleY - (baseY + row));
            var iy = Math.Clamp(baseY + row, 0, source.Height - 1);
            for (var column = -1; column <= 2; column++)
            {
                var weight = weightY * CubicKernel(sampleX - (baseX + column));
                var ix = Math.Clamp(baseX + column, 0, source.Width - 1);
                var color = source.GetPixel(ix, iy);
                var a = color.Alpha / 255d;
                // SKColor exposes straight RGB, so explicitly interpolate premultiplied components.
                red += (color.Red / 255d) * a * weight;
                green += (color.Green / 255d) * a * weight;
                blue += (color.Blue / 255d) * a * weight;
                alpha += a * weight;
            }
        }

        alpha = Math.Clamp(alpha, 0d, 1d);
        return new PremultipliedColor(
            Math.Clamp(red, 0d, alpha),
            Math.Clamp(green, 0d, alpha),
            Math.Clamp(blue, 0d, alpha),
            alpha);
    }

    // Keys' cubic convolution kernel with a = -0.5 (Catmull-Rom).
    private static double CubicKernel(double value)
    {
        var x = Math.Abs(value);
        if (x <= 1d) return (1.5d * x * x * x) - (2.5d * x * x) + 1d;
        if (x < 2d) return (-0.5d * x * x * x) + (2.5d * x * x) - (4d * x) + 2d;
        return 0d;
    }

    private static SKColor ToColor(double red, double green, double blue, double alpha)
    {
        alpha = Math.Clamp(alpha, 0d, 1d);
        if (alpha <= 1e-12) return SKColors.Transparent;
        return new SKColor(
            ToByte(red / alpha),
            ToByte(green / alpha),
            ToByte(blue / alpha),
            ToByte(alpha));
    }

    private static byte ToByte(double value) => (byte)Math.Clamp((int)Math.Round(value * 255d), 0, 255);

    private readonly record struct PremultipliedColor(double Red, double Green, double Blue, double Alpha);
}
