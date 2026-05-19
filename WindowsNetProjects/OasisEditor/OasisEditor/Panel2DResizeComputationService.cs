using System.Windows;

namespace OasisEditor;

internal static class Panel2DResizeComputationService
{
    private const double MinSize = 1d;

    public static PanelElementModel ComputeResizedElement(
        PanelElementModel sourceElement,
        ResizeHandleKind handleKind,
        Point startDocumentPoint,
        Point endDocumentPoint)
    {
        ArgumentNullException.ThrowIfNull(sourceElement);

        var dx = endDocumentPoint.X - startDocumentPoint.X;
        var dy = endDocumentPoint.Y - startDocumentPoint.Y;

        var x = sourceElement.X;
        var y = sourceElement.Y;
        var width = sourceElement.Width;
        var height = sourceElement.Height;

        if (handleKind is ResizeHandleKind.Left or ResizeHandleKind.TopLeft or ResizeHandleKind.BottomLeft)
        {
            x += dx;
            width -= dx;
        }

        if (handleKind is ResizeHandleKind.Right or ResizeHandleKind.TopRight or ResizeHandleKind.BottomRight)
        {
            width += dx;
        }

        if (handleKind is ResizeHandleKind.Top or ResizeHandleKind.TopLeft or ResizeHandleKind.TopRight)
        {
            y += dy;
            height -= dy;
        }

        if (handleKind is ResizeHandleKind.Bottom or ResizeHandleKind.BottomLeft or ResizeHandleKind.BottomRight)
        {
            height += dy;
        }

        if (width < MinSize)
        {
            width = MinSize;
        }

        if (height < MinSize)
        {
            height = MinSize;
        }

        return PanelElementModelCloner.Clone(sourceElement, x: x, y: y, width: width, height: height);
    }
}
