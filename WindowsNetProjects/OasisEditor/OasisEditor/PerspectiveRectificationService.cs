using SkiaSharp;

namespace OasisEditor;

/// <summary>Shared four-corner perspective rectifier used by every Face artwork source.</summary>
internal static class PerspectiveRectificationService
{
    public static FaceSourceShapeOutputSize EstimateOutputSize(IReadOnlyList<FacePointModel> quad, double? targetAspectRatio = null)
    {
        if (quad.Count != 4) throw new ArgumentException("A perspective quad must contain four ordered corners.", nameof(quad));
        var twiceArea=0d;for(var i=0;i<4;i++){var next=(i+1)%4;twiceArea+=(quad[i].X*quad[next].Y)-(quad[next].X*quad[i].Y);}
        if(Math.Abs(twiceArea)<1d)throw new ArgumentException("The registration quad is degenerate; move its corners to enclose the artwork.",nameof(quad));
        var width = Math.Max(Distance(quad[0], quad[1]), Distance(quad[3], quad[2]));
        var height = Math.Max(Distance(quad[0], quad[3]), Distance(quad[1], quad[2]));
        if (targetAspectRatio is > 0d and < double.PositiveInfinity)
        {
            // Fit inside the useful sampled resolution: aspect correction must never upscale.
            if (width / Math.Max(1e-9, height) > targetAspectRatio) width = height * targetAspectRatio.Value;
            else height = width / targetAspectRatio.Value;
        }
        // Pixel-centre edge lengths are one less than their useful pixel count.
        return new(Math.Max(1, (int)Math.Floor(width) + 1), Math.Max(1, (int)Math.Floor(height) + 1));
    }

    public static SKBitmap Rectify(SKBitmap source, IReadOnlyList<FacePointModel> sourceQuad, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(source);
        var output = new SKBitmap(Math.Max(1, width), Math.Max(1, height), SKColorType.Rgba8888, SKAlphaType.Premul);
        var shape = new PanelFaceSourceShapeModel { TopLeft = sourceQuad[0], TopRight = sourceQuad[1], BottomRight = sourceQuad[2], BottomLeft = sourceQuad[3] };
        for (var y = 0; y < output.Height; y++)
        for (var x = 0; x < output.Width; x++)
        {
            if (!FaceSourceShapeTransformService.TryTransformFacePointToPanel(shape, output.Width - 1, output.Height - 1, x, y, out var point))
                output.SetPixel(x, y, SKColors.Transparent);
            else
                output.SetPixel(x, y, SampleBicubic(source, point.X, point.Y));
        }
        return output;
    }

    private static double Distance(FacePointModel a, FacePointModel b) => Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));

    private static SKColor SampleBicubic(SKBitmap bitmap, double x, double y)
    {
        // Preserve the established edge-clamped sampling behavior.
        var ix = Math.Clamp((int)Math.Round(x), 0, bitmap.Width - 1);
        var iy = Math.Clamp((int)Math.Round(y), 0, bitmap.Height - 1);
        return bitmap.GetPixel(ix, iy);
    }
}
