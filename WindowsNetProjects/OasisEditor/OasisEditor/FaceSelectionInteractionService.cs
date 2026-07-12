using System.Windows;

namespace OasisEditor;

internal static class FaceSelectionInteractionService
{
    public static EditorSelectionItem ToSelectionItem(FaceElementModel element) => new(EditorSelectionDomain.FaceElement, element.ObjectId);

    public static IReadOnlyList<EditorSelectionItem> SelectItemsFromRect(IReadOnlyList<FaceElementModel> elements, Rect rect)
    {
        return elements
            .Where(element => element.IsVisible
                              && !string.IsNullOrWhiteSpace(element.ObjectId)
                              && IsFullyEnclosed(element, rect))
            .Select(ToSelectionItem)
            .ToList();
    }

    public static bool IsFullyEnclosed(FaceElementModel element, Rect rect)
    {
        var left = Math.Min(element.X, element.X + element.Width);
        var right = Math.Max(element.X, element.X + element.Width);
        var top = Math.Min(element.Y, element.Y + element.Height);
        var bottom = Math.Max(element.Y, element.Y + element.Height);
        return left >= rect.Left && right <= rect.Right && top >= rect.Top && bottom <= rect.Bottom;
    }

    public static bool CanStartGroupMoveFrom(FaceElementModel element, DocumentSelectionState selectionState)
    {
        return TransformLockInteractionService.CanMoveOrResize(element)
               && selectionState.Items.Contains(ToSelectionItem(element));
    }
}
