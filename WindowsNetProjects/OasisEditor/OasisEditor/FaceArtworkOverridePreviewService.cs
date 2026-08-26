using System.IO;
using SkiaSharp;

namespace OasisEditor;

internal sealed record FaceArtworkOverridePreviewResult(byte[] PngBytes, int Width, int Height);

/// <summary>Creates bounded, cancellable display previews. Production artwork never consumes this path.</summary>
internal static class FaceArtworkOverridePreviewService
{
    public const int MaximumPreviewDimension = 1600;

    public static FaceSourceShapeOutputSize DeterminePreviewSize(int sourceWidth, int sourceHeight,
        FacePerspectiveRegistrationModel registration, int maximumDimension = MaximumPreviewDimension)
    {
        if (maximumDimension <= 0) throw new ArgumentOutOfRangeException(nameof(maximumDimension));
        var useful=FaceSourceShapeTransformService.EstimateRegisteredImageOutputSize(sourceWidth,sourceHeight,registration);
        var scale=Math.Min(1d,maximumDimension/(double)Math.Max(useful.Width,useful.Height));
        return new FaceSourceShapeOutputSize(Math.Max(1,(int)Math.Round(useful.Width*scale)),Math.Max(1,(int)Math.Round(useful.Height*scale)));
    }

    public static string CreateCacheKey(string path, long contentRevision, FacePerspectiveRegistrationModel registration,
        int maximumDimension = MaximumPreviewDimension) => FormattableString.Invariant(
        $"{path}|{contentRevision}|{registration.TopLeft.X},{registration.TopLeft.Y}|{registration.TopRight.X},{registration.TopRight.Y}|{registration.BottomRight.X},{registration.BottomRight.Y}|{registration.BottomLeft.X},{registration.BottomLeft.Y}|{maximumDimension}");

    public static FaceArtworkOverridePreviewResult Generate(string path, FaceArtworkOverrideModel value,
        CancellationToken cancellationToken, int maximumDimension = MaximumPreviewDimension)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source=SKBitmap.Decode(path) ?? throw new InvalidDataException($"Artwork Override could not be decoded: '{path}'.");
        var registration=value.PerspectiveRegistration.Normalize();
        if(!registration.IsValid())throw new InvalidOperationException("The Artwork Override perspective registration is invalid.");
        var size=DeterminePreviewSize(source.Width,source.Height,registration,maximumDimension);
        var quad=new[]{registration.TopLeft,registration.TopRight,registration.BottomRight,registration.BottomLeft}
            .Select(point=>new FacePointModel{X=point.X*source.Width,Y=point.Y*source.Height}).ToArray();
        using var rectified=PerspectiveRasterizer.RectifyPreview(source,quad,size.Width,size.Height,cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        using var image=SKImage.FromBitmap(rectified);using var data=image.Encode(SKEncodedImageFormat.Png,90);
        return new FaceArtworkOverridePreviewResult(data.ToArray(),size.Width,size.Height);
    }
}
