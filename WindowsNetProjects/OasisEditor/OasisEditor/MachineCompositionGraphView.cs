using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace OasisEditor.Avalonia;

public sealed class MachineCompositionGraphView : UserControl
{
    private readonly Canvas _canvas = new() { MinWidth = 1100, MinHeight = 740, RenderTransformOrigin=new RelativePoint(0,0,RelativeUnit.Relative) };
    private readonly StackPanel _diagnostics = new() { Spacing = 4, Margin = new Thickness(8) };
    private readonly ScrollViewer _scroller;
    private readonly TextBlock _zoomLabel = new() { VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center };
    private double _zoom = 1, _offsetX, _offsetY;
    private Point _panStart;
    private double _panStartX, _panStartY;
    private bool _panning;

    public event Action<MachineOverviewNode>? NodeActivated;

    public MachineCompositionGraphView()
    {
        var fit = new Button { Content = "Fit", Padding = new Thickness(10, 3) };
        fit.Click += (_, _) => FitToContent();
        var toolbar = new StackPanel { Orientation = global::Avalonia.Layout.Orientation.Horizontal, Spacing = 8, Margin = new Thickness(8) };
        toolbar.Children.Add(fit); toolbar.Children.Add(_zoomLabel);
        var content = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto") };
        content.Children.Add(toolbar);
        _scroller = new ScrollViewer { Content = _canvas, Background = Brushes.Transparent, HorizontalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Auto };
        Grid.SetRow(_scroller, 1); content.Children.Add(_scroller);
        var diagnosticScroll = new ScrollViewer { Content = _diagnostics, MaxHeight = 135, HorizontalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        Grid.SetRow(diagnosticScroll, 2); content.Children.Add(diagnosticScroll);
        Content = content;
        Background = Brush("WorkspaceBrush");
        // The canvas is transformed and has empty, non-hit-testable regions.
        // Handle navigation on the stationary viewport, before normal scrolling.
        _scroller.AddHandler(PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel);
        PointerMoved += OnPointerMoved;
        PointerCaptureLost += (_,_)=>_panning=false;
        PointerReleased += OnPointerReleased;
        _scroller.AddHandler(PointerWheelChangedEvent, OnPointerWheelChanged, RoutingStrategies.Tunnel);
        _scroller.SizeChanged += (_, _) => FitToContent();
        ActualThemeVariantChanged += (_, _) => { Background = Brush("WorkspaceBrush"); RedrawCurrent(); };
    }

    private MachineOverview? _graph;
    public void SetGraph(MachineOverview graph)
    {
        _graph = graph ?? throw new ArgumentNullException(nameof(graph));
        RedrawCurrent();
    }

    private void RedrawCurrent()
    {
        if (_graph is null) return;
        _canvas.Children.Clear();
        _diagnostics.Children.Clear();
        var lookup = _graph.Nodes.ToDictionary(node => node.Id, StringComparer.OrdinalIgnoreCase);
        foreach (var edge in _graph.Edges)
        {
            if (!lookup.TryGetValue(edge.FromId, out var from) || !lookup.TryGetValue(edge.ToId, out var to)) continue;
            var x1 = from.X + from.Width; var y1 = from.Y + from.Height / 2;
            var x2 = to.X; var y2 = to.Y + to.Height / 2;
            var bend = x1 + (x2 - x1) / 2;
            var line = new Polyline { Points = new global::Avalonia.Collections.AvaloniaList<Point>
                { new(x1, y1), new(bend, y1), new(bend, y2), new(x2, y2) }, Stroke = Brush("MutedBrush"), StrokeThickness = 1.7 };
            if (edge.Provenance) line.StrokeDashArray = new global::Avalonia.Collections.AvaloniaList<double> { 5, 4 };
            _canvas.Children.Add(line);
            if (!string.IsNullOrWhiteSpace(edge.Label))
            {
                var label = new Border { Background = Brush("WorkspaceBrush"), Padding = new Thickness(3, 1),
                    Child = new TextBlock { Text = edge.Label, Foreground = Brush("MutedBrush"), FontSize = 11 } };
                Canvas.SetLeft(label, (x1 + x2) / 2 - 28); Canvas.SetTop(label, (y1 + y2) / 2 - 12); _canvas.Children.Add(label);
            }
        }
        foreach (var node in _graph.Nodes)
        {
            var card = CreateNodeCard(node);
            Canvas.SetLeft(card, node.X); Canvas.SetTop(card, node.Y); _canvas.Children.Add(card);
        }
        foreach (var diagnostic in _graph.Diagnostics)
            _diagnostics.Children.Add(new TextBlock { Text = "• " + diagnostic, TextWrapping = TextWrapping.Wrap,
                Foreground = Brush("TextBrush"), Margin = new Thickness(4, 0) });
        if (_graph.Diagnostics.Length == 0)
            _diagnostics.Children.Add(new TextBlock { Text = "No composition diagnostics.", Foreground = Brush("MutedBrush") });
        _canvas.Width = Math.Max(1100, _graph.Nodes.Select(node => node.X + node.Width + 40).DefaultIfEmpty(1100).Max());
        _canvas.Height = Math.Max(740, _graph.Nodes.Select(node => node.Y + node.Height + 40).DefaultIfEmpty(740).Max());
        FitToContent();
    }

    private Control CreateNodeCard(MachineOverviewNode node)
    {
        var stack = new StackPanel { Spacing = 4 };
        stack.Children.Add(new TextBlock { Text = node.Kind.ToUpperInvariant(), FontSize = 10, FontWeight = FontWeight.SemiBold,
            Foreground = Brush("MutedBrush") });
        stack.Children.Add(new TextBlock { Text = node.Title, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap,
            Foreground = Brush("TextBrush") });
        stack.Children.Add(new TextBlock { Text = node.Metadata, FontSize = 11, TextWrapping = TextWrapping.Wrap,
            Foreground = Brush("MutedBrush") });
        var border = new Border { Width = node.Width, Height = node.Height, ClipToBounds=true, Padding = new Thickness(10), CornerRadius = new CornerRadius(5),
            Background = Brush("PanelBrush"), BorderBrush = Brush(node.Missing ? "SelectionColor" : "DividerBrush"), BorderThickness = new Thickness(1.5), Child = stack };
        ToolTip.SetTip(border, node.ManifestPath ?? node.Metadata);
        border.PointerPressed += (_, e) => { if (e.GetCurrentPoint(border).Properties.IsLeftButtonPressed) NodeActivated?.Invoke(node); };
        return border;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(_canvas).Properties.IsMiddleButtonPressed) return;
        _panning = true; _panStart = e.GetPosition(this); _panStartX = _offsetX; _panStartY = _offsetY; e.Pointer.Capture(this); e.Handled = true;
    }
    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_panning) return;
        var p = e.GetPosition(this); _offsetX = _panStartX + p.X - _panStart.X; _offsetY = _panStartY + p.Y - _panStart.Y; ApplyTransform();
    }
    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    { if (_panning) { _panning = false; e.Pointer.Capture(null); e.Handled = true; } }
    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        var oldZoom = _zoom;
        _zoom = Math.Clamp(_zoom * (e.Delta.Y > 0 ? 1.1 : 1 / 1.1), .1, 2.5);
        var point = e.GetPosition(this); _offsetX = point.X - (point.X - _offsetX) * (_zoom / oldZoom);
        _offsetY = point.Y - (point.Y - _offsetY) * (_zoom / oldZoom);
        ApplyTransform(); e.Handled = true;
    }
    private void ApplyTransform()
    {
        _canvas.RenderTransform = new MatrixTransform(new Matrix(_zoom, 0, 0, _zoom, _offsetX, _offsetY));
        _zoomLabel.Text = $"{_zoom:P0} · middle-drag to pan";
    }
    private void FitToContent()
    {
        if (_graph is null || _graph.Nodes.Length==0) return;
        var width = Math.Max(1, _scroller.Bounds.Width - 36);
        var height = Math.Max(1, _scroller.Bounds.Height - 36);
        var left=_graph.Nodes.Min(n=>n.X);var top=_graph.Nodes.Min(n=>n.Y);
        var contentWidth=_graph.Nodes.Max(n=>n.X+n.Width)-left;
        var contentHeight=_graph.Nodes.Max(n=>n.Y+n.Height)-top;
        _zoom = Math.Clamp(Math.Min(width / Math.Max(1, contentWidth), height / Math.Max(1, contentHeight)), .1, 1.25);
        _offsetX=18+(width-contentWidth*_zoom)/2-left*_zoom;
        _offsetY=18+(height-contentHeight*_zoom)/2-top*_zoom;
        ApplyTransform();
    }
    private IBrush Brush(string key)
    {
        if (Application.Current is { } application && application.TryGetResource(key, ActualThemeVariant, out var value))
            return value switch { IBrush brush => brush, Color color => new SolidColorBrush(color), _ => Brushes.Transparent };
        return Brushes.Transparent;
    }
}
