using System.Windows;

namespace OasisEditor;

internal static class Panel2DSelectionInteractionService
{
    public static EditorSelectionItem ToSelectionItem(PanelElementModel element) => new(EditorSelectionDomain.PanelElement, element.ObjectId);

    public static IReadOnlyList<EditorSelectionItem> SelectItemsFromRect(IReadOnlyList<PanelElementModel> elements, Rect rect)
    {
        return elements
            .Where(element => element.IsVisible
                              && !string.IsNullOrWhiteSpace(element.ObjectId)
                              && IsFullyEnclosed(element, rect))
            .Select(ToSelectionItem)
            .ToList();
    }

    public static bool IsFullyEnclosed(PanelElementModel element, Rect rect)
    {
        var left = Math.Min(element.X, element.X + element.Width);
        var right = Math.Max(element.X, element.X + element.Width);
        var top = Math.Min(element.Y, element.Y + element.Height);
        var bottom = Math.Max(element.Y, element.Y + element.Height);
        return left >= rect.Left && right <= rect.Right && top >= rect.Top && bottom <= rect.Bottom;
    }

    public static bool CanShowResizeHandles(IReadOnlyList<EditorSelectionItem> selectedItems, PanelElementModel? primaryElement)
    {
        return selectedItems.Count(item => item.Domain == EditorSelectionDomain.PanelElement) == 1
               && primaryElement is not null
               && TransformLockInteractionService.CanMoveOrResize(primaryElement);
    }

    public static bool CanStartGroupMoveFrom(PanelElementModel element, DocumentSelectionState selectionState)
    {
        return TransformLockInteractionService.CanMoveOrResize(element)
               && selectionState.Items.Contains(ToSelectionItem(element));
    }
}
