using Avalonia;
using Avalonia.Media;

namespace OasisEditor.Avalonia;

public sealed partial class PanelViewport
{
    public Func<string?>? SelectedSourceRegion { get; set; }
    public event Action<string>? SourceRegionSelected;
    private FaceSourceShapeSnapshot? _regionDrag;
    private int _regionCorner;
    private string? _regionPanel;
    private PointSnapshot[]? _regionPreview;

    private bool BeginRegionDrag(Point position)
    {
        if(FaceActive)return false;
        foreach(var region in session.SourceShapes.OrderBy(s=>s.Id==SelectedSourceRegion?.Invoke()?0:1))
        {
            for(var i=0;i<region.Points.Count;i++)
            {
                var point=region.Points[i];
                var screen=new Point(point.X*_zoom+_pan.X,point.Y*_zoom+_pan.Y);
                if(new Vector(position.X-screen.X,position.Y-screen.Y).Length>8)continue;
                _regionDrag=region;_regionCorner=i;_regionPanel=session.PanelPath;_regionPreview=region.Points.ToArray();
                _press=position;_drag=null;SourceRegionSelected?.Invoke(region.Id);InvalidateVisual();return true;
            }
        }
        return false;
    }
    private void MoveRegionDrag(Point position)
    {
        if(_regionDrag is null || _regionPreview is null)return;
        _regionPreview[_regionCorner]=new((position.X-_pan.X)/_zoom,(position.Y-_pan.Y)/_zoom);
        InvalidateVisual();
    }
    private void CommitRegionDrag()
    {
        var source=_regionDrag;var points=_regionPreview;_regionDrag=null;_regionPreview=null;
        if(source is null||points is null||session.PanelPath!=_regionPanel)return;
        var current=session.SourceShapes.FirstOrDefault(s=>s.Id==source.Id);
        if(current is null||!current.Points.SequenceEqual(source.Points))return;
        try{session.UpdateSourceShape(source.Id,source.Name,points);}
        catch(Exception ex) when(ex is ArgumentException or InvalidOperationException){CommandFailed?.Invoke(ex.Message);}
    }
    private void DrawSourceCorner(DrawingContext context,Point point,bool selected)
    {
        var size=selected?8:5;
        context.DrawRectangle(Brush("ViewportBackground"),new Pen(Brush("SelectionColor"),1.5),new Rect(point-new Vector(size/2d,size/2d),new Size(size,size)));
    }
}
