namespace OasisEditor;

public enum FaceSourceRegionKind
{
    Rect
}

/// <summary>A portable rectangular region in Face logical coordinates.</summary>
public sealed class FaceSourceRegionModel
{
    public FaceSourceRegionKind Kind { get; init; } = FaceSourceRegionKind.Rect;
    public double X { get; init; }
    public double Y { get; init; }
    public double Width { get; init; }
    public double Height { get; init; }

    public static FaceSourceRegionModel FromBounds(double x, double y, double width, double height)
    {
        var right = x + width;
        var bottom = y + height;
        var left = Math.Min(x, right);
        var top = Math.Min(y, bottom);
        return new FaceSourceRegionModel
        {
            Kind = FaceSourceRegionKind.Rect,
            X = left,
            Y = top,
            Width = Math.Max(x, right) - left,
            Height = Math.Max(y, bottom) - top
        };
    }

    public bool IsValid => Kind == FaceSourceRegionKind.Rect
        && double.IsFinite(X)
        && double.IsFinite(Y)
        && double.IsFinite(Width)
        && double.IsFinite(Height)
        && Width > 0
        && Height > 0;
}
