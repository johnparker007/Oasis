using System.Windows;
using System.Windows.Media;

namespace OasisEditor;

public static class CanvasSelectionBehavior
{
    public static readonly DependencyProperty IsSelectableProperty =
        DependencyProperty.RegisterAttached(
            "IsSelectable",
            typeof(bool),
            typeof(CanvasSelectionBehavior),
            new PropertyMetadata(false));

    public static readonly DependencyProperty IsSelectedProperty =
        DependencyProperty.RegisterAttached(
            "IsSelected",
            typeof(bool),
            typeof(CanvasSelectionBehavior),
            new PropertyMetadata(false));

    private static readonly DependencyProperty SelectedElementProperty =
        DependencyProperty.RegisterAttached(
            "SelectedElement",
            typeof(FrameworkElement),
            typeof(CanvasSelectionBehavior),
            new PropertyMetadata(null));

    public static bool GetIsSelectable(DependencyObject dependencyObject)
    {
        return (bool)dependencyObject.GetValue(IsSelectableProperty);
    }

    public static void SetIsSelectable(DependencyObject dependencyObject, bool value)
    {
        dependencyObject.SetValue(IsSelectableProperty, value);
    }

    public static bool GetIsSelected(DependencyObject dependencyObject)
    {
        return (bool)dependencyObject.GetValue(IsSelectedProperty);
    }

    public static void SetIsSelected(DependencyObject dependencyObject, bool value)
    {
        dependencyObject.SetValue(IsSelectedProperty, value);
    }

    public static void SelectFromSource(FrameworkElement canvas, DependencyObject? source)
    {
        var clickedElement = FindSelectableElement(source, canvas);
        var selectedElement = (FrameworkElement?)canvas.GetValue(SelectedElementProperty);

        if (ReferenceEquals(clickedElement, selectedElement))
        {
            return;
        }

        if (selectedElement is not null)
        {
            SetIsSelected(selectedElement, false);
        }

        if (clickedElement is null)
        {
            canvas.ClearValue(SelectedElementProperty);
            return;
        }

        SetIsSelected(clickedElement, true);
        canvas.SetValue(SelectedElementProperty, clickedElement);
    }

    public static void ClearSelection(FrameworkElement canvas)
    {
        var selectedElement = (FrameworkElement?)canvas.GetValue(SelectedElementProperty);
        if (selectedElement is not null)
        {
            SetIsSelected(selectedElement, false);
        }

        canvas.ClearValue(SelectedElementProperty);
    }

    public static void SelectElement(FrameworkElement canvas, FrameworkElement? element)
    {
        var selectedElement = (FrameworkElement?)canvas.GetValue(SelectedElementProperty);
        if (ReferenceEquals(selectedElement, element))
        {
            return;
        }

        if (selectedElement is not null)
        {
            SetIsSelected(selectedElement, false);
        }

        if (element is null)
        {
            canvas.ClearValue(SelectedElementProperty);
            return;
        }

        SetIsSelected(element, true);
        canvas.SetValue(SelectedElementProperty, element);
    }

    public static FrameworkElement? FindSelectableElement(DependencyObject? source, FrameworkElement canvas)
    {
        var current = source;
        while (current is not null)
        {
            if (current is FrameworkElement element && GetIsSelectable(element))
            {
                return element;
            }

            if (ReferenceEquals(current, canvas))
            {
                return null;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }
}
