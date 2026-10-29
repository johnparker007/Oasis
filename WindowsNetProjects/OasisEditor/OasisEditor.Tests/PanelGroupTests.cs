using OasisEditor.Rendering;
using OasisEditor.FacePipeline;
using SkiaSharp;
using Xunit;

namespace OasisEditor.Avalonia.Tests;

[Collection("Specialist renderers")]
public sealed class PanelGroupTests : IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"OasisGroups-"+Guid.NewGuid().ToString("N"));
    private readonly AuthoringSession session;
    public PanelGroupTests(){Directory.CreateDirectory(root);session=new(root);session.CreateProject("Groups");session.CreatePanel("Panel");}
    private string Lamp(string color,double x=10,double y=10)
    {var id=session.AddSpecialistElement("lamp",x,y);session.UpdateElement(id,width:20,height:20);session.UpdateSpecialistSettings(id,new(OffColorHex:color));return id;}
    private static SKColor Pixel(AuthoringSession s,int x,int y)
    {
        using var surface=SKSurface.Create(new SKImageInfo(300,300));surface.Canvas.Clear(SKColors.Transparent);
        Panel2DRendererFactory.Create("GroupTest").Render(surface.Canvas,s.RenderDocument!.Elements,new MachineRuntimeState(),PanelViewportTransform.Identity);
        using var bitmap=SKBitmap.FromImage(surface.Snapshot());return bitmap.GetPixel(x,y);
    }
    [Fact]
    public void GroupingConsecutiveObjectsPreservesPixelsAndReorderingChangesOverlaps()
    {
        var red=Lamp("#FF0000");var blue=Lamp("#0000FF");var green=Lamp("#00FF00",80);
        var baseline=Pixel(session,15,15);Assert.Equal(SKColors.Blue,baseline);
        session.SelectElements([red,blue]);var group=session.CreateGroup("Lamps",icon:"Lamp");
        Assert.Equal(new[]{red,blue,green},session.Elements.Select(e=>e.Id));Assert.Equal(baseline,Pixel(session,15,15));
        session.Select(blue);session.MoveSelectionInOrder(false);Assert.Equal(SKColors.Red,Pixel(session,15,15));
        session.Undo();Assert.Equal(baseline,Pixel(session,15,15));session.Redo();Assert.Equal(SKColors.Red,Pixel(session,15,15));
        session.Select(group);session.ReorderSelection(green,after:true);Assert.Equal(new[]{green,blue,red},session.Elements.Select(e=>e.Id));
        session.UngroupSelection();Assert.Empty(session.Groups);
        session.Select(red);session.MoveSelectionInOrder(false);Assert.Equal(new[]{green,red,blue},session.Elements.Select(e=>e.Id));
        session.BringSelectionToFront();Assert.Equal(new[]{green,blue,red},session.Elements.Select(e=>e.Id));
        session.SendSelectionToBack();Assert.Equal(new[]{red,green,blue},session.Elements.Select(e=>e.Id));
    }
    [Fact]
    public void NestedTransformsReparentAndUngroupPreserveWorldGeometryIncludingShear()
    {
        var lamp=Lamp("#FF0000");var a=session.CreateGroup("A");session.UpdateGroup(a,x:80,y:40,rotation:30,scaleX:2,scaleY:1);
        var b=session.CreateGroup("B",groupSelection:false);session.UpdateGroup(b,x:10,y:5,rotation:-20,scaleX:1,scaleY:3);
        var before=session.RenderDocument!.Elements.Single();
        session.Select(a);session.ReparentSelection(b);
        AssertWorld(before,session.RenderDocument!.Elements.Single());
        session.Select(a);session.UngroupSelection();Assert.DoesNotContain(session.Groups,g=>g.Id==a);
        AssertWorld(before,session.RenderDocument!.Elements.Single());
        session.Undo();Assert.Contains(session.Groups,g=>g.Id==a);AssertWorld(before,session.RenderDocument!.Elements.Single());
        session.Select(lamp);session.MoveSelected(12,-9);var after=session.RenderDocument!.Elements.Single();
        var p=(before.Transform??PanelTransform.Identity).Map(before.X,before.Y);var q=(after.Transform??PanelTransform.Identity).Map(after.X,after.Y);
        Assert.Equal(p.X+12,q.X,8);Assert.Equal(p.Y-9,q.Y,8);
    }
    [Fact]
    public void ParentVisibilityAndLocksAreInheritedWithoutOverwritingChildren()
    {
        var a=Lamp("#FF0000");var b=Lamp("#0000FF",60);session.UpdateElement(a,visible:false);
        session.SelectElements([a,b]);var group=session.CreateGroup("Lamps");session.UpdateGroup(group,visible:false);
        Assert.All(session.RenderDocument!.Elements,e=>Assert.False(e.IsVisible));session.UpdateGroup(group,visible:true,locked:true);
        Assert.False(session.RenderDocument!.Elements.Single(e=>e.ObjectId==a).IsVisible);
        session.Select(b);Assert.Throws<InvalidOperationException>(()=>session.MoveSelected(1,1));
        Assert.Throws<InvalidOperationException>(()=>session.UpdateElement(b,x:20));
        session.UpdateGroup(group,locked:false);session.MoveSelected(1,1);Assert.True(session.IsDirty);
    }
    [Fact]
    public void DuplicateClipboardDeleteAndSaveRetainGroupStructureAndFreshIds()
    {
        var a=Lamp("#FF0000");var group=session.CreateGroup("Lamps",icon:"Lamp");session.UpdateGroup(group,x:40,rotation:20,scaleX:2);
        session.Select(group);Assert.True(session.CopySelectedToClipboard());var duplicate=Assert.Single(session.DuplicateSelected());
        Assert.NotEqual(group,duplicate);Assert.Equal(2,session.Groups.Count);Assert.Equal(2,session.Elements.Count);
        session.DeleteSelected();Assert.Single(session.Groups);session.Undo();Assert.Equal(2,session.Groups.Count);
        session.Save();var path=session.PanelPath!;var project=session.ProjectFilePath!;
        var reopened=new AuthoringSession(root);reopened.OpenProject(project);reopened.OpenPanel(path);
        Assert.False(reopened.IsDirty);Assert.Equal(session.Groups,reopened.Groups);Assert.Equal(session.Elements,reopened.Elements);
        var pasted=Assert.Single(session.PasteClipboard());Assert.NotEqual(duplicate,pasted);Assert.Equal(3,session.Groups.Count);
        session.Select(group);session.DeleteSelected();Assert.DoesNotContain(session.Elements,e=>e.Id==a);
        session.Undo();Assert.Contains(session.Elements,e=>e.Id==a);
    }
    [Fact]
    public void CyclesInvalidParentsIconsAndSingularTransformsFailAtomically()
    {
        Lamp("#FF0000");var group=session.CreateGroup();var child=session.CreateGroup("Child",group,groupSelection:false);session.Save();
        session.Select(group);Assert.Throws<ArgumentException>(()=>session.ReparentSelection(child));Assert.False(session.IsDirty);
        Assert.Throws<ArgumentException>(()=>session.UpdateGroup(group,scaleX:0));Assert.Throws<ArgumentException>(()=>session.UpdateGroup(group,icon:"External"));
        Assert.Throws<ArgumentException>(()=>session.ReparentSelection("missing"));Assert.False(session.IsDirty);
    }
    [Fact]
    public void HitTestingUsesInverseTransformAndActualRotatedRectangle()
    {
        var lamp=Lamp("#FF0000",0,0);var g=session.CreateGroup();session.UpdateGroup(g,x:100,y:100,rotation:45,scaleX:2);
        var t=session.RenderDocument!.Elements.Single().Transform!;var inside=t.Map(10,10);
        Assert.Equal(lamp,session.HitTestPanel(inside.X,inside.Y));
        var bounds=session.ViewportElements.Single(e=>e.Id==lamp);Assert.Null(session.HitTestPanel(bounds.X+1,bounds.Y+1));
        session.UpdateGroup(g,visible:false);Assert.Null(session.HitTestPanel(inside.X,inside.Y));
    }
    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    public void LegacySchemaPanelsLoadWithoutDirtyingAndGroupedSaveUsesNewSchema(int version)
    {
        var path=session.PanelPath!;var content=File.ReadAllText(path).Replace($"\"SchemaVersion\": {Panel2DDocumentStorage.CurrentSchemaVersion}",$"\"SchemaVersion\": {version}");File.WriteAllText(path,content);
        var reopened=new AuthoringSession(root);reopened.OpenProject(session.ProjectFilePath!);reopened.OpenPanel(path);Assert.False(reopened.IsDirty);
        reopened.CreateGroup();reopened.Save();Assert.Contains($"\"SchemaVersion\": {Panel2DDocumentStorage.CurrentSchemaVersion}",File.ReadAllText(path));
        reopened.Undo();Assert.Empty(reopened.Groups);
    }
    [Fact]
    public void FaceGenerationAndLampMasksUseTransformedGroupCoordinates()
    {
        var id=Lamp("#FF0000",0,0);var group=session.CreateGroup();session.UpdateGroup(group,x:100,y:100,rotation:90);
        session.AddSourceShape("Face",0,0,200,200);session.Save();
        Assert.True(Panel2DDocumentStorage.TryReadValidated(File.ReadAllText(session.PanelPath!),out var file,out _));
        var raw=Panel2DDocumentStorage.ToModel(file);var shape=raw.FaceSourceShapes.Single();
        var generated=new FaceGenerationService().GenerateFromPanelFaceSourceShape(raw,shape,"Face","panel");
        var lamp=Assert.Single(generated.Document.Elements.OfType<FaceLampWindowElement>());
        Assert.Equal(80,lamp.X,6);Assert.Equal(100,lamp.Y,6);Assert.Equal(20,lamp.Width,6);Assert.Equal(20,lamp.Height,6);
        using var source=new SKBitmap(20,20);source.Erase(SKColors.White);var mask=new byte[200*200];
        FaceGenerationService.CompositeSourceShapeLampMask(mask,200,200,shape,session.RenderDocument!.Elements.Single(),source,lamp,1,new OasisEditor.FacePipeline.ImageProcessingExecutionOptions(1));
        Assert.Equal(255,mask[110*200+90]);Assert.Equal(0,mask[10*200+10]);
    }
    [Fact]
    public void MovingGroupedChildUpdatesLocalInspectorPositionAndNoOpApplyKeepsRedo()
    {
        var id=Lamp("#FF0000");var g=session.CreateGroup();session.UpdateGroup(g,x:40,y:20,rotation:25,scaleX:2);
        session.Select(id);session.ReparentSelection(null);var before=session.Elements.Single();
        session.UpdateElement(id,x:before.X+10,y:before.Y+20);var point=session.RenderDocument!.Elements.Single();
        var origin=(point.Transform??PanelTransform.Identity).Map(point.X,point.Y);
        Assert.Equal(point.X,origin.X,8);Assert.Equal(point.Y,origin.Y,8);
        session.Undo();Assert.True(session.CanRedo);
        var group=session.Groups.Single();var parts=group.Transform.Decompose();
        Assert.False(session.UpdateGroup(g,x:group.Transform.X,y:group.Transform.Y,rotation:parts.Rotation,scaleX:parts.ScaleX,scaleY:parts.ScaleY));
        Assert.True(session.CanRedo);
    }
    private static void AssertWorld(PanelElementModel a,PanelElementModel b)
    {
        foreach(var corner in new[]{(0d,0d),(a.Width,0d),(a.Width,a.Height),(0d,a.Height)})
        {var p=(a.Transform??PanelTransform.Identity).Map(a.X+corner.Item1,a.Y+corner.Item2);var q=(b.Transform??PanelTransform.Identity).Map(b.X+corner.Item1,b.Y+corner.Item2);Assert.Equal(p.X,q.X,8);Assert.Equal(p.Y,q.Y,8);}
    }
    public void Dispose()=>Directory.Delete(root,true);
}
