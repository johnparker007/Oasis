namespace OasisEditor;

internal static class Panel2DResizeHandleService
{
    public static IReadOnlyList<ResizeHandle> GetHandles(PanelElementModel element)
    {
        ArgumentNullException.ThrowIfNull(element);

        var left = element.X;
        var right = element.X + element.Width;
        var top = element.Y;
        var bottom = element.Y + element.Height;
        var midX = (left + right) / 2d;
        var midY = (top + bottom) / 2d;

        return
        [
            new ResizeHandle(ResizeHandleKind.TopLeft, left, top),
            new ResizeHandle(ResizeHandleKind.Top, midX, top),
            new ResizeHandle(ResizeHandleKind.TopRight, right, top),
            new ResizeHandle(ResizeHandleKind.Left, left, midY),
            new ResizeHandle(ResizeHandleKind.Right, right, midY),
            new ResizeHandle(ResizeHandleKind.BottomLeft, left, bottom),
            new ResizeHandle(ResizeHandleKind.Bottom, midX, bottom),
            new ResizeHandle(ResizeHandleKind.BottomRight, right, bottom)
        ];
    }
}

internal readonly record struct ResizeHandle(ResizeHandleKind Kind, double X, double Y);

internal enum ResizeHandleKind
{
    None,
    TopLeft,
    Top,
    TopRight,
    Left,
    Right,
    BottomLeft,
    Bottom,
    BottomRight
}
