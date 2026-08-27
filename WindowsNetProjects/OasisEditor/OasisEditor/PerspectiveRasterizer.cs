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

        var sourcePixels = new BitmapPixelBuffer(source);
        var outputPixels = new BitmapPixelBuffer(output);
        var h = destinationToSource;
        Span<double> nx = stackalloc double[4];
        Span<double> ny = stackalloc double[4];
        Span<double> denominator = stackalloc double[4];
        for (var y = 0; y < height; y++)
        {
            // Four homogeneous lanes represent the fixed 2x2 quarter-pixel sample positions.
            // Their terms are affine in x, so advancing a pixel is three additions per lane.
            for (var lane = 0; lane < 4; lane++)
            {
                var destinationX = (lane & 1) == 0 ? .25d : .75d;
                var destinationY = y + (lane < 2 ? .25d : .75d);
                nx[lane] = h[0] * destinationX + h[1] * destinationY + h[2];
                ny[lane] = h[3] * destinationX + h[4] * destinationY + h[5];
                denominator[lane] = h[6] * destinationX + h[7] * destinationY + h[8];
            }
            for (var x = 0; x < width; x++)
            {
                var red = 0d; var green = 0d; var blue = 0d; var alpha = 0d;
                for (var lane = 0; lane < 4; lane++)
                {
                    var d = denominator[lane];
                    if (double.IsFinite(d) && Math.Abs(d) >= 1e-9)
                    {
                        var sourceX = nx[lane] / d; var sourceY = ny[lane] / d;
                        if (double.IsFinite(sourceX) && double.IsFinite(sourceY))
                        {
                            var sample = SampleBicubicPremultiplied(sourcePixels, source.Width, source.Height, sourceX, sourceY);
                            red += sample.Red; green += sample.Green; blue += sample.Blue; alpha += sample.Alpha;
                        }
                    }
                    nx[lane] += h[0]; ny[lane] += h[3]; denominator[lane] += h[6];
                }
                var color = ToColor(red * .25d, green * .25d, blue * .25d, alpha * .25d);
                outputPixels.WriteStraight(x, y, color.Red, color.Green, color.Blue, color.Alpha);
            }
        }

        return output;
    }

    /// <summary>Responsive display-only warp using native Skia perspective drawing and bilinear filtering.</summary>
    public static SKBitmap RectifyPreview(SKBitmap source, IReadOnlyList<FacePointModel> sourceQuad, int width, int height,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (sourceQuad.Count != 4) throw new ArgumentException("A perspective quad must have four corners.", nameof(sourceQuad));
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        var output = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        output.Erase(SKColors.Transparent);
        if (!FaceSourceShapeTransformService.TryCreateHomography(sourceQuad,
                FaceSourceShapeTransformService.CreateFaceCorners(width, height), out var sourceToDestination)) return output;
        cancellationToken.ThrowIfCancellationRequested();
        var matrix = new SKMatrix
        {
            ScaleX=(float)sourceToDestination[0], SkewX=(float)sourceToDestination[1], TransX=(float)sourceToDestination[2],
            SkewY=(float)sourceToDestination[3], ScaleY=(float)sourceToDestination[4], TransY=(float)sourceToDestination[5],
            Persp0=(float)sourceToDestination[6], Persp1=(float)sourceToDestination[7], Persp2=(float)sourceToDestination[8]
        };
        using(var canvas=new SKCanvas(output))using(var paint=new SKPaint{IsAntialias=true,FilterQuality=SKFilterQuality.Low})
        {canvas.SetMatrix(matrix);canvas.DrawBitmap(source,0,0,paint);canvas.Flush();}
        cancellationToken.ThrowIfCancellationRequested();
        return output;
    }

    internal static SKColor SampleBicubic(SKBitmap source, double x, double y)
    {
        var sample = SampleBicubicPremultiplied(new BitmapPixelBuffer(source), source.Width, source.Height, x, y);
        return ToColor(sample.Red, sample.Green, sample.Blue, sample.Alpha);
    }

    private static PremultipliedColor SampleBicubicPremultiplied(BitmapPixelBuffer source, int width, int height, double x, double y)
        => source.IsDirect
            ? SampleBicubicPremultipliedDirect(source, width, height, x, y)
            : SampleBicubicPremultipliedFallback(source, width, height, x, y);

    private static PremultipliedColor SampleBicubicPremultipliedDirect(BitmapPixelBuffer source, int width, int height, double x, double y)
        => SampleBicubicCore(source, width, height, x, y, direct: true);

    private static PremultipliedColor SampleBicubicPremultipliedFallback(BitmapPixelBuffer source, int width, int height, double x, double y)
        => SampleBicubicCore(source, width, height, x, y, direct: false);

    private static PremultipliedColor SampleBicubicCore(BitmapPixelBuffer source, int width, int height, double x, double y, bool direct)
    {
        // Pixel (i,j) is centred at (i + .5,j + .5) in the continuous image extent.
        if (x < 0d || y < 0d || x > width || y > height) return default;
        var sampleX = x - 0.5d;
        var sampleY = y - 0.5d;
        var baseX = (int)Math.Floor(sampleX);
        var baseY = (int)Math.Floor(sampleY);
        var red = 0d;
        var green = 0d;
        var blue = 0d;
        var alpha = 0d;

        Span<double> weightsX = stackalloc double[4];
        Span<int> samplesX = stackalloc int[4];
        for (var column = 0; column < 4; column++)
        {
            var offset = column - 1;
            weightsX[column] = CubicKernel(sampleX - (baseX + offset));
            samplesX[column] = Math.Clamp(baseX + offset, 0, width - 1);
        }
        if (direct)
        {
            for (var row = -1; row <= 2; row++)
            {
                var weightY = CubicKernel(sampleY - (baseY + row));
                var iy = Math.Clamp(baseY + row, 0, height - 1);
                for (var column = 0; column < 4; column++)
                {
                    var weight = weightY * weightsX[column];
                    source.ReadPremultipliedDirect(samplesX[column], iy, out var r, out var g, out var b, out var a);
                    const double byteScale = 1d / 255d;
                    red += r * byteScale * weight; green += g * byteScale * weight;
                    blue += b * byteScale * weight; alpha += a * byteScale * weight;
                }
            }
        }
        else
        {
            for (var row = -1; row <= 2; row++)
            {
                var weightY = CubicKernel(sampleY - (baseY + row));
                var iy = Math.Clamp(baseY + row, 0, height - 1);
                for (var column = 0; column < 4; column++)
                {
                    var weight = weightY * weightsX[column];
                    source.ReadPremultiplied(samplesX[column], iy, out var r, out var g, out var b, out var a);
                    const double byteScale = 1d / 255d;
                    red += r * byteScale * weight; green += g * byteScale * weight;
                    blue += b * byteScale * weight; alpha += a * byteScale * weight;
                }
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
