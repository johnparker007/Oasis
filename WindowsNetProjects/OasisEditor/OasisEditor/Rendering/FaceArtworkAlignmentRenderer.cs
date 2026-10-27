using SkiaSharp;

namespace OasisEditor.Rendering;

/// <summary>Display-only projective preview. Production output keeps its quality-first rasterizer.</summary>
internal static class FaceArtworkAlignmentRenderer
{
    public static void Draw(SKCanvas canvas, SKImage source, FaceArtworkOverrideModel value, SKRect destination, double opacity)
    {
        var registration = value.PerspectiveRegistration.Normalize();
        if (!registration.IsValid()) return;
        var quad = new[] { registration.TopLeft, registration.TopRight, registration.BottomRight, registration.BottomLeft }
            .Select(p => new FacePointModel { X = p.X * source.Width, Y = p.Y * source.Height }).ToArray();
        var target = new[] { new FacePointModel { X = destination.Left, Y = destination.Top },
            new FacePointModel { X = destination.Right, Y = destination.Top },
            new FacePointModel { X = destination.Right, Y = destination.Bottom },
            new FacePointModel { X = destination.Left, Y = destination.Bottom } };
        if (!PortableFaceSourceShapeTransformAdapter.TryCreateHomography(quad, target, out var h)) return;
        var matrix = new SKMatrix((float)h[0], (float)h[1], (float)h[2], (float)h[3], (float)h[4], (float)h[5], (float)h[6], (float)h[7], (float)h[8]);
        using var paint = new SKPaint { Color = SKColors.White.WithAlpha((byte)Math.Round(Math.Clamp(opacity, 0, 1) * 255)), IsAntialias = true };
        canvas.Save();
        canvas.ClipRect(destination);
        canvas.Concat(ref matrix);
        canvas.DrawImage(source, 0, 0, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear), paint);
        canvas.Restore();
    }
}
