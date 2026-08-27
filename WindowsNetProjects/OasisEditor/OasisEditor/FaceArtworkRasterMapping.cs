using SkiaSharp;

namespace OasisEditor;

/// <summary>Shared mapping of the displayed artwork crop to its authored Face destination.</summary>
public static class FaceArtworkRasterMapping
{
    public static SKRect ResolveSourceRect(FaceArtworkElement element, int imageWidth, int imageHeight)
    {
        var sourceRegion = element.SourceRegion;
        var sourceBounds = element.Provenance?.SourceElementBounds;
        if (sourceRegion is null || sourceBounds is null || sourceBounds.Width <= 0d || sourceBounds.Height <= 0d)
            return SKRect.Create(0f, 0f, imageWidth, imageHeight);

        var scaleX = imageWidth / sourceBounds.Width;
        var scaleY = imageHeight / sourceBounds.Height;
        var x = (sourceRegion.X - sourceBounds.X) * scaleX;
        var y = (sourceRegion.Y - sourceBounds.Y) * scaleY;
        var width = sourceRegion.Width * scaleX;
        var height = sourceRegion.Height * scaleY;
        var left = (float)Math.Clamp(x, 0d, imageWidth);
        var top = (float)Math.Clamp(y, 0d, imageHeight);
        var right = (float)Math.Clamp(x + width, left, imageWidth);
        var bottom = (float)Math.Clamp(y + height, top, imageHeight);
        return new SKRect(left, top, right, bottom);
    }

    public static EditorViewportContentScale ContentScale(FaceArtworkElement element, int imageWidth, int imageHeight)
    {
        var source = ResolveSourceRect(element, imageWidth, imageHeight);
        return EditorViewportContentScale.FromMapping(source.Width, source.Height, element.Width, element.Height);
    }
}
