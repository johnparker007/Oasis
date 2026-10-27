using System.Windows;

namespace OasisEditor;

internal static class FaceSourceRegionWpfAdapter
{
    public static FaceSourceRegionModel FromRect(Rect rect)
        => rect.IsEmpty
            ? FaceSourceRegionModel.FromBounds(0, 0, 0, 0)
            : FaceSourceRegionModel.FromBounds(rect.X, rect.Y, rect.Width, rect.Height);

    public static Rect ToRect(FaceSourceRegionModel region)
    {
        ArgumentNullException.ThrowIfNull(region);
        return new Rect(region.X, region.Y, region.Width, region.Height);
    }
}
