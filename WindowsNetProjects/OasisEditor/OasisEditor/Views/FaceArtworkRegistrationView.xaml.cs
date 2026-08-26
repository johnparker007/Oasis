using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace OasisEditor.Views;

public partial class FaceArtworkRegistrationView : UserControl
{
    private const double Radius = 8;
    private int _drag = -1;
    private bool _isPanning;
    private Point _panStart;
    private Vector _panOrigin;
    private ArtworkRegistrationViewportTransform _viewport = ArtworkRegistrationViewportTransform.Fit;
    private FacePerspectiveRegistrationModel? _preview;
    private FaceWorkspaceViewModel? _subscribedWorkspace;
    private string? _viewportSourcePath;
    private int _viewportSourceWidth;
    private int _viewportSourceHeight;
    private bool _viewportSourceIsOverride;
    private object? _viewportSourceImage;

    public FaceArtworkRegistrationView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        DataContextChanged += OnDataContextChanged;
    }

    private FaceWorkspaceViewModel? Workspace => (DataContext as DocumentTabViewModel)?.FaceWorkspace;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        SubscribeToWorkspace();
        Draw();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        CancelInteraction();
        UnsubscribeFromWorkspace();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        UnsubscribeFromWorkspace();
        CancelInteraction();
        ResetView();
        if (IsLoaded) SubscribeToWorkspace();
        Draw();
    }

    private void SubscribeToWorkspace()
    {
        var workspace = Workspace;
        if (ReferenceEquals(workspace, _subscribedWorkspace)) return;
        UnsubscribeFromWorkspace();
        _subscribedWorkspace = workspace;
        if (_subscribedWorkspace is not null)
        {
            _subscribedWorkspace.PropertyChanged += OnWorkspacePropertyChanged;
            RememberViewportSource();
        }
    }

    private void UnsubscribeFromWorkspace()
    {
        if (_subscribedWorkspace is not null) _subscribedWorkspace.PropertyChanged -= OnWorkspacePropertyChanged;
        _subscribedWorkspace = null;
    }

    private void OnWorkspacePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(FaceWorkspaceViewModel.GeometryRegistration)
            or nameof(FaceWorkspaceViewModel.GeometryRawImagePath)
            or nameof(FaceWorkspaceViewModel.GeometryRawImage)
            or nameof(FaceWorkspaceViewModel.GeometrySourcePixelWidth)
            or nameof(FaceWorkspaceViewModel.GeometrySourcePixelHeight)
            or nameof(FaceWorkspaceViewModel.IsEditingOverrideGeometry))) return;

        // An external authored change makes an in-progress preview obsolete. Never commit against stale geometry.
        CancelInteraction();
        if (ViewportSourceChanged()) ResetView();
        RememberViewportSource();
        Draw();
    }

    private bool ViewportSourceChanged()
    {
        var workspace = Workspace;
        return workspace is null
            || !string.Equals(_viewportSourcePath, workspace.GeometryRawImagePath, StringComparison.Ordinal)
            || _viewportSourceWidth != workspace.GeometrySourcePixelWidth
            || _viewportSourceHeight != workspace.GeometrySourcePixelHeight
            || _viewportSourceIsOverride != workspace.IsEditingOverrideGeometry
            || !ReferenceEquals(_viewportSourceImage, workspace.GeometryRawImage);
    }

    private void RememberViewportSource()
    {
        var workspace = Workspace;
        _viewportSourcePath = workspace?.GeometryRawImagePath;
        _viewportSourceWidth = workspace?.GeometrySourcePixelWidth ?? 0;
        _viewportSourceHeight = workspace?.GeometrySourcePixelHeight ?? 0;
        _viewportSourceIsOverride = workspace?.IsEditingOverrideGeometry ?? false;
        _viewportSourceImage = workspace?.GeometryRawImage;
    }

    private void CancelInteraction()
    {
        _drag = -1;
        _preview = null;
        _isPanning = false;
        Overlay.Cursor = null;
        if (Overlay.IsMouseCaptured) Overlay.ReleaseMouseCapture();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e) => Draw();

    private Rect BaseImageRect()
    {
        var w = Math.Max(1, Workspace?.GeometrySourcePixelWidth ?? 1);
        var h = Math.Max(1, Workspace?.GeometrySourcePixelHeight ?? 1);
        var availableW = Math.Max(1, Overlay.ActualWidth - 48);
        var availableH = Math.Max(1, Overlay.ActualHeight - 48);
        var scale = Math.Min(availableW / w, availableH / h);
        var width = w * scale;
        var height = h * scale;
        return new Rect((Overlay.ActualWidth - width) / 2, (Overlay.ActualHeight - height) / 2, width, height);
    }

    private Point ToCanvas(NormalizedFacePointModel point)
    {
        return _viewport.NormalizedToScreen(new Point(point.X, point.Y), BaseImageRect());
    }

    private NormalizedFacePointModel ToNormalized(Point point)
    {
        var normalized = _viewport.ScreenToNormalized(point, BaseImageRect());
        return new NormalizedFacePointModel
        {
            X = Math.Clamp(normalized.X, 0, 1),
            Y = Math.Clamp(normalized.Y, 0, 1)
        };
    }

    private void Draw()
    {
        Overlay.Children.Clear();
        var imageRect = _viewport.ImageRect(BaseImageRect());
        SourceImage.Width = imageRect.Width;
        SourceImage.Height = imageRect.Height;
        Canvas.SetLeft(SourceImage, imageRect.X);
        Canvas.SetTop(SourceImage, imageRect.Y);
        var registration = _preview ?? Workspace?.GeometryRegistration;
        if (registration is null) return;
        var points = new[] { registration.TopLeft, registration.TopRight, registration.BottomRight, registration.BottomLeft };
        var line = new Polyline
        {
            Stroke = (Brush)FindResource("SelectionBrush"),
            StrokeThickness = 2,
            Points = new PointCollection(points.Select(ToCanvas).Append(ToCanvas(points[0])))
        };
        Overlay.Children.Add(line);
        foreach (var point in points)
        {
            var position = ToCanvas(point);
            var handle = new Ellipse
            {
                Width = Radius * 2, Height = Radius * 2,
                Fill = (Brush)FindResource("SelectionBrush"),
                Stroke = (Brush)FindResource("TextPrimaryBrush"), StrokeThickness = 2
            };
            Canvas.SetLeft(handle, position.X - Radius);
            Canvas.SetTop(handle, position.Y - Radius);
            Overlay.Children.Add(handle);
        }
    }

    private void OnDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Middle)
        {
            if (_drag >= 0) return;
            _isPanning = true;
            _panStart = e.GetPosition(Overlay);
            _panOrigin = new Vector(_viewport.PanX, _viewport.PanY);
            Overlay.Cursor = Cursors.SizeAll;
            Overlay.CaptureMouse();
            e.Handled = true;
            return;
        }

        if (e.ChangedButton != MouseButton.Left || _isPanning) return;
        var registration = Workspace?.GeometryRegistration;
        if (registration is null) return;
        var at = e.GetPosition(Overlay);
        var points = new[] { registration.TopLeft, registration.TopRight, registration.BottomRight, registration.BottomLeft };
        _drag = Enumerable.Range(0, 4).OrderBy(index => (ToCanvas(points[index]) - at).Length).First();
        if ((ToCanvas(points[_drag]) - at).Length > 20) { _drag = -1; return; }
        _preview = registration;
        Overlay.CaptureMouse();
        e.Handled = true;
    }

    private void OnMove(object sender, MouseEventArgs e)
    {
        if (_isPanning)
        {
            if (e.MiddleButton != MouseButtonState.Pressed) { CancelInteraction(); return; }
            var delta = e.GetPosition(Overlay) - _panStart;
            _viewport = _viewport with { PanX = _panOrigin.X + delta.X, PanY = _panOrigin.Y + delta.Y };
            Draw();
            return;
        }

        if (_drag < 0 || _preview is null || e.LeftButton != MouseButtonState.Pressed) return;
        var point = ToNormalized(e.GetPosition(Overlay));
        _preview = new FacePerspectiveRegistrationModel
        {
            TopLeft = _drag == 0 ? point : _preview.TopLeft,
            TopRight = _drag == 1 ? point : _preview.TopRight,
            BottomRight = _drag == 2 ? point : _preview.BottomRight,
            BottomLeft = _drag == 3 ? point : _preview.BottomLeft
        };
        Draw();
    }

    private void OnUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Middle && _isPanning)
        {
            CancelInteraction();
            e.Handled = true;
            return;
        }
        if (e.ChangedButton != MouseButton.Left || _drag < 0) return;
        var candidate = _preview?.Normalize();
        CancelInteraction();
        if (candidate?.IsValid() == true) Workspace?.CommitGeometryRegistration(candidate);
        e.Handled = true;
    }

    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var next = _viewport.WithZoomAt(e.GetPosition(Overlay), e.Delta, BaseImageRect());
        if (next == _viewport) return;
        _viewport = next;
        Draw();
        e.Handled = true;
    }

    private void OnLostMouseCapture(object sender, MouseEventArgs e)
    {
        _drag = -1;
        _preview = null;
        _isPanning = false;
        Overlay.Cursor = null;
        Draw();
    }

    private void ResetView()
    {
        _viewport = ArtworkRegistrationViewportTransform.Fit;
    }

    private void OnFit(object sender, RoutedEventArgs e)
    {
        CancelInteraction();
        ResetView();
        Draw();
    }

    private void OnReset(object sender, RoutedEventArgs e) => Workspace?.ResetGeometryRegistration();
}
