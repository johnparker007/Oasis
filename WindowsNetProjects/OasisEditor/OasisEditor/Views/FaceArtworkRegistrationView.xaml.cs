using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace OasisEditor.Views;

public partial class FaceArtworkRegistrationView : UserControl
{
    private const double Radius=8; private int _drag=-1; private FacePerspectiveRegistrationModel? _preview;
    public FaceArtworkRegistrationView() { InitializeComponent(); Loaded += (_,_) => Draw(); }
    private FaceWorkspaceViewModel? Workspace => (DataContext as DocumentTabViewModel)?.FaceWorkspace;
    private void OnSizeChanged(object sender, SizeChangedEventArgs e) => Draw();
    private Rect ImageRect()
    {
        var w=Math.Max(1,Workspace?.ArtworkSourcePixelWidth??1);var h=Math.Max(1,Workspace?.ArtworkSourcePixelHeight??1);
        var availableW=Math.Max(1,Overlay.ActualWidth-48);var availableH=Math.Max(1,Overlay.ActualHeight-48);
        var scale=Math.Min(availableW/w,availableH/h);var width=w*scale;var height=h*scale;
        return new Rect((Overlay.ActualWidth-width)/2,(Overlay.ActualHeight-height)/2,width,height);
    }
    private Point ToCanvas(NormalizedFacePointModel p) { var r=ImageRect();return new(r.X+p.X*r.Width,r.Y+p.Y*r.Height); }
    private NormalizedFacePointModel ToNormalized(Point p) { var r=ImageRect();return new() { X=Math.Clamp((p.X-r.X)/Math.Max(1,r.Width),0,1), Y=Math.Clamp((p.Y-r.Y)/Math.Max(1,r.Height),0,1) }; }
    private void Draw()
    {
        Overlay.Children.Clear(); var r=_preview??Workspace?.ArtworkRegistration; if(r is null)return;
        var points=new[]{r.TopLeft,r.TopRight,r.BottomRight,r.BottomLeft};
        var line=new Polyline { Stroke=(Brush)FindResource("SelectionBrush"), StrokeThickness=2, Points=new PointCollection(points.Select(ToCanvas).Append(ToCanvas(points[0]))) }; Overlay.Children.Add(line);
        foreach(var p in points){var q=ToCanvas(p);var handle=new Ellipse{Width=Radius*2,Height=Radius*2,Fill=(Brush)FindResource("SelectionBrush"),Stroke=(Brush)FindResource("TextPrimaryBrush"),StrokeThickness=2};Canvas.SetLeft(handle,q.X-Radius);Canvas.SetTop(handle,q.Y-Radius);Overlay.Children.Add(handle);}
    }
    private void OnDown(object sender, MouseButtonEventArgs e)
    {
        var r=Workspace?.ArtworkRegistration;if(r is null)return;var at=e.GetPosition(Overlay);var points=new[]{r.TopLeft,r.TopRight,r.BottomRight,r.BottomLeft};
        _drag=Enumerable.Range(0,4).OrderBy(i=>(ToCanvas(points[i])-at).Length).First(); if((ToCanvas(points[_drag])-at).Length>20){_drag=-1;return;} _preview=r;Overlay.CaptureMouse();
    }
    private void OnMove(object sender, MouseEventArgs e)
    {
        if(_drag<0||_preview is null||e.LeftButton!=MouseButtonState.Pressed)return;var p=ToNormalized(e.GetPosition(Overlay));
        _preview=new FacePerspectiveRegistrationModel { TopLeft=_drag==0?p:_preview.TopLeft,TopRight=_drag==1?p:_preview.TopRight,BottomRight=_drag==2?p:_preview.BottomRight,BottomLeft=_drag==3?p:_preview.BottomLeft };Draw();
    }
    private void OnUp(object sender, MouseButtonEventArgs e)
    {
        if(_drag<0)return;Overlay.ReleaseMouseCapture();var candidate=_preview?.Normalize();_drag=-1;_preview=null;if(candidate?.IsValid()==true)Workspace?.CommitRegistration(candidate);Draw();
    }
    private void OnReset(object sender, RoutedEventArgs e){Workspace?.ResetRegistration();Draw();}
}
