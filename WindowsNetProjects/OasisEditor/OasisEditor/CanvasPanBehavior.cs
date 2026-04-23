using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace OasisEditor;

public static class CanvasPanBehavior
{
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(CanvasPanBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    private static readonly DependencyProperty StartPointProperty =
        DependencyProperty.RegisterAttached(
            "StartPoint",
            typeof(Point),
            typeof(CanvasPanBehavior),
            new PropertyMetadata(default(Point)));

    private static readonly DependencyProperty OriginProperty =
        DependencyProperty.RegisterAttached(
            "Origin",
            typeof(Point),
            typeof(CanvasPanBehavior),
            new PropertyMetadata(default(Point)));

    private static readonly DependencyProperty IsPanningProperty =
        DependencyProperty.RegisterAttached(
            "IsPanning",
            typeof(bool),
            typeof(CanvasPanBehavior),
            new PropertyMetadata(false));

    public static bool GetIsEnabled(DependencyObject dependencyObject)
    {
        return (bool)dependencyObject.GetValue(IsEnabledProperty);
    }

    public static void SetIsEnabled(DependencyObject dependencyObject, bool value)
    {
        dependencyObject.SetValue(IsEnabledProperty, value);
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
            EnsureTranslateTransform(element);
            element.MouseDown += OnMouseDown;
            element.MouseMove += OnMouseMove;
            element.MouseUp += OnMouseUp;
            element.LostMouseCapture += OnLostMouseCapture;
        }
        else
        {
            element.MouseDown -= OnMouseDown;
            element.MouseMove -= OnMouseMove;
            element.MouseUp -= OnMouseUp;
            element.LostMouseCapture -= OnLostMouseCapture;
        }
    }

    private static void OnMouseDown(object sender, MouseButtonEventArgs eventArgs)
    {
        if (sender is not FrameworkElement element)
        {
            return;
        }

        if (eventArgs.ChangedButton != MouseButton.Middle)
        {
            return;
        }

        EnsureTranslateTransform(element);
        var startPoint = eventArgs.GetPosition(element.Parent as IInputElement ?? element);
        var transform = (TranslateTransform)element.RenderTransform;
        element.SetValue(StartPointProperty, startPoint);
        element.SetValue(OriginProperty, new Point(transform.X, transform.Y));
        element.SetValue(IsPanningProperty, true);
        element.CaptureMouse();
        element.Cursor = Cursors.SizeAll;
        eventArgs.Handled = true;
    }

    private static void OnMouseMove(object sender, MouseEventArgs eventArgs)
    {
        if (sender is not FrameworkElement element || !(bool)element.GetValue(IsPanningProperty))
        {
            return;
        }

        var startPoint = (Point)element.GetValue(StartPointProperty);
        var origin = (Point)element.GetValue(OriginProperty);
        var currentPoint = eventArgs.GetPosition(element.Parent as IInputElement ?? element);
        var delta = currentPoint - startPoint;
        var transform = (TranslateTransform)element.RenderTransform;
        transform.X = origin.X + delta.X;
        transform.Y = origin.Y + delta.Y;
    }

    private static void OnMouseUp(object sender, MouseButtonEventArgs eventArgs)
    {
        if (eventArgs.ChangedButton != MouseButton.Middle)
        {
            return;
        }

        EndPan(sender as FrameworkElement);
    }

    private static void OnLostMouseCapture(object sender, MouseEventArgs eventArgs)
    {
        EndPan(sender as FrameworkElement);
    }

    private static void EndPan(FrameworkElement? element)
    {
        if (element is null)
        {
            return;
        }

        element.SetValue(IsPanningProperty, false);
        if (element.IsMouseCaptured)
        {
            element.ReleaseMouseCapture();
        }

        element.Cursor = Cursors.Arrow;
    }

    private static void EnsureTranslateTransform(FrameworkElement element)
    {
        if (element.RenderTransform is TranslateTransform)
        {
            return;
        }

        element.RenderTransform = new TranslateTransform();
    }
}
