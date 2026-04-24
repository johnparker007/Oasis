using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
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

    public static readonly DependencyProperty IsImageToolActiveProperty =
        DependencyProperty.RegisterAttached(
            "IsImageToolActive",
            typeof(bool),
            typeof(CanvasPanBehavior),
            new PropertyMetadata(false));

    public static readonly DependencyProperty PanelLayoutJsonProperty =
        DependencyProperty.RegisterAttached(
            "PanelLayoutJson",
            typeof(string),
            typeof(CanvasPanBehavior),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnPanelLayoutJsonChanged));

    private static readonly DependencyProperty SelectedElementProperty =
        DependencyProperty.RegisterAttached(
            "SelectedElement",
            typeof(FrameworkElement),
            typeof(CanvasPanBehavior),
            new PropertyMetadata(null));

    private static readonly DependencyProperty IsPersistedElementProperty =
        DependencyProperty.RegisterAttached(
            "IsPersistedElement",
            typeof(bool),
            typeof(CanvasPanBehavior),
            new PropertyMetadata(false));

    private static readonly DependencyProperty IsApplyingLayoutProperty =
        DependencyProperty.RegisterAttached(
            "IsApplyingLayout",
            typeof(bool),
            typeof(CanvasPanBehavior),
            new PropertyMetadata(false));

    private const double MinZoom = 0.25;
    private const double MaxZoom = 4.0;
    private const double ZoomStep = 1.1;
    private const double NewRectangleWidth = 180;
    private const double NewRectangleHeight = 120;
    private const double NewImageWidth = 220;
    private const double NewImageHeight = 140;

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

    public static bool GetIsImageToolActive(DependencyObject dependencyObject)
    {
        return (bool)dependencyObject.GetValue(IsImageToolActiveProperty);
    }

    public static void SetIsImageToolActive(DependencyObject dependencyObject, bool value)
    {
        dependencyObject.SetValue(IsImageToolActiveProperty, value);
    }

    public static string? GetPanelLayoutJson(DependencyObject dependencyObject)
    {
        return (string?)dependencyObject.GetValue(PanelLayoutJsonProperty);
    }

    public static void SetPanelLayoutJson(DependencyObject dependencyObject, string? value)
    {
        dependencyObject.SetValue(PanelLayoutJsonProperty, value);
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

        element.Focus();
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

        canvas.Focus();
        var clickedElement = FindSelectableElement(eventArgs.OriginalSource as DependencyObject, canvas);

        if (GetIsRectangleToolActive(canvas) && clickedElement is null)
        {
            AddRectangle(canvas, eventArgs);
            eventArgs.Handled = true;
            return;
        }

        if (GetIsImageToolActive(canvas) && clickedElement is null)
        {
            AddImage(canvas, eventArgs);
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
        rectangle.SetValue(IsPersistedElementProperty, true);

        var x = Math.Max(0, canvasPoint.X - (NewRectangleWidth / 2));
        var y = Math.Max(0, canvasPoint.Y - (NewRectangleHeight / 2));
        System.Windows.Controls.Canvas.SetLeft(rectangle, x);
        System.Windows.Controls.Canvas.SetTop(rectangle, y);
        var previousSelection = (FrameworkElement?)panelCanvas.GetValue(SelectedElementProperty);
        if (panelCanvas.DataContext is not DocumentTabViewModel tab)
        {
            return;
        }

        ExecuteCanvasMutation(panelCanvas, new AddRectangleMutationCommand(tab.DocumentId, panelCanvas, rectangle, previousSelection, x, y));
    }

    private static void AddImage(FrameworkElement canvas, MouseButtonEventArgs eventArgs)
    {
        if (canvas is not Canvas panelCanvas)
        {
            return;
        }

        var clickPosition = eventArgs.GetPosition(panelCanvas.Parent as IInputElement ?? panelCanvas);
        var (scale, translate) = EnsureTransformGroup(panelCanvas);
        var canvasPoint = new Point(
            (clickPosition.X - translate.X) / scale.ScaleX,
            (clickPosition.Y - translate.Y) / scale.ScaleY);

        var image = new Image
        {
            Width = NewImageWidth,
            Height = NewImageHeight,
            Stretch = Stretch.Fill,
            Source = CreatePlaceholderImageSource()
        };
        SetIsSelectable(image, true);
        SetIsSelected(image, true);
        image.SetValue(IsPersistedElementProperty, true);

        var x = Math.Max(0, canvasPoint.X - (NewImageWidth / 2));
        var y = Math.Max(0, canvasPoint.Y - (NewImageHeight / 2));
        Canvas.SetLeft(image, x);
        Canvas.SetTop(image, y);

        var previousSelection = (FrameworkElement?)panelCanvas.GetValue(SelectedElementProperty);
        if (panelCanvas.DataContext is not DocumentTabViewModel tab)
        {
            return;
        }

        ExecuteCanvasMutation(panelCanvas, new AddImageMutationCommand(tab.DocumentId, panelCanvas, image, previousSelection, x, y));
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

    private static void ExecuteCanvasMutation(FrameworkElement canvas, Commands.ICommand command)
    {
        if (canvas.DataContext is not DocumentTabViewModel tab)
        {
            return;
        }

        tab.CommandService.Execute(command);
        if (canvas is Canvas panelCanvas)
        {
            SyncPanelLayout(panelCanvas);
        }
    }

    private static ImageSource CreatePlaceholderImageSource()
    {
        const int pixelWidth = 220;
        const int pixelHeight = 140;
        const int bytesPerPixel = 4;
        var pixels = new byte[pixelWidth * pixelHeight * bytesPerPixel];

        for (var y = 0; y < pixelHeight; y++)
        {
            for (var x = 0; x < pixelWidth; x++)
            {
                var index = ((y * pixelWidth) + x) * bytesPerPixel;
                var isGridLine = x % 22 == 0 || y % 20 == 0;
                byte red = 58;
                byte green = 80;
                byte blue = 118;

                if (isGridLine)
                {
                    red = 92;
                    green = 118;
                    blue = 163;
                }

                pixels[index] = blue;
                pixels[index + 1] = green;
                pixels[index + 2] = red;
                pixels[index + 3] = 255;
            }
        }

        var bitmap = BitmapSource.Create(
            pixelWidth,
            pixelHeight,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            pixels,
            pixelWidth * bytesPerPixel);
        bitmap.Freeze();
        return bitmap;
    }

    private static void OnPanelLayoutJsonChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
    {
        if (dependencyObject is not Canvas canvas)
        {
            return;
        }

        if ((bool)canvas.GetValue(IsApplyingLayoutProperty))
        {
            return;
        }

        ApplyPersistedLayout(canvas, eventArgs.NewValue as string);
    }

    private static void ApplyPersistedLayout(Canvas canvas, string? layoutJson)
    {
        canvas.SetValue(IsApplyingLayoutProperty, true);
        try
        {
            var persistedChildren = canvas.Children
                .OfType<FrameworkElement>()
                .Where(child => (bool)child.GetValue(IsPersistedElementProperty))
                .ToList();

            foreach (var child in persistedChildren)
            {
                canvas.Children.Remove(child);
            }

            var elements = Panel2DDocumentStorage.DeserializeLayout(layoutJson);
            foreach (var element in elements)
            {
                var visual = CreateVisualFromElement(element);
                if (visual is null)
                {
                    continue;
                }

                visual.SetValue(IsPersistedElementProperty, true);
                SetIsSelectable(visual, true);
                SetIsSelected(visual, false);
                Canvas.SetLeft(visual, Math.Max(0, element.X));
                Canvas.SetTop(visual, Math.Max(0, element.Y));
                canvas.Children.Add(visual);
            }

            canvas.ClearValue(SelectedElementProperty);
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

    private static FrameworkElement? CreateVisualFromElement(PanelElementFile element)
    {
        if (string.Equals(element.Kind, "rectangle", StringComparison.OrdinalIgnoreCase))
        {
            return new Rectangle
            {
                Width = element.Width <= 0 ? NewRectangleWidth : element.Width,
                Height = element.Height <= 0 ? NewRectangleHeight : element.Height
            };
        }

        if (string.Equals(element.Kind, "image", StringComparison.OrdinalIgnoreCase))
        {
            return new Image
            {
                Width = element.Width <= 0 ? NewImageWidth : element.Width,
                Height = element.Height <= 0 ? NewImageHeight : element.Height,
                Stretch = Stretch.Fill,
                Source = CreatePlaceholderImageSource()
            };
        }

        return null;
    }

    private static void SyncPanelLayout(Canvas canvas)
    {
        if ((bool)canvas.GetValue(IsApplyingLayoutProperty))
        {
            return;
        }

        var elements = canvas.Children
            .OfType<FrameworkElement>()
            .Where(child => (bool)child.GetValue(IsPersistedElementProperty))
            .Select(CreateElementFromVisual)
            .Where(element => element is not null)
            .Cast<PanelElementFile>()
            .ToArray();

        var layoutJson = Panel2DDocumentStorage.SerializeLayout(elements);
        canvas.SetValue(IsApplyingLayoutProperty, true);
        try
        {
            canvas.SetCurrentValue(PanelLayoutJsonProperty, layoutJson);
            if (canvas.DataContext is DocumentTabViewModel tab)
            {
                tab.PanelLayoutJson = layoutJson;
            }
        }
        finally
        {
            canvas.SetValue(IsApplyingLayoutProperty, false);
        }
    }

    private static PanelElementFile? CreateElementFromVisual(FrameworkElement child)
    {
        var kind = child switch
        {
            Rectangle => "rectangle",
            Image => "image",
            _ => null
        };

        if (kind is null)
        {
            return null;
        }

        return new PanelElementFile
        {
            Kind = kind,
            X = Canvas.GetLeft(child),
            Y = Canvas.GetTop(child),
            Width = child.Width,
            Height = child.Height
        };
    }

    private sealed class AddRectangleMutationCommand : Commands.IDocumentCommand
    {
        private readonly Guid _documentId;
        private readonly Canvas _canvas;
        private readonly Rectangle _rectangle;
        private readonly FrameworkElement? _previousSelection;
        private readonly double _x;
        private readonly double _y;

        public AddRectangleMutationCommand(Guid documentId, Canvas canvas, Rectangle rectangle, FrameworkElement? previousSelection, double x, double y)
        {
            _documentId = documentId;
            _canvas = canvas;
            _rectangle = rectangle;
            _previousSelection = previousSelection;
            _x = x;
            _y = y;
        }

        public Guid DocumentId => _documentId;

        public string Description => "Add rectangle";

        public void Execute()
        {
            if (!_canvas.Children.Contains(_rectangle))
            {
                Canvas.SetLeft(_rectangle, _x);
                Canvas.SetTop(_rectangle, _y);
                _canvas.Children.Add(_rectangle);
            }

            if (_previousSelection is not null)
            {
                SetIsSelected(_previousSelection, false);
            }

            SetIsSelected(_rectangle, true);
            _canvas.SetValue(SelectedElementProperty, _rectangle);
            SyncPanelLayout(_canvas);
        }

        public void Undo()
        {
            _canvas.Children.Remove(_rectangle);
            SetIsSelected(_rectangle, false);

            if (_previousSelection is not null && _canvas.Children.Contains(_previousSelection))
            {
                SetIsSelected(_previousSelection, true);
                _canvas.SetValue(SelectedElementProperty, _previousSelection);
                return;
            }

            _canvas.ClearValue(SelectedElementProperty);
            SyncPanelLayout(_canvas);
        }
    }

    private sealed class AddImageMutationCommand : Commands.IDocumentCommand
    {
        private readonly Guid _documentId;
        private readonly Canvas _canvas;
        private readonly Image _image;
        private readonly FrameworkElement? _previousSelection;
        private readonly double _x;
        private readonly double _y;

        public AddImageMutationCommand(Guid documentId, Canvas canvas, Image image, FrameworkElement? previousSelection, double x, double y)
        {
            _documentId = documentId;
            _canvas = canvas;
            _image = image;
            _previousSelection = previousSelection;
            _x = x;
            _y = y;
        }

        public Guid DocumentId => _documentId;

        public string Description => "Add image";

        public void Execute()
        {
            if (!_canvas.Children.Contains(_image))
            {
                Canvas.SetLeft(_image, _x);
                Canvas.SetTop(_image, _y);
                _canvas.Children.Add(_image);
            }

            if (_previousSelection is not null)
            {
                SetIsSelected(_previousSelection, false);
            }

            SetIsSelected(_image, true);
            _canvas.SetValue(SelectedElementProperty, _image);
            SyncPanelLayout(_canvas);
        }

        public void Undo()
        {
            _canvas.Children.Remove(_image);
            SetIsSelected(_image, false);

            if (_previousSelection is not null && _canvas.Children.Contains(_previousSelection))
            {
                SetIsSelected(_previousSelection, true);
                _canvas.SetValue(SelectedElementProperty, _previousSelection);
                return;
            }

            _canvas.ClearValue(SelectedElementProperty);
            SyncPanelLayout(_canvas);
        }
    }
}
