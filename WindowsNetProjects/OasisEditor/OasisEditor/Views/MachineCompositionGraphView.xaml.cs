using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace OasisEditor.Views;

public partial class MachineCompositionGraphView : UserControl
{
    private readonly ScaleTransform _scale = new(1, 1);
    private readonly TranslateTransform _translate = new();
    private MachineCompositionGraphViewModel? _viewModel;
    private Point _panStart;
    private double _panStartX, _panStartY;
    private bool _panning, _initialFit = true;

    public MachineCompositionGraphView()
    {
        InitializeComponent();
        GraphCanvas.RenderTransform = new TransformGroup { Children = new TransformCollection { _scale, _translate } };
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel is not null) _viewModel.PropertyChanged -= OnGraphChanged;
        _viewModel = (e.NewValue as DocumentTabViewModel)?.MachineCompositionGraph;
        if (_viewModel is not null) _viewModel.PropertyChanged += OnGraphChanged;
        _initialFit = true;
        Draw();
    }

    private void OnGraphChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MachineCompositionGraphViewModel.Graph) or nameof(MachineCompositionGraphViewModel.SelectedNodeId))
            Dispatcher.Invoke(Draw);
    }

    private void Draw()
    {
        GraphCanvas.Children.Clear();
        var graph = _viewModel?.Graph;
        if (graph is null) return;
        foreach (var route in graph.Routes)
        {
            var edge = route.Edge;
            var line = new Polyline { StrokeThickness=2, Points=new PointCollection(route.Points.Select(x => new Point(x.X,x.Y))),
                Stroke = BrushResource(edge.Kind == MachineCompositionEdgeKind.Provenance ? "TextSecondaryBrush" : "BorderStrongBrush") };
            if (edge.Kind == MachineCompositionEdgeKind.Provenance) line.StrokeDashArray = new DoubleCollection { 5, 4 };
            GraphCanvas.Children.Add(line);
            if (string.IsNullOrWhiteSpace(edge.Label)) continue;
            var label = new Border { Background=BrushResource("WorkspaceBackgroundBrush"), Padding=new Thickness(4,1,4,1),
                Child=new TextBlock { Text=edge.Label, FontSize=11, Foreground=BrushResource("TextSecondaryBrush") } };
            Canvas.SetLeft(label, route.LabelPosition.X); Canvas.SetTop(label, route.LabelPosition.Y); GraphCanvas.Children.Add(label);
        }
        foreach (var node in graph.Nodes) GraphCanvas.Children.Add(CreateCard(node));
        GraphCanvas.Width = Math.Max(1, graph.Nodes.Select(x => x.X + x.Width + 30).DefaultIfEmpty(1).Max());
        GraphCanvas.Height = Math.Max(1, graph.Nodes.Select(x => x.Y + x.Height + 35).DefaultIfEmpty(1).Max());
        if (_initialFit && Viewport.ActualWidth > 0) { _initialFit = false; Fit(); }
    }

    private UIElement CreateCard(MachineCompositionNode node)
    {
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock { Text=node.Kind.ToString().ToUpperInvariant(), FontSize=10, FontWeight=FontWeights.SemiBold,
            Foreground=BrushResource(node.IsMissing ? "WarningTextBrush" : "TextSecondaryBrush") });
        panel.Children.Add(new TextBlock { Text=node.Title, FontSize=15, FontWeight=FontWeights.SemiBold, Margin=new Thickness(0,5,0,2), TextTrimming=TextTrimming.CharacterEllipsis });
        panel.Children.Add(new TextBlock { Text=node.Metadata, FontSize=11, Foreground=BrushResource("TextSecondaryBrush"), TextWrapping=TextWrapping.Wrap });
        var selected = _viewModel?.SelectedNodeId == node.Id;
        var card = new Border { Width=node.Width, Height=node.Height, Padding=new Thickness(11), CornerRadius=new CornerRadius(5),
            Background=BrushResource("PanelBackgroundBrush"), BorderThickness=new Thickness(selected ? 3 : 1),
            BorderBrush=BrushResource(node.IsMissing ? "WarningTextBrush" : selected ? "SelectionBrush" : "BorderStrongBrush"), Child=panel, Tag=node,
            ToolTip=node.IsMissing ? "This authored reference cannot currently be resolved." : node.ManifestPath is null ? null : "Double-click to open the authoritative asset." };
        card.MouseLeftButtonDown += Card_MouseLeftButtonDown;
        Canvas.SetLeft(card,node.X); Canvas.SetTop(card,node.Y);
        return card;
    }

    private Brush BrushResource(string key) => TryFindResource(key) as Brush ?? Brushes.Gray;
    private void Card_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border { Tag: MachineCompositionNode node } || _viewModel is null) return;
        _viewModel.SelectedNodeId = node.Id;
        if (e.ClickCount == 2) _viewModel.Open(node);
        e.Handled = true;
    }
    private void Viewport_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        var old = _scale.ScaleX; var next = Math.Clamp(old * (e.Delta > 0 ? 1.12 : 1/1.12), .25, 3.0);
        var point = e.GetPosition(Viewport); var contentX=(point.X-_translate.X)/old; var contentY=(point.Y-_translate.Y)/old;
        _scale.ScaleX=_scale.ScaleY=next; _translate.X=point.X-contentX*next; _translate.Y=point.Y-contentY*next; UpdateZoom(); e.Handled=true;
    }
    private void Viewport_MouseDown(object sender, MouseButtonEventArgs e)
    { if (e.ChangedButton != MouseButton.Middle && e.ChangedButton != MouseButton.Left) return; _panning=true; _panStart=e.GetPosition(Viewport); _panStartX=_translate.X; _panStartY=_translate.Y; Viewport.CaptureMouse(); }
    private void Viewport_MouseMove(object sender, MouseEventArgs e)
    { if (!_panning) return; var p=e.GetPosition(Viewport); _translate.X=_panStartX+p.X-_panStart.X; _translate.Y=_panStartY+p.Y-_panStart.Y; }
    private void Viewport_MouseUp(object sender, MouseButtonEventArgs e) { _panning=false; Viewport.ReleaseMouseCapture(); }
    private void Fit_Click(object sender, RoutedEventArgs e) => Fit();
    private void Reset_Click(object sender, RoutedEventArgs e) { _scale.ScaleX=_scale.ScaleY=1; _translate.X=_translate.Y=20; UpdateZoom(); }
    private void Viewport_SizeChanged(object sender, SizeChangedEventArgs e) { if (_initialFit && GraphCanvas.Width > 1) { _initialFit=false; Fit(); } }
    private void Fit()
    {
        if (GraphCanvas.Width <= 1 || Viewport.ActualWidth <= 1) return;
        const double margin=36; var zoom=Math.Clamp(Math.Min((Viewport.ActualWidth-margin*2)/GraphCanvas.Width,(Viewport.ActualHeight-margin*2)/GraphCanvas.Height),.25,1.5);
        _scale.ScaleX=_scale.ScaleY=zoom; _translate.X=(Viewport.ActualWidth-GraphCanvas.Width*zoom)/2; _translate.Y=(Viewport.ActualHeight-GraphCanvas.Height*zoom)/2; UpdateZoom();
    }
    private void UpdateZoom() => ZoomLabel.Text=$"{_scale.ScaleX*100:0}%";
}
