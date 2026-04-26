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
                var visual = PanelElementFactory.CreateVisualFromElement(element);
                if (visual is null)
                {
                    continue;
                }

                SetIsPersistedElement(visual, true);
                CanvasSelectionBehavior.SetIsSelectable(visual, true);
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

    private static bool GetIsPersistedElement(FrameworkElement element)
    {
        return (bool)element.GetValue(IsPersistedElementProperty);
    }

    private static void SetIsPersistedElement(FrameworkElement element, bool value)
    {
        element.SetValue(IsPersistedElementProperty, value);
    }
}
