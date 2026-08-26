using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SkiaSharp;

namespace OasisEditor;

internal sealed record FaceArtworkOverridePreviewResult(byte[] PngBytes, int Width, int Height);

/// <summary>Creates bounded, cancellable display previews. Production artwork never consumes this path.</summary>
internal static class FaceArtworkOverridePreviewService
{
    public const int MaximumPreviewDimension = 1000;
    public const int MaximumSourceDecodeDimension = 2560;

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
        var displaySource=ReloadableBitmapImageLoader.Load(path,MaximumSourceDecodeDimension)
            ?? throw new InvalidDataException($"Artwork Override could not be decoded: '{path}'.");
        using var source=ToSkBitmap(displaySource);
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

    private static SKBitmap ToSkBitmap(BitmapSource source)
    {
        var converted=source.Format==PixelFormats.Pbgra32?source:new FormatConvertedBitmap(source,PixelFormats.Pbgra32,null,0);
        var stride=checked(converted.PixelWidth*4);var pixels=new byte[checked(stride*converted.PixelHeight)];
        converted.CopyPixels(pixels,stride,0);
        var bitmap=new SKBitmap(new SKImageInfo(converted.PixelWidth,converted.PixelHeight,SKColorType.Bgra8888,SKAlphaType.Premul));
        Marshal.Copy(pixels,0,bitmap.GetPixels(),pixels.Length);
        return bitmap;
    }
}
