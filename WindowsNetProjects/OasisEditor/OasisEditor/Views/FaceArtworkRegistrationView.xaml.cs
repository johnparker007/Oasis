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
    private FacePerspectiveRegistrationModel? _preview;
    private FaceWorkspaceViewModel? _subscribedWorkspace;

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
        CancelDrag();
        UnsubscribeFromWorkspace();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        UnsubscribeFromWorkspace();
        CancelDrag();
        if (IsLoaded) SubscribeToWorkspace();
        Draw();
    }

    private void SubscribeToWorkspace()
    {
        var workspace = Workspace;
        if (ReferenceEquals(workspace, _subscribedWorkspace)) return;
        UnsubscribeFromWorkspace();
        _subscribedWorkspace = workspace;
        if (_subscribedWorkspace is not null) _subscribedWorkspace.PropertyChanged += OnWorkspacePropertyChanged;
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
            or nameof(FaceWorkspaceViewModel.GeometrySourcePixelHeight))) return;

        // An external authored change makes an in-progress preview obsolete. Never commit against stale geometry.
        CancelDrag();
        Draw();
    }

    private void CancelDrag()
    {
        _drag = -1;
        _preview = null;
        if (Overlay.IsMouseCaptured) Overlay.ReleaseMouseCapture();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e) => Draw();

    private Rect ImageRect()
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
        var rect = ImageRect();
        return new Point(rect.X + (point.X * rect.Width), rect.Y + (point.Y * rect.Height));
    }

    private NormalizedFacePointModel ToNormalized(Point point)
    {
        var rect = ImageRect();
        return new NormalizedFacePointModel
        {
            X = Math.Clamp((point.X - rect.X) / Math.Max(1, rect.Width), 0, 1),
            Y = Math.Clamp((point.Y - rect.Y) / Math.Max(1, rect.Height), 0, 1)
        };
    }

    private void Draw()
    {
        Overlay.Children.Clear();
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
        var registration = Workspace?.GeometryRegistration;
        if (registration is null) return;
        var at = e.GetPosition(Overlay);
        var points = new[] { registration.TopLeft, registration.TopRight, registration.BottomRight, registration.BottomLeft };
        _drag = Enumerable.Range(0, 4).OrderBy(index => (ToCanvas(points[index]) - at).Length).First();
        if ((ToCanvas(points[_drag]) - at).Length > 20) { _drag = -1; return; }
        _preview = registration;
        Overlay.CaptureMouse();
    }

    private void OnMove(object sender, MouseEventArgs e)
    {
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
        if (_drag < 0) return;
        var candidate = _preview?.Normalize();
        CancelDrag();
        if (candidate?.IsValid() == true) Workspace?.CommitGeometryRegistration(candidate);
    }

    private void OnReset(object sender, RoutedEventArgs e) => Workspace?.ResetGeometryRegistration();
}
