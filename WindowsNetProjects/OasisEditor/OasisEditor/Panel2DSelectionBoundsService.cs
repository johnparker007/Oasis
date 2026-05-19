using System.Windows;

namespace OasisEditor;

internal static class Panel2DSelectionBoundsService
{
    public static Rect CreateNormalizedDocumentRect(Point startDocumentPoint, Point endDocumentPoint)
    {
        var minX = Math.Min(startDocumentPoint.X, endDocumentPoint.X);
        var maxX = Math.Max(startDocumentPoint.X, endDocumentPoint.X);
        var minY = Math.Min(startDocumentPoint.Y, endDocumentPoint.Y);
        var maxY = Math.Max(startDocumentPoint.Y, endDocumentPoint.Y);

        return new Rect(new Point(minX, minY), new Point(maxX, maxY));
    }
}
