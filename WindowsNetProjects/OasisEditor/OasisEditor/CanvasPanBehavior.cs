using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Linq;
using OasisEditor.Commands;

namespace OasisEditor;

public static class CanvasPanBehavior
{
    public static readonly RoutedUICommand UndoCommand = new("Undo", "Undo", typeof(CanvasPanBehavior));
    public static readonly RoutedUICommand RedoCommand = new("Redo", "Redo", typeof(CanvasPanBehavior));

    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(CanvasPanBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static readonly DependencyProperty IsRectangleToolActiveProperty =
        DependencyProperty.RegisterAttached(
            "IsRectangleToolActive",
            typeof(bool),
            typeof(CanvasPanBehavior),
            new PropertyMetadata(false));

    public static readonly DependencyProperty IsImageToolActiveProperty =
        DependencyProperty.RegisterAttached(
            "IsImageToolActive",
            typeof(bool),
            typeof(CanvasPanBehavior),
            new PropertyMetadata(false));

    public static readonly DependencyProperty IsSkiaRuntimeRenderingEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsSkiaRuntimeRenderingEnabled",
            typeof(bool),
            typeof(CanvasPanBehavior),
            new PropertyMetadata(false));

    public static readonly DependencyProperty PanelLayoutJsonProperty =
        DependencyProperty.RegisterAttached(
            "PanelLayoutJson",
            typeof(string),
            typeof(CanvasPanBehavior),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnPanelLayoutJsonChanged));

    public static readonly DependencyProperty SelectedPanelSelectionProperty =
        DependencyProperty.RegisterAttached(
            "SelectedPanelSelection",
            typeof(PanelSelectionInfo?),
            typeof(CanvasPanBehavior),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedPanelSelectionChanged));

    public static readonly DependencyProperty PanelZoomProperty =
        DependencyProperty.RegisterAttached(
            "PanelZoom",
            typeof(double),
            typeof(CanvasPanBehavior),
            new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnPanelViewportStateChanged));

    public static readonly DependencyProperty PanelPanXProperty =
        DependencyProperty.RegisterAttached(
            "PanelPanX",
            typeof(double),
            typeof(CanvasPanBehavior),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnPanelViewportStateChanged));

    public static readonly DependencyProperty PanelPanYProperty =
        DependencyProperty.RegisterAttached(
            "PanelPanY",
            typeof(double),
            typeof(CanvasPanBehavior),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnPanelViewportStateChanged));

    private static readonly DependencyProperty IsSyncingViewportStateProperty =
        DependencyProperty.RegisterAttached(
            "IsSyncingViewportState",
            typeof(bool),
            typeof(CanvasPanBehavior),
            new PropertyMetadata(false));

    private static readonly DependencyProperty SubscribedDocumentTabProperty =
        DependencyProperty.RegisterAttached(
            "SubscribedDocumentTab",
            typeof(DocumentTabViewModel),
            typeof(CanvasPanBehavior),
            new PropertyMetadata(null));

    public static bool GetIsEnabled(DependencyObject dependencyObject)
    {
        return (bool)dependencyObject.GetValue(IsEnabledProperty);
    }

    public static void SetIsEnabled(DependencyObject dependencyObject, bool value)
    {
        dependencyObject.SetValue(IsEnabledProperty, value);
    }

    public static bool GetIsRectangleToolActive(DependencyObject dependencyObject)
    {
        return (bool)dependencyObject.GetValue(IsRectangleToolActiveProperty);
    }

    public static void SetIsRectangleToolActive(DependencyObject dependencyObject, bool value)
    {
        dependencyObject.SetValue(IsRectangleToolActiveProperty, value);
    }

    public static bool GetIsImageToolActive(DependencyObject dependencyObject)
    {
        return (bool)dependencyObject.GetValue(IsImageToolActiveProperty);
    }

    public static void SetIsImageToolActive(DependencyObject dependencyObject, bool value)
    {
        dependencyObject.SetValue(IsImageToolActiveProperty, value);
    }

    public static bool GetIsSkiaRuntimeRenderingEnabled(DependencyObject dependencyObject)
    {
        return (bool)dependencyObject.GetValue(IsSkiaRuntimeRenderingEnabledProperty);
    }

    public static void SetIsSkiaRuntimeRenderingEnabled(DependencyObject dependencyObject, bool value)
    {
        dependencyObject.SetValue(IsSkiaRuntimeRenderingEnabledProperty, value);
    }

    public static string? GetPanelLayoutJson(DependencyObject dependencyObject)
    {
        return (string?)dependencyObject.GetValue(PanelLayoutJsonProperty);
    }

    public static void SetPanelLayoutJson(DependencyObject dependencyObject, string? value)
    {
        dependencyObject.SetValue(PanelLayoutJsonProperty, value);
    }

    public static PanelSelectionInfo? GetSelectedPanelSelection(DependencyObject dependencyObject)
    {
        return (PanelSelectionInfo?)dependencyObject.GetValue(SelectedPanelSelectionProperty);
    }

    public static void SetSelectedPanelSelection(DependencyObject dependencyObject, PanelSelectionInfo? value)
    {
        dependencyObject.SetValue(SelectedPanelSelectionProperty, value);
    }

    public static double GetPanelZoom(DependencyObject dependencyObject)
    {
        return (double)dependencyObject.GetValue(PanelZoomProperty);
    }

    public static void SetPanelZoom(DependencyObject dependencyObject, double value)
    {
        dependencyObject.SetValue(PanelZoomProperty, value);
    }

    public static double GetPanelPanX(DependencyObject dependencyObject)
    {
        return (double)dependencyObject.GetValue(PanelPanXProperty);
    }

    public static void SetPanelPanX(DependencyObject dependencyObject, double value)
    {
        dependencyObject.SetValue(PanelPanXProperty, value);
    }

    public static double GetPanelPanY(DependencyObject dependencyObject)
    {
        return (double)dependencyObject.GetValue(PanelPanYProperty);
    }

    public static void SetPanelPanY(DependencyObject dependencyObject, double value)
    {
        dependencyObject.SetValue(PanelPanYProperty, value);
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
            CanvasPanZoomBehavior.EnsureTransformGroup(element);
            ApplyViewportStateToCanvas(element);
            element.MouseDown += OnMouseDown;
            element.MouseLeftButtonDown += OnMouseLeftButtonDown;
            element.MouseMove += OnMouseMove;
            element.MouseUp += OnMouseUp;
            element.MouseWheel += OnMouseWheel;
            element.LostMouseCapture += OnLostMouseCapture;
            element.DataContextChanged += OnCanvasDataContextChanged;
            AttachVisualStateSubscription(element);
        }
        else
        {
            element.MouseDown -= OnMouseDown;
            element.MouseLeftButtonDown -= OnMouseLeftButtonDown;
            element.MouseMove -= OnMouseMove;
            element.MouseUp -= OnMouseUp;
            element.MouseWheel -= OnMouseWheel;
            element.LostMouseCapture -= OnLostMouseCapture;
            element.DataContextChanged -= OnCanvasDataContextChanged;
            DetachVisualStateSubscription(element);
        }
    }

    private static void OnCanvasDataContextChanged(object sender, DependencyPropertyChangedEventArgs _)
    {
        if (sender is not FrameworkElement element)
        {
            return;
        }

        DetachVisualStateSubscription(element);
        AttachVisualStateSubscription(element);
    }

    private static void AttachVisualStateSubscription(FrameworkElement element)
    {
        if (element.DataContext is not DocumentTabViewModel tab)
        {
            return;
        }

        tab.PanelVisualStateChanged += OnPanelVisualStateChanged;
        element.SetValue(SubscribedDocumentTabProperty, tab);
    }

    private static void DetachVisualStateSubscription(FrameworkElement element)
    {
        if (element.GetValue(SubscribedDocumentTabProperty) is not DocumentTabViewModel tab)
        {
            return;
        }

        tab.PanelVisualStateChanged -= OnPanelVisualStateChanged;
        element.ClearValue(SubscribedDocumentTabProperty);
    }

    private static void OnPanelVisualStateChanged(PanelVisualStateChangedEvent visualStateChanged)
    {
        foreach (Window window in Application.Current.Windows)
        {
            foreach (var canvas in FindVisualChildren<Canvas>(window))
            {
                if (canvas.DataContext is not DocumentTabViewModel tab || tab.DocumentId != visualStateChanged.DocumentId)
                {
                    continue;
                }

                if (!GetIsSkiaRuntimeRenderingEnabled(canvas))
                {
                    PanelLayoutMapper.ApplyVisualState(canvas, tab, visualStateChanged, tab.RuntimeState);
                }
            }
        }
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject root) where T : DependencyObject
    {
        var childCount = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < childCount; i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            if (child is T typed)
            {
                yield return typed;
            }

            foreach (var nested in FindVisualChildren<T>(child))
            {
                yield return nested;
            }
        }
    }

    private static void OnPanelViewportStateChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs _)
    {
        if (dependencyObject is not FrameworkElement element)
        {
            return;
        }

        if ((bool)element.GetValue(IsSyncingViewportStateProperty))
        {
            return;
        }

        ApplyViewportStateToCanvas(element);
    }

    private static void OnMouseDown(object sender, MouseButtonEventArgs eventArgs)
    {
        if (sender is not FrameworkElement element)
        {
            return;
        }

        CanvasPanZoomBehavior.HandleMouseDown(element, eventArgs);
        UpdateViewportStateFromCanvas(element);
    }

    private static void OnMouseMove(object sender, MouseEventArgs eventArgs)
    {
        if (sender is not FrameworkElement element)
        {
            return;
        }

        CanvasPanZoomBehavior.HandleMouseMove(element, eventArgs);
        UpdateViewportStateFromCanvas(element);
    }

    private static void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs eventArgs)
    {
        if (sender is not FrameworkElement canvas)
        {
            return;
        }

        canvas.Focus();
        var clickedElement = CanvasSelectionBehavior.FindSelectableElement(eventArgs.OriginalSource as DependencyObject, canvas);

        if (clickedElement is null
            && PanelToolPlacementController.TryHandlePlacement(
                canvas,
                eventArgs,
                GetIsRectangleToolActive(canvas),
                GetIsImageToolActive(canvas),
                ExecuteCanvasMutation))
        {
            eventArgs.Handled = true;
            return;
        }

        CanvasSelectionBehavior.SelectFromSource(canvas, eventArgs.OriginalSource as DependencyObject);
        NotifyActiveDocumentSelection(canvas, clickedElement);
        if (clickedElement is not null)
        {
            eventArgs.Handled = true;
        }
    }

    private static void OnMouseWheel(object sender, MouseWheelEventArgs eventArgs)
    {
        if (sender is not FrameworkElement element)
        {
            return;
        }

        CanvasPanZoomBehavior.HandleMouseWheel(element, eventArgs);
        UpdateViewportStateFromCanvas(element);
    }

    private static void OnMouseUp(object sender, MouseButtonEventArgs eventArgs)
    {
        if (sender is not FrameworkElement element)
        {
            return;
        }

        CanvasPanZoomBehavior.HandleMouseUp(element, eventArgs);
    }

    private static void OnLostMouseCapture(object sender, MouseEventArgs eventArgs)
    {
        if (sender is not FrameworkElement element)
        {
            return;
        }

        CanvasPanZoomBehavior.HandleLostMouseCapture(element);
    }

    private static void ApplyViewportStateToCanvas(FrameworkElement element)
    {
        var (scale, translate) = CanvasPanZoomBehavior.EnsureTransformGroup(element);
        scale.ScaleX = GetPanelZoom(element);
        scale.ScaleY = GetPanelZoom(element);
        translate.X = GetPanelPanX(element);
        translate.Y = GetPanelPanY(element);
    }

    private static void UpdateViewportStateFromCanvas(FrameworkElement element)
    {
        var (scale, translate) = CanvasPanZoomBehavior.EnsureTransformGroup(element);
        element.SetValue(IsSyncingViewportStateProperty, true);
        try
        {
            element.SetCurrentValue(PanelZoomProperty, scale.ScaleX);
            element.SetCurrentValue(PanelPanXProperty, translate.X);
            element.SetCurrentValue(PanelPanYProperty, translate.Y);
        }
        finally
        {
            element.SetValue(IsSyncingViewportStateProperty, false);
        }
    }

    private static void ExecuteCanvasMutation(FrameworkElement canvas, Commands.ICommand command)
    {
        if (canvas.DataContext is not DocumentTabViewModel tab)
        {
            return;
        }

        if (!CanvasCommandDispatcher.ExecuteMutation(canvas, tab, command))
        {
            return;
        }

        if (canvas is Canvas panelCanvas)
        {
            PanelLayoutMapper.SyncPanelLayout(panelCanvas, PanelLayoutJsonProperty);
        }
    }

    private static void OnPanelLayoutJsonChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
    {
        if (dependencyObject is not Canvas canvas)
        {
            return;
        }

        if (PanelLayoutMapper.IsApplyingLayout(canvas))
        {
            return;
        }

        var runtimeState = canvas.DataContext is DocumentTabViewModel tab
            ? tab.RuntimeState
            : new PanelRuntimeState();
        PanelLayoutMapper.ApplyPersistedLayout(canvas, eventArgs.NewValue as string, runtimeState, GetIsSkiaRuntimeRenderingEnabled(canvas));
    }

    private static void OnSelectedPanelSelectionChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
    {
        if (dependencyObject is not Canvas canvas)
        {
            return;
        }

        if (eventArgs.NewValue is not PanelSelectionInfo selection)
        {
            CanvasSelectionBehavior.ClearSelection(canvas);
            NotifyActiveDocumentSelection(canvas, null);
            return;
        }

        var matchedElement = canvas.Children
            .OfType<FrameworkElement>()
            .FirstOrDefault(element => IsSelectionMatch(element, selection));
        CanvasSelectionBehavior.SelectElement(canvas, matchedElement);
        NotifyActiveDocumentSelection(canvas, matchedElement);
    }

    private static bool IsSelectionMatch(FrameworkElement element, PanelSelectionInfo selection)
    {
        if (!PanelSelectionContract.TryCreateFromVisual(element, out var selectable))
        {
            return false;
        }

        return PanelSelectionContract.IsMatch(selectable, selection);
    }

    private static void NotifyActiveDocumentSelection(FrameworkElement canvas, FrameworkElement? selectedElement)
    {
        if (canvas.DataContext is not DocumentTabViewModel tab)
        {
            return;
        }

        if (selectedElement is null)
        {
            CanvasCommandDispatcher.NotifyDocumentSelection(canvas, tab, null);
            return;
        }

        if (!PanelSelectionContract.TryCreateFromVisual(selectedElement, out var selectable))
        {
            var fallbackSelection = new PanelSelectionInfo(
                selectedElement.Uid?.Trim() ?? string.Empty,
                selectedElement.GetType().Name,
                Canvas.GetLeft(selectedElement),
                Canvas.GetTop(selectedElement),
                selectedElement.Width,
                selectedElement.Height);
            CanvasCommandDispatcher.NotifyDocumentSelection(canvas, tab, fallbackSelection);
            return;
        }

        CanvasCommandDispatcher.NotifyDocumentSelection(canvas, tab, PanelSelectionContract.ToSelectionInfo(selectable));
    }
}
