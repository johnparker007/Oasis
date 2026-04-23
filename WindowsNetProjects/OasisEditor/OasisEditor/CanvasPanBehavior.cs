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

    private const double MinZoom = 0.25;
    private const double MaxZoom = 4.0;
    private const double ZoomStep = 1.1;

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
            EnsureTransformGroup(element);
            element.MouseDown += OnMouseDown;
            element.MouseMove += OnMouseMove;
            element.MouseUp += OnMouseUp;
            element.MouseWheel += OnMouseWheel;
            element.LostMouseCapture += OnLostMouseCapture;
        }
        else
        {
            element.MouseDown -= OnMouseDown;
            element.MouseMove -= OnMouseMove;
            element.MouseUp -= OnMouseUp;
            element.MouseWheel -= OnMouseWheel;
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

        var (_, translate) = EnsureTransformGroup(element);
        var startPoint = eventArgs.GetPosition(element.Parent as IInputElement ?? element);
        element.SetValue(StartPointProperty, startPoint);
        element.SetValue(OriginProperty, new Point(translate.X, translate.Y));
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
        var (_, translate) = EnsureTransformGroup(element);
        translate.X = origin.X + delta.X;
        translate.Y = origin.Y + delta.Y;
    }

    private static void OnMouseWheel(object sender, MouseWheelEventArgs eventArgs)
    {
        if (sender is not FrameworkElement element)
        {
            return;
        }

        var (scale, translate) = EnsureTransformGroup(element);
        var parent = element.Parent as IInputElement ?? element;
        var pivot = eventArgs.GetPosition(parent);
        var zoomFactor = eventArgs.Delta > 0 ? ZoomStep : 1.0 / ZoomStep;
        var newScale = Math.Clamp(scale.ScaleX * zoomFactor, MinZoom, MaxZoom);
        if (Math.Abs(newScale - scale.ScaleX) < 0.0001)
        {
            return;
        }

        var worldX = (pivot.X - translate.X) / scale.ScaleX;
        var worldY = (pivot.Y - translate.Y) / scale.ScaleY;

        scale.ScaleX = newScale;
        scale.ScaleY = newScale;
        translate.X = pivot.X - (worldX * newScale);
        translate.Y = pivot.Y - (worldY * newScale);
        eventArgs.Handled = true;
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

    private static (ScaleTransform Scale, TranslateTransform Translate) EnsureTransformGroup(FrameworkElement element)
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
}
