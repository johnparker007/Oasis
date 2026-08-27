using OasisEditor;
using SkiaSharp;
using Xunit;

namespace OasisEditor.Tests;

public sealed class PerspectiveRasterizerTests
{
    [Fact]
    public void SampleBicubic_AtFractionalCoordinate_InterpolatesRatherThanRounds()
    {
        using var source = Bitmap(2, 1, (x, _) => x == 0 ? SKColors.Black : SKColors.White);

        var result = PerspectiveRasterizer.SampleBicubic(source, 1d, 0.5d);

        Assert.InRange(result.Red, 120, 135);
        Assert.NotEqual(source.GetPixel(0, 0), result);
        Assert.NotEqual(source.GetPixel(1, 0), result);
    }

    [Fact]
    public void Rectify_IdentityQuad_PreservesLinearImageWithoutHalfPixelTranslation()
    {
        using var source = Bitmap(8, 6, (x, y) => new SKColor((byte)(20 + (x * 20)), (byte)(30 + (y * 25)), 80));
        using var result = PerspectiveRasterizer.Rectify(source, FullQuad(source), source.Width, source.Height);

        Assert.Equal(source.Width, result.Width);
        Assert.Equal(source.Height, result.Height);
        for (var y = 0; y < source.Height; y++)
        for (var x = 0; x < source.Width; x++)
        {
            var expected = source.GetPixel(x, y);
            var actual = result.GetPixel(x, y);
            Assert.InRange(Math.Abs(actual.Red - expected.Red), 0, 3);
            Assert.InRange(Math.Abs(actual.Green - expected.Green), 0, 3);
        }
    }

    [Fact]
    public void Rectify_PerspectiveLine_HasStableAntialiasedCentroid()
    {
        using var source = Bitmap(40, 40, (x, _) => x is 19 or 20 ? SKColors.White : SKColors.Black);
        var quad = new[] { Point(8, 0), Point(32, 4), Point(38, 38), Point(2, 40) };
        using var result = PerspectiveRasterizer.Rectify(source, quad, 30, 35);

        var centroids = Enumerable.Range(3, result.Height - 6).Select(y => BrightnessCentroid(result, y)).ToArray();
        var adjacentMovements = centroids.Zip(centroids.Skip(1), (first, second) => Math.Abs(second - first));

        // This asymmetric quad correctly produces a slightly angled line. Stability means its
        // centroid moves smoothly along that line rather than jumping between whole pixels.
        Assert.InRange(centroids.Max() - centroids.Min(), 0.3, 0.7);
        Assert.All(adjacentMovements, movement => Assert.InRange(movement, 0d, 0.08d));
        Assert.Contains(
            Enumerable.Range(3, result.Height - 6)
                .SelectMany(y => Enumerable.Range(0, result.Width).Select(x => result.GetPixel(x, y).Red)),
            value => value is > 10 and < 245);
    }

    [Fact]
    public void Rectify_DiagonalDetail_ProducesAntialiasedValues()
    {
        using var source = Bitmap(16, 16, (x, y) => Math.Abs(x - y) <= 0 ? SKColors.White : SKColors.Black);
        var quad = new[] { Point(1, 0), Point(15, 2), Point(14, 16), Point(0, 13) };
        using var result = PerspectiveRasterizer.Rectify(source, quad, 16, 16);

        Assert.Contains(Enumerable.Range(0, result.Height).SelectMany(y => Enumerable.Range(0, result.Width).Select(x => result.GetPixel(x, y).Red)), value => value is > 10 and < 245);
    }

    [Fact]
    public void Rectify_Minification_AveragesHighFrequencyDetail()
    {
        using var source = Bitmap(32, 8, (x, _) => x % 2 == 0 ? SKColors.Black : SKColors.White);
        using var result = PerspectiveRasterizer.Rectify(source, FullQuad(source), 4, 8);

        Assert.All(Enumerable.Range(0, result.Width), x => Assert.InRange(result.GetPixel(x, 4).Red, 80, 175));
    }

    [Fact]
    public void SampleBicubic_TransparentBoundary_InterpolatesPremultipliedColorWithoutDarkFringe()
    {
        using var source = Bitmap(2, 1, (x, _) => x == 0 ? SKColors.Red : SKColors.Transparent);

        var result = PerspectiveRasterizer.SampleBicubic(source, 1d, 0.5d);

        Assert.InRange(result.Alpha, 120, 135);
        Assert.InRange(result.Red, 250, 255);
        Assert.InRange(result.Green, 0, 1);
        Assert.InRange(result.Blue, 0, 1);
        Assert.Equal(SKColors.Transparent, PerspectiveRasterizer.SampleBicubic(source, 2.1d, 0.5d));
    }

    [Fact]
    public void SampleBicubic_BgraStorageReadsChannelsInTheirDeclaredOrder()
    {
        using var source = new SKBitmap(1, 1, SKColorType.Bgra8888, SKAlphaType.Premul);
        source.SetPixel(0, 0, new SKColor(230, 40, 10, 128));

        var result = PerspectiveRasterizer.SampleBicubic(source, .5d, .5d);

        Assert.InRange(result.Red, 229, 231);
        Assert.InRange(result.Green, 39, 41);
        Assert.InRange(result.Blue, 9, 11);
        Assert.Equal(128, result.Alpha);
    }

    private static SKBitmap Bitmap(int width, int height, Func<int, int, SKColor> color)
    {
        var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++) bitmap.SetPixel(x, y, color(x, y));
        return bitmap;
    }

    private static FacePointModel[] FullQuad(SKBitmap bitmap) =>
        [Point(0, 0), Point(bitmap.Width, 0), Point(bitmap.Width, bitmap.Height), Point(0, bitmap.Height)];

    private static FacePointModel Point(double x, double y) => new() { X = x, Y = y };

    private static double BrightnessCentroid(SKBitmap bitmap, int y)
    {
        var weighted = 0d;
        var total = 0d;
        for (var x = 0; x < bitmap.Width; x++)
        {
            var brightness = bitmap.GetPixel(x, y).Red;
            weighted += (x + 0.5d) * brightness;
            total += brightness;
        }
        return weighted / total;
    }
}
