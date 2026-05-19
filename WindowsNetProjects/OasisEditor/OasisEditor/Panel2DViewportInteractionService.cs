using System.Windows;

namespace OasisEditor;

internal static class Panel2DViewportInteractionService
{
    public static bool ShouldStartDragSelection(Point startScreenPoint, Point currentScreenPoint, double threshold)
    {
        return (currentScreenPoint - startScreenPoint).Length >= threshold;
    }

    public static bool HasDocumentDelta(Point startDocumentPoint, Point endDocumentPoint, double epsilon = 0.0001d)
    {
        var delta = endDocumentPoint - startDocumentPoint;
        return Math.Abs(delta.X) >= epsilon || Math.Abs(delta.Y) >= epsilon;
    }
}
