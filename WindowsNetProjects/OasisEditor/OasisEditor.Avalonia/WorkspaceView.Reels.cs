using System.Numerics;
using Avalonia.Controls;
using Avalonia.Media;
using OasisEditor.Avalonia.Rendering3D;

namespace OasisEditor.Avalonia;

public sealed partial class WorkspaceView
{
    public ReelAuthoringSession Reels { get; private set; } = null!;
    private void InitializeReels(MenuItem create)
    {
        Reels = new(_session);
        Reels.Changed += On3DAssetChanged;
        MenuAction(create,"Reel…",async () => await CreateReelDialogAsync());
    }
    public async Task OpenReelAsync(string path)
    {
        if(FindAssetWindow("Reel",Path.GetFullPath(path,_session.RequireProjectDirectory())) is {} owned){ShowOwnedAssetWindow(owned.Window);return;}
        if (!SameScenePath(Reels.FilePath,Path.GetFullPath(path,_session.RequireProjectDirectory())) || !Reels.IsDirty) Reels.Open(path);
        _faceActive = _machineActive = false; _sceneKind = "Reel"; _selectedSceneNodeId = Reels.Document.Id;
        await ReloadSceneAsync(); RestoreDocumentCamera(); Refresh();
    }
    private void ReloadReelScene()
    {
        var reel = Reels.Document;
        var width = (float)(reel.WidthMm / 1000); var radius = (float)(reel.DiameterMm / 2000);
        if (!float.IsFinite(width) || !float.IsFinite(radius) || width <= 0 || radius <= 0)
            throw new InvalidDataException("Reel dimensions exceed the supported viewport range.");
        _composition=null; _mountedFaces.Clear(); _faceOutputKeys.Clear(); _meshNodeIds.Clear();
        var vertices = new List<float>(); var indices = new List<int>();
        const int segments=96;
        for(var i=0;i<segments;i++)
        {
            var a=i*MathF.Tau/segments;var b=(i+1)*MathF.Tau/segments;
            var n0=new Vector3(0,MathF.Cos(a),MathF.Sin(a));var n1=new Vector3(0,MathF.Cos(b),MathF.Sin(b));
            Quad(new(-width/2,n0.Y*radius,n0.Z*radius),new(width/2,n0.Y*radius,n0.Z*radius),new(width/2,n1.Y*radius,n1.Z*radius),new(-width/2,n1.Y*radius,n1.Z*radius),n0,n1);
            Triangle(new(-width/2,0,0),new(-width/2,n1.Y*radius,n1.Z*radius),new(-width/2,n0.Y*radius,n0.Z*radius),-Vector3.UnitX);
            Triangle(new(width/2,0,0),new(width/2,n0.Y*radius,n0.Z*radius),new(width/2,n1.Y*radius,n1.Z*radius),Vector3.UnitX);
        }
        var color = this.TryFindResource("ReelSurfaceColor",ActualThemeVariant,out var resource) && resource is Color c
            ? new Vector4(c.R/255f,c.G/255f,c.B/255f,1) : throw new InvalidOperationException("Missing Reel surface theme color.");
        var mesh = new SceneRenderMesh("reel",reel.DisplayName,vertices.ToArray(),indices.ToArray(),new(color,Vector3.Zero,DoubleSided:true),Matrix4x4.Identity);
        _meshNodeIds[mesh.Id]=reel.Id;
        _baseScene=new([mesh],new(-width/2,-radius,-radius),new(width/2,radius,radius),[]);
        _sceneHierarchyUpdating=true;
        try { _sceneRows.Clear();_sceneRows.Add(new(reel.Id,reel.DisplayName,"Reel",0));_sceneHierarchy.SelectedItem=_sceneRows[0]; }
        finally {_sceneHierarchyUpdating=false;}
        var identity="Reel:"+Reels.FilePath;UpdateSceneDisplay(identity!=_sceneIdentity);_sceneIdentity=identity;
        Refresh3DInspector();SetStatus($"Reel • {reel.DiameterMm:g} mm diameter × {reel.WidthMm:g} mm width");
        void Vertex(Vector3 p,Vector3 n) => vertices.AddRange([p.X,p.Y,p.Z,n.X,n.Y,n.Z,0,0]);
        void Triangle(Vector3 a,Vector3 b,Vector3 c,Vector3 n)
        {var start=vertices.Count/8;Vertex(a,n);Vertex(b,n);Vertex(c,n);indices.AddRange([start,start+1,start+2]);}
        void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector3 n0,Vector3 n1)
        {var start=vertices.Count/8;Vertex(a,n0);Vertex(b,n0);Vertex(c,n1);Vertex(d,n1);indices.AddRange([start,start+1,start+2,start,start+2,start+3]);}
    }
    private void RefreshReelInspector()
    {
        _inspector.Children.Clear();_fields.Clear();var reel=Reels.Document;
        Heading("PHYSICAL REEL");
        _inspector.Children.Add(new TextBlock{Text="ID: "+reel.Id,TextWrapping=TextWrapping.Wrap,Opacity=.7});
        Field("Display name",reel.DisplayName);Field("Diameter mm",reel.DiameterMm);Field("Width mm",reel.WidthMm);
        _inspector.Children.Add(ActionButton("Apply Reel settings",()=>Try(()=>Reels.Update(_fields["Display name"].Text,Number("Diameter mm"),Number("Width mm")))));
        _inspector.Children.Add(new TextBlock{Text="This reusable asset defines the physical reel. Symbol artwork and lamp windows are authored on the Face; the Machine assigns the Reel to a reel reference.",TextWrapping=TextWrapping.Wrap,Margin=new(0,12,0,0),Opacity=.7});
    }
    private async Task CreateReelDialogAsync()
    {
        if(global::Avalonia.Controls.TopLevel.GetTopLevel(this) is not Window owner)return;
        var name=new TextBox{PlaceholderText="Reel name"};var error=new TextBlock{TextWrapping=TextWrapping.Wrap};
        var content=new StackPanel{Margin=new(16),Spacing=10,Children={name,error}};
        var dialog=new Window{Title="Create Reel",Width=400,Height=200,Content=content};
        content.Children.Add(ActionButton("Create Reel",async()=>{try{var path=Reels.Create(name.Text??"");await OpenReelAsync(path);dialog.Close();}catch(Exception e) when(e is not OutOfMemoryException){error.Text=e.Message;}}));
        await dialog.ShowDialog(owner);
    }
}
