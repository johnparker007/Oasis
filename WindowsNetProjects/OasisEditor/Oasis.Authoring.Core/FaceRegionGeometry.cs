using OasisEditor;

namespace OasisEditor.FacePipeline;

internal readonly record struct FacePoint(double X, double Y);

internal struct FaceRect
{
    public double X { get; private set; }
    public double Y { get; private set; }
    public double Width { get; private set; }
    public double Height { get; private set; }
    public FaceRect(double x, double y, double width, double height) { X = x; Y = y; Width = width; Height = height; }
    public static FaceRect Empty => default;
    public bool IsEmpty => Width <= 0d || Height <= 0d;
    public void Inflate(double horizontal, double vertical)
    { X -= horizontal; Y -= vertical; Width += horizontal * 2d; Height += vertical * 2d; }
    public static FaceRect Union(FaceRect left, FaceRect right)
    {
        if (left.IsEmpty) return right;
        if (right.IsEmpty) return left;
        var x = Math.Min(left.X, right.X); var y = Math.Min(left.Y, right.Y);
        return new FaceRect(x, y, Math.Max(left.X + left.Width, right.X + right.Width) - x,
            Math.Max(left.Y + left.Height, right.Y + right.Height) - y);
    }
    public static FaceRect Intersect(FaceRect left, FaceRect right)
    {
        var x = Math.Max(left.X, right.X); var y = Math.Max(left.Y, right.Y);
        var rightEdge = Math.Min(left.X + left.Width, right.X + right.Width);
        var bottom = Math.Min(left.Y + left.Height, right.Y + right.Height);
        return rightEdge <= x || bottom <= y ? Empty : new FaceRect(x, y, rightEdge - x, bottom - y);
    }
}

internal static class FaceSourceRegionGeometryAdapter
{
    public static FaceRect ToRect(FaceSourceRegionModel value) => new(value.X, value.Y, value.Width, value.Height);
    public static FaceSourceRegionModel FromRect(FaceRect value) => FaceSourceRegionModel.FromBounds(value.X, value.Y, value.Width, value.Height);
}
