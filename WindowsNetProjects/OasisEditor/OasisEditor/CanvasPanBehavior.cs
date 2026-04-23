using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

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

    public static readonly DependencyProperty IsSelectableProperty =
        DependencyProperty.RegisterAttached(
            "IsSelectable",
            typeof(bool),
            typeof(CanvasPanBehavior),
            new PropertyMetadata(false));

    public static readonly DependencyProperty IsSelectedProperty =
        DependencyProperty.RegisterAttached(
            "IsSelected",
            typeof(bool),
            typeof(CanvasPanBehavior),
            new PropertyMetadata(false));

    public static readonly DependencyProperty IsRectangleToolActiveProperty =
        DependencyProperty.RegisterAttached(
            "IsRectangleToolActive",
            typeof(bool),
            typeof(CanvasPanBehavior),
            new PropertyMetadata(false));

    private static readonly DependencyProperty SelectedElementProperty =
        DependencyProperty.RegisterAttached(
            "SelectedElement",
            typeof(FrameworkElement),
            typeof(CanvasPanBehavior),
            new PropertyMetadata(null));

    private const double MinZoom = 0.25;
    private const double MaxZoom = 4.0;
    private const double ZoomStep = 1.1;
    private const double NewRectangleWidth = 180;
    private const double NewRectangleHeight = 120;

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

    public static bool GetIsRectangleToolActive(DependencyObject dependencyObject)
    {
        return (bool)dependencyObject.GetValue(IsRectangleToolActiveProperty);
    }

    public static void SetIsRectangleToolActive(DependencyObject dependencyObject, bool value)
    {
        dependencyObject.SetValue(IsRectangleToolActiveProperty, value);
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
            element.MouseLeftButtonDown += OnMouseLeftButtonDown;
            element.MouseMove += OnMouseMove;
            element.MouseUp += OnMouseUp;
            element.MouseWheel += OnMouseWheel;
            element.LostMouseCapture += OnLostMouseCapture;
        }
        else
        {
            element.MouseDown -= OnMouseDown;
            element.MouseLeftButtonDown -= OnMouseLeftButtonDown;
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

    private static void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs eventArgs)
    {
        if (sender is not FrameworkElement canvas)
        {
            return;
        }

        var clickedElement = FindSelectableElement(eventArgs.OriginalSource as DependencyObject, canvas);

        if (GetIsRectangleToolActive(canvas) && clickedElement is null)
        {
            AddRectangle(canvas, eventArgs);
            eventArgs.Handled = true;
            return;
        }

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

    private static void AddRectangle(FrameworkElement canvas, MouseButtonEventArgs eventArgs)
    {
        if (canvas is not System.Windows.Controls.Canvas panelCanvas)
        {
            return;
        }

        var clickPosition = eventArgs.GetPosition(panelCanvas.Parent as IInputElement ?? panelCanvas);
        var (scale, translate) = EnsureTransformGroup(panelCanvas);
        var canvasPoint = new Point(
            (clickPosition.X - translate.X) / scale.ScaleX,
            (clickPosition.Y - translate.Y) / scale.ScaleY);

        var rectangle = new Rectangle
        {
            Width = NewRectangleWidth,
            Height = NewRectangleHeight
        };
        SetIsSelectable(rectangle, true);
        SetIsSelected(rectangle, true);

        var x = Math.Max(0, canvasPoint.X - (NewRectangleWidth / 2));
        var y = Math.Max(0, canvasPoint.Y - (NewRectangleHeight / 2));
        System.Windows.Controls.Canvas.SetLeft(rectangle, x);
        System.Windows.Controls.Canvas.SetTop(rectangle, y);
        panelCanvas.Children.Add(rectangle);

        var previousSelection = (FrameworkElement?)panelCanvas.GetValue(SelectedElementProperty);
        if (previousSelection is not null)
        {
            SetIsSelected(previousSelection, false);
        }

        panelCanvas.SetValue(SelectedElementProperty, rectangle);
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

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }
}
