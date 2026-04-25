using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace OasisEditor;

public static class CanvasPanZoomBehavior
{
    private static readonly DependencyProperty StartPointProperty =
        DependencyProperty.RegisterAttached(
            "StartPoint",
            typeof(Point),
            typeof(CanvasPanZoomBehavior),
            new PropertyMetadata(default(Point)));

    private static readonly DependencyProperty OriginProperty =
        DependencyProperty.RegisterAttached(
            "Origin",
            typeof(Point),
            typeof(CanvasPanZoomBehavior),
            new PropertyMetadata(default(Point)));

    private static readonly DependencyProperty IsPanningProperty =
        DependencyProperty.RegisterAttached(
            "IsPanning",
            typeof(bool),
            typeof(CanvasPanZoomBehavior),
            new PropertyMetadata(false));

    private const double MinZoom = 0.25;
    private const double MaxZoom = 4.0;
    private const double ZoomStep = 1.1;

    public static bool HandleMouseDown(FrameworkElement element, MouseButtonEventArgs eventArgs)
    {
        if (eventArgs.ChangedButton != MouseButton.Middle)
        {
            return false;
        }

        element.Focus();
        var (_, translate) = EnsureTransformGroup(element);
        var startPoint = eventArgs.GetPosition(element.Parent as IInputElement ?? element);
        element.SetValue(StartPointProperty, startPoint);
        element.SetValue(OriginProperty, new Point(translate.X, translate.Y));
        element.SetValue(IsPanningProperty, true);
        element.CaptureMouse();
        element.Cursor = Cursors.SizeAll;
        eventArgs.Handled = true;
        return true;
    }

    public static void HandleMouseMove(FrameworkElement element, MouseEventArgs eventArgs)
    {
        if (!(bool)element.GetValue(IsPanningProperty))
        {
            return;
        }

        var startPoint = (Point)element.GetValue(StartPointProperty);
        var origin = (Point)element.GetValue(OriginProperty);
        var currentPoint = eventArgs.GetPosition(element.Parent as IInputElement ?? element);
        var delta = currentPoint - startPoint;
        var (_, translate) = EnsureTransformGroup(element);
        translate.X = origin.X + delta.X;
        translate.Y = origin.Y + delta.Y;
    }

    public static void HandleMouseWheel(FrameworkElement element, MouseWheelEventArgs eventArgs)
    {
        var (scale, translate) = EnsureTransformGroup(element);
        var pivot = eventArgs.GetPosition(element);
        var previousScale = scale.ScaleX;
        var zoomFactor = eventArgs.Delta > 0 ? ZoomStep : 1.0 / ZoomStep;
        var newScale = Math.Clamp(previousScale * zoomFactor, MinZoom, MaxZoom);
        if (Math.Abs(newScale - previousScale) < 0.0001)
        {
            return;
        }

        scale.ScaleX = newScale;
        scale.ScaleY = newScale;

        // Keep the world-space point under the mouse pointer stable while zooming.
        translate.X += pivot.X * (previousScale - newScale);
        translate.Y += pivot.Y * (previousScale - newScale);
        eventArgs.Handled = true;
    }

    public static void HandleMouseUp(FrameworkElement element, MouseButtonEventArgs eventArgs)
    {
        if (eventArgs.ChangedButton != MouseButton.Middle)
        {
            return;
        }

        EndPan(element);
    }

    public static void HandleLostMouseCapture(FrameworkElement element)
    {
        EndPan(element);
    }

    public static (ScaleTransform Scale, TranslateTransform Translate) EnsureTransformGroup(FrameworkElement element)
    {
        if (element.RenderTransform is TransformGroup existingGroup &&
            existingGroup.Children.OfType<ScaleTransform>().FirstOrDefault() is { } existingScale &&
            existingGroup.Children.OfType<TranslateTransform>().FirstOrDefault() is { } existingTranslate)
        {
            return (existingScale, existingTranslate);
        }

        var transformGroup = new TransformGroup();
        var scale = new ScaleTransform(1, 1);
        var translate = new TranslateTransform();
        transformGroup.Children.Add(scale);
        transformGroup.Children.Add(translate);
        element.RenderTransform = transformGroup;
        return (scale, translate);
    }

    private static void EndPan(FrameworkElement element)
    {
        element.SetValue(IsPanningProperty, false);
        if (element.IsMouseCaptured)
        {
            element.ReleaseMouseCapture();
        }

        element.Cursor = Cursors.Arrow;
    }
}
