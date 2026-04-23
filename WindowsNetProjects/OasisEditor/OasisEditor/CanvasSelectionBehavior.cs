using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace OasisEditor;

public static class CanvasSelectionBehavior
{
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(CanvasSelectionBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

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

    public static bool GetIsEnabled(DependencyObject dependencyObject)
    {
        return (bool)dependencyObject.GetValue(IsEnabledProperty);
    }

    public static void SetIsEnabled(DependencyObject dependencyObject, bool value)
    {
        dependencyObject.SetValue(IsEnabledProperty, value);
    }

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

    private static void OnIsEnabledChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
    {
        if (dependencyObject is not FrameworkElement element)
        {
            return;
        }

        var isEnabled = (bool)eventArgs.NewValue;
        if (isEnabled)
        {
            element.MouseLeftButtonDown += OnMouseLeftButtonDown;
        }
        else
        {
            element.MouseLeftButtonDown -= OnMouseLeftButtonDown;
        }
    }

    private static void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs eventArgs)
    {
        if (sender is not FrameworkElement canvas)
        {
            return;
        }

        var clickedElement = FindSelectableElement(eventArgs.OriginalSource as DependencyObject, canvas);
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
        eventArgs.Handled = true;
    }

    private static FrameworkElement? FindSelectableElement(DependencyObject? source, FrameworkElement canvas)
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

            if (current is Shape shape && ReferenceEquals(shape.Parent, canvas) && GetIsSelectable(shape))
            {
                return shape;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }
}
