using System.Windows;

namespace OasisEditor;

internal static class PanelFaceSourceShapeHitTestService
{
    public static bool TryHitCorner(
        IReadOnlyList<PanelFaceSourceShapeModel> shapes,
        Point documentPoint,
        double hitRadius,
        out PanelFaceSourceShapeModel shape,
        out int cornerIndex)
    {
        shape = new PanelFaceSourceShapeModel();
        cornerIndex = -1;
        var hitRadiusSquared = hitRadius * hitRadius;

        foreach (var candidate in shapes.Reverse())
        {
            var points = GetCornerPoints(candidate);
            for (var i = 0; i < points.Length; i++)
            {
                var dx = points[i].X - documentPoint.X;
                var dy = points[i].Y - documentPoint.Y;
                if ((dx * dx) + (dy * dy) <= hitRadiusSquared)
                {
                    shape = candidate;
                    cornerIndex = i;
                    return true;
                }
            }
        }

        return false;
    }

    private static FacePointModel[] GetCornerPoints(PanelFaceSourceShapeModel shape) =>
        [shape.TopLeft, shape.TopRight, shape.BottomRight, shape.BottomLeft];
}
