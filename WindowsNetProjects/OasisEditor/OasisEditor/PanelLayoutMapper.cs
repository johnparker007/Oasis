using System.Windows;
using System.Windows.Controls;

namespace OasisEditor;

public static class PanelLayoutMapper
{
    private static readonly DependencyProperty IsApplyingLayoutProperty =
        DependencyProperty.RegisterAttached(
            "IsApplyingLayout",
            typeof(bool),
            typeof(PanelLayoutMapper),
            new PropertyMetadata(false));

    private static readonly DependencyProperty IsPersistedElementProperty =
        DependencyProperty.RegisterAttached(
            "IsPersistedElement",
            typeof(bool),
            typeof(PanelLayoutMapper),
            new PropertyMetadata(false));

    public static bool IsApplyingLayout(Canvas canvas)
    {
        return (bool)canvas.GetValue(IsApplyingLayoutProperty);
    }

    public static void ApplyPersistedLayout(Canvas canvas, string? layoutJson)
    {
        canvas.SetValue(IsApplyingLayoutProperty, true);
        try
        {
            var persistedChildren = canvas.Children
                .OfType<FrameworkElement>()
                .Where(GetIsPersistedElement)
                .ToList();

            foreach (var child in persistedChildren)
            {
                canvas.Children.Remove(child);
            }

            var elements = Panel2DDocumentStorage.DeserializeLayout(layoutJson);
            foreach (var element in elements)
            {
                if (element.IsVisible is false)
                {
                    continue;
                }

                var visual = PanelElementFactory.CreateVisualFromElement(element);
                if (visual is null)
                {
                    continue;
                }

                SetIsPersistedElement(visual, true);
                CanvasSelectionBehavior.SetIsSelectable(visual, !element.IsLocked);
                CanvasSelectionBehavior.SetIsSelected(visual, false);
                Canvas.SetLeft(visual, Math.Max(0, element.X));
                Canvas.SetTop(visual, Math.Max(0, element.Y));
                canvas.Children.Add(visual);
            }

            CanvasSelectionBehavior.ClearSelection(canvas);
            if (canvas.DataContext is DocumentTabViewModel tab)
            {
                tab.PanelLayoutJson = Panel2DDocumentStorage.SerializeLayout(elements);
            }
        }
        finally
        {
            canvas.SetValue(IsApplyingLayoutProperty, false);
        }
    }

    public static void SyncPanelLayout(Canvas canvas, DependencyProperty panelLayoutJsonProperty)
    {
        if (IsApplyingLayout(canvas))
        {
            return;
        }

        var layoutJson = canvas.DataContext is DocumentTabViewModel tab
            ? tab.GetPanelLayoutProjectionJson()
            : Panel2DDocumentStorage.SerializeLayout(
                canvas.Children
                    .OfType<FrameworkElement>()
                    .Where(GetIsPersistedElement)
                    .Select(PanelElementFactory.CreateElementFromVisual)
                    .Where(element => element is not null)
                    .Cast<PanelElementFile>()
                    .ToArray());
        canvas.SetValue(IsApplyingLayoutProperty, true);
        try
        {
            canvas.SetCurrentValue(panelLayoutJsonProperty, layoutJson);
            if (canvas.DataContext is DocumentTabViewModel tabViewModel)
            {
                tabViewModel.PanelLayoutJson = layoutJson;
            }
        }
        finally
        {
            canvas.SetValue(IsApplyingLayoutProperty, false);
        }
    }

    public static void ApplyVisualState(Canvas canvas, DocumentTabViewModel tab, PanelVisualStateChangedEvent visualStateChange)
    {
        if (visualStateChange.ValuesByObjectId.Count == 0)
        {
            return;
        }

        var elementsByObjectId = tab.GetPanelElements()
            .Where(element => !string.IsNullOrWhiteSpace(element.ObjectId))
            .ToDictionary(element => element.ObjectId, element => element, StringComparer.Ordinal);

        var currentPersistedChildren = canvas.Children
            .OfType<FrameworkElement>()
            .Where(GetIsPersistedElement)
            .ToArray();

        foreach (var existingVisual in currentPersistedChildren)
        {
            var objectId = existingVisual.Uid?.Trim();
            if (string.IsNullOrWhiteSpace(objectId)
                || !visualStateChange.ValuesByObjectId.ContainsKey(objectId)
                || !elementsByObjectId.TryGetValue(objectId, out var sourceModel))
            {
                continue;
            }

            var updatedVisual = PanelElementFactory.CreateVisualFromElement(Panel2DDocumentStorage.ToStorageElement(sourceModel));
            if (updatedVisual is null)
            {
                continue;
            }

            var childIndex = canvas.Children.IndexOf(existingVisual);
            if (childIndex < 0)
            {
                continue;
            }

            SetIsPersistedElement(updatedVisual, true);
            CanvasSelectionBehavior.SetIsSelectable(updatedVisual, CanvasSelectionBehavior.GetIsSelectable(existingVisual));
            CanvasSelectionBehavior.SetIsSelected(updatedVisual, CanvasSelectionBehavior.GetIsSelected(existingVisual));
            Canvas.SetLeft(updatedVisual, Canvas.GetLeft(existingVisual));
            Canvas.SetTop(updatedVisual, Canvas.GetTop(existingVisual));
            canvas.Children.RemoveAt(childIndex);
            canvas.Children.Insert(childIndex, updatedVisual);
        }
    }

    private static bool GetIsPersistedElement(FrameworkElement element)
    {
        return (bool)element.GetValue(IsPersistedElementProperty);
    }

    private static void SetIsPersistedElement(FrameworkElement element, bool value)
    {
        element.SetValue(IsPersistedElementProperty, value);
    }
}
