namespace OasisEditor;

internal static class ResizeHandleCursorService
{
    public static ResizeHandleCursorKind GetCursorKind(ResizeHandleKind handleKind)
    {
        return handleKind switch
        {
            ResizeHandleKind.Left or ResizeHandleKind.Right => ResizeHandleCursorKind.SizeWE,
            ResizeHandleKind.Top or ResizeHandleKind.Bottom => ResizeHandleCursorKind.SizeNS,
            ResizeHandleKind.TopLeft or ResizeHandleKind.BottomRight => ResizeHandleCursorKind.SizeNWSE,
            ResizeHandleKind.TopRight or ResizeHandleKind.BottomLeft => ResizeHandleCursorKind.SizeNESW,
            _ => ResizeHandleCursorKind.Arrow
        };
    }
}

internal enum ResizeHandleCursorKind
{
    Arrow,
    SizeWE,
    SizeNS,
    SizeNWSE,
    SizeNESW
}
