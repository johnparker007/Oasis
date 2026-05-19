using System.Windows;

namespace OasisEditor;

internal static class Panel2DResizeHandleHitTestService
{
    public static bool TryHitHandle(
        PanelElementModel element,
        Point documentPoint,
        double handleSizeDocumentSpace,
        out ResizeHandleKind handleKind)
    {
        ArgumentNullException.ThrowIfNull(element);

        handleKind = ResizeHandleKind.None;
        var half = handleSizeDocumentSpace / 2d;
        var handles = Panel2DResizeHandleService.GetHandles(element);
        foreach (var handle in handles)
        {
            if (documentPoint.X >= handle.X - half
                && documentPoint.X <= handle.X + half
                && documentPoint.Y >= handle.Y - half
                && documentPoint.Y <= handle.Y + half)
            {
                handleKind = handle.Kind;
                return true;
            }
        }

        return false;
    }
}
