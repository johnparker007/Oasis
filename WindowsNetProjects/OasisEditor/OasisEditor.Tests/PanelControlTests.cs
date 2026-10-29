using System.Text.Json;
using OasisEditor.FacePipeline;
using OasisEditor.Rendering;
using SkiaSharp;
using Xunit;

namespace OasisEditor.Avalonia.Tests;

[Collection("Specialist renderers")]
public sealed class PanelControlTests : IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"OasisControls-"+Guid.NewGuid().ToString("N"));
    private readonly AuthoringSession session;
    public PanelControlTests(){Directory.CreateDirectory(root);session=new(root);session.CreateProject("Controls");session.CreatePanel("Panel");}
    [Fact]
    public void LiveNamesCountDescendantsAndCustomNamesRemainSeparateFromCounts()
    {
        var a=session.AddSpecialistElement("lamp",0,0);var b=session.AddSpecialistElement("lamp",50,0);session.SelectElements([a,b]);
        var group=session.CreateGroup();Assert.Equal("Lamp (2)",session.GroupLabel(group).DisplayName);Assert.Equal("Lamp",session.GroupLabel(group).Icon);
        var reel=session.AddSpecialistElement("reel",100,0);session.Select(reel);session.ReparentSelection(group);
        Assert.Equal("Mixed (3)",session.GroupLabel(group).DisplayName);Assert.Equal("Hierarchy",session.GroupLabel(group).Icon);
        var parent=session.CreateGroup(groupSelection:false);session.Select(group);session.ReparentSelection(parent);
        Assert.Equal(3,session.GroupLabel(parent).Count);session.UpdateGroup(group,name:"Gamble ladder");
        Assert.Equal("Gamble ladder (3)",session.GroupLabel(group).DisplayName);Assert.Equal("Gamble ladder",session.Groups.Single(g=>g.Id==group).Name);
        session.Select(a);session.DeleteSelected();Assert.Equal("Gamble ladder (2)",session.GroupLabel(group).DisplayName);
        session.UpdateGroup(group,automaticName:true);Assert.Equal("Mixed (2)",session.GroupLabel(group).DisplayName);
        session.Undo();Assert.Equal("Gamble ladder (2)",session.GroupLabel(group).DisplayName);session.Undo();Assert.Equal("Gamble ladder (3)",session.GroupLabel(group).DisplayName);
        session.UpdateGroup(group,icon:"Console");session.Select(reel);session.ReparentSelection(null);Assert.Equal("Console",session.GroupLabel(group).Icon);
    }
    [Fact]
    public void TwelveChannelsAndInputStayOneControlAndRoundTripWithoutChangingPixels()
    {
        var original=InstallControl(session,12);
        Assert.Equal(Render(original),Render(session.RenderDocument!));
        var control=Assert.Single(session.Groups.Where(g=>g.Control is not null));
        Assert.Equal(12,session.ControlChannels(control.Id).Count);Assert.Equal(1,session.GroupLabel(control.ParentId!).Count);
        Assert.Equal("Lamp (1)",session.GroupLabel(control.ParentId!).DisplayName);Assert.Equal("Lamp",session.GroupLabel(control.Id).Icon);
        Assert.All(session.Elements,e=>Assert.Equal(control.Id,e.ParentId));
        var input=Assert.Single(session.ImportedInputDefinitions);Assert.Equal("5",input.ButtonNumber);Assert.Equal(Guid.Parse(control.Id),input.LinkedVisualElementId);
        Assert.Equal("2",input.RawMfmeShortcut);input.ButtonNumber="999";Assert.Equal("5",session.ImportedInputDefinitions.Single().ButtonNumber);
        session.UpdateControlInput(control.Id,null);Assert.Empty(session.ImportedInputDefinitions);
        var disabled=session.Groups.Single(g=>g.Id==control.Id).Control!.Input!;Assert.False(disabled.Enabled);Assert.Equal("5",disabled.ButtonNumber);
        session.UpdateControlInput(control.Id,disabled with {Enabled=true});Assert.Equal(input.Id,session.ImportedInputDefinitions.Single().Id);
        session.Save();var reopened=new AuthoringSession(root);reopened.OpenProject(session.ProjectFilePath!);reopened.OpenPanel(session.PanelPath!);
        Assert.False(reopened.IsDirty);Assert.Equal(12,reopened.Elements.Count);Assert.Equal("5",reopened.ImportedInputDefinitions.Single().ButtonNumber);
        session.Select(control.Id);session.DuplicateSelected();Assert.Equal(2,session.Groups.Count(g=>g.Control is not null));
        Assert.Equal(2,session.ImportedInputDefinitions.Select(i=>i.Id).Distinct().Count());
        Assert.All(session.Groups.Where(g=>g.Control is not null),g=>Assert.All(session.Elements.Where(e=>e.ParentId==g.Id),e=>Assert.Equal(g.Id,session.RenderDocument!.Elements.Single(v=>v.ObjectId==e.Id).SharedSourceSetId)));
        session.Undo();Assert.Single(session.ImportedInputDefinitions);Assert.Equal(12,session.Elements.Count);
    }
    [Fact]
    public void InputEditsDeletionUndoAndFaceGenerationRetainBothCapabilities()
    {
        InstallControl(session,2);var control=session.Groups.Single(g=>g.Control is not null);var binding=control.Control!.Input!;
        Assert.Equal(binding.Id,session.HitTestInput(25,25,session.ImportedInputDefinitions));
        session.SelectElements(session.ControlChannels(control.Id).Select(e=>e.Id).ToArray());session.SetSelectedObjectState(visible:false);
        Assert.Equal(binding.Id,session.HitTestInput(25,25,session.ImportedInputDefinitions));session.Undo();
        session.UpdateControlInput(control.Id,binding with {ButtonNumber="7",KeyboardShortcut="N"});Assert.Equal("7",session.ImportedInputDefinitions.Single().ButtonNumber);
        session.UpdateControlInput(control.Id,null);Assert.Empty(session.ImportedInputDefinitions);session.Undo();Assert.Equal("7",session.ImportedInputDefinitions.Single().ButtonNumber);
        session.AddSourceShape("Face",0,0,200,200);session.Save();
        var panel=session.RenderDocument!;var face=new FaceGenerationService().GenerateFromPanelFaceSourceShape(panel,panel.FaceSourceShapes.Single(),"Face","panel").Document;
        Assert.Equal(2,face.Elements.OfType<FaceLampWindowElement>().Count());
        var button=Assert.Single(face.Elements.OfType<FaceButtonElement>());Assert.Equal(control.Id,button.LinkedPanel2DElementId);Assert.NotNull(button.LinkedInputReference);
        session.Select(control.Id);Assert.Throws<InvalidOperationException>(session.UngroupSelection);
        session.DeleteSelected();Assert.Empty(session.Elements);Assert.Empty(session.ImportedInputDefinitions);session.Undo();Assert.Equal(2,session.Elements.Count);Assert.Single(session.ImportedInputDefinitions);
        var reel=session.AddSpecialistElement("reel",0,0);session.Select(reel);Assert.Throws<ArgumentException>(()=>session.ReparentSelection(control.Id));
    }
    [Fact]
    public void ImportSectionsRemainConsecutiveAndInputDoesNotChangeLampClassification()
    {
        var sources=Source(2).ToList();sources.Add(new PanelElementModel {ObjectId=Guid.NewGuid().ToString("N"),Kind=PanelElementKind.Reel,Name="Reel",Width=10,Height=20,SourceComponentIndex=15});
        sources.Add(new PanelElementModel {ObjectId=Guid.NewGuid().ToString("N"),Kind=PanelElementKind.Lamp,Name="Lamp",X=80,Y=20,Width=20,Height=20,SourceComponentIndex=16,SharedSourceSetId="later"});
        var raw=JsonSerializer.Deserialize<Panel2DDocumentFile>(File.ReadAllText(session.PanelPath!))!;
        var imported=AuthoringSession.AttachFmlControls(raw,sources,[Input(sources[1].ObjectId)]);
        Assert.Equal(new[]{"lamp","lamp","reel","lamp"},imported.Elements.Select(e=>e.Kind));
        Assert.Equal(3,imported.Groups.Count(g=>g.ParentId is null));Assert.Equal(2,imported.Groups.Count(g=>g.Control is not null));
        Assert.All(imported.Groups.Where(g=>g.ParentId is null),g=>Assert.True(g.AutomaticName));
    }
    internal static Panel2DDocumentModel InstallControl(AuthoringSession session,int count=2)
    {
        var source=Source(count);var document=JsonSerializer.Deserialize<Panel2DDocumentFile>(File.ReadAllText(session.PanelPath!))!;
        var next=AuthoringSession.AttachFmlControls(document,source,[Input(source[^1].ObjectId)]);
        File.WriteAllText(session.PanelPath!,JsonSerializer.Serialize(next));var path=session.PanelPath!;session.OpenProject(session.ProjectFilePath!);session.OpenPanel(path);
        return new Panel2DDocumentModel {Elements=source};
    }
    private static PanelElementModel[] Source(int count)=>Enumerable.Range(0,count).Select(i=>new PanelElementModel
    {ObjectId=Guid.NewGuid().ToString("N"),Kind=PanelElementKind.Lamp,Name=$"Lamp {14+i}",DisplayText="Hold / Nudge",X=20,Y=20,Width=60,Height=40,
        SourceComponentIndex=14,SourceElementIndex=i,DisplayNumber=14+i,SharedSourceSetId="source",SharedSourceSetCount=count,OffColorHex=i%2==0?"#FF0000":"#0000FF"}).ToArray();
    private static InputDefinitionModel Input(string visual)=>new() {Id=Guid.NewGuid().ToString("N"),Name="Hold / Nudge",Kind=InputDefinitionKind.Button,ButtonNumber="5",KeyboardShortcut="2",RawMfmeShortcut="2",LinkedVisualElementId=Guid.Parse(visual)};
    private static byte[] Render(Panel2DDocumentModel document)
    {using var surface=SKSurface.Create(new SKImageInfo(200,200));surface.Canvas.Clear(SKColors.Transparent);Panel2DRendererFactory.Create("ControlTest").Render(surface.Canvas,document.Elements,new MachineRuntimeState(),PanelViewportTransform.Identity);using var image=surface.Snapshot();using var bitmap=SKBitmap.FromImage(image);return bitmap.Bytes;}
    public void Dispose()=>Directory.Delete(root,true);
}
