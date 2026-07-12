
namespace OasisEditor;

internal interface IPanelSelectableObject
{
    string ObjectId { get; }
    PanelElementKind ElementKind { get; }
    double X { get; }
    double Y { get; }
    double Width { get; }
    double Height { get; }
}

internal static class PanelSelectionContract
{
    public static PanelSelectionInfo ToSelectionInfo(IPanelSelectableObject selectable)
    {
        return new PanelSelectionInfo(
            selectable.ObjectId,
            Panel2DDocumentStorage.SerializeElementKind(selectable.ElementKind),
            selectable.X,
            selectable.Y,
            selectable.Width,
            selectable.Height);
    }

    public static bool IsMatch(IPanelSelectableObject selectable, PanelSelectionInfo selection)
    {
        if (!string.IsNullOrWhiteSpace(selection.ObjectId)
            && !string.IsNullOrWhiteSpace(selectable.ObjectId))
        {
            if (string.Equals(selectable.ObjectId, selection.ObjectId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return IsTypeAndBoundsMatch(selectable, selection);
    }

    public static bool IsTypeAndBoundsMatch(IPanelSelectableObject selectable, PanelSelectionInfo selection)
    {
        return selectable.ElementKind == Panel2DDocumentStorage.ParseElementKind(selection.Kind)
            && AreClose(selectable.X, selection.X)
            && AreClose(selectable.Y, selection.Y)
            && AreClose(selectable.Width, selection.Width)
            && AreClose(selectable.Height, selection.Height);
    }

    public static bool IsSameSelection(PanelSelectionInfo left, PanelSelectionInfo right)
    {
        if (!string.IsNullOrWhiteSpace(left.ObjectId)
            && !string.IsNullOrWhiteSpace(right.ObjectId))
        {
            return string.Equals(left.ObjectId, right.ObjectId, StringComparison.Ordinal);
        }

        return Panel2DDocumentStorage.ParseElementKind(left.Kind) == Panel2DDocumentStorage.ParseElementKind(right.Kind)
            && AreClose(left.X, right.X)
            && AreClose(left.Y, right.Y)
            && AreClose(left.Width, right.Width)
            && AreClose(left.Height, right.Height);
    }

    public static bool IsSame(IPanelSelectableObject left, IPanelSelectableObject right)
    {
        if (!string.IsNullOrWhiteSpace(left.ObjectId)
            && !string.IsNullOrWhiteSpace(right.ObjectId))
        {
            return string.Equals(left.ObjectId, right.ObjectId, StringComparison.Ordinal);
        }

        return left.ElementKind == right.ElementKind
            && AreClose(left.X, right.X)
            && AreClose(left.Y, right.Y)
            && AreClose(left.Width, right.Width)
            && AreClose(left.Height, right.Height);
    }

    private static bool AreClose(double left, double right)
    {
        return Math.Abs(left - right) < 0.01d;
    }

}
