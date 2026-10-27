using Xunit;

namespace OasisEditor.Avalonia.Tests;

public sealed class DocumentRetentionTests
{
    [Fact]
    public void PanelFaceMachineAndReusableAssetHistoriesRemainIndependent()
    {
        var root=Path.Combine(Path.GetTempPath(),"OasisTabs-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            var p=new AuthoringSession(root);p.CreateProject("Tabs");p.EnableDocumentRetention();
            var a=p.CreatePanel("A");var id=p.AddRectangle();p.UpdateElement(id,name:"Pending A");
            var b=p.CreatePanel("B");p.AddRectangle();p.Save();p.OpenPanel(a);
            Assert.True(p.IsDirty);Assert.Equal("Pending A",p.Elements.Single().Name);p.Undo();Assert.NotEqual("Pending A",p.Elements.Single().Name);p.Redo();
            p.OpenPanel(b);Assert.True(p.HasRetainedChanges);Assert.Throws<InvalidOperationException>(p.CloseProject);
            p.CloseActiveDocument();p.OpenPanel(a);p.Save();Assert.False(p.HasRetainedChanges);

            var f=new FaceAuthoringSession(p);f.EnableDocumentRetention();var fa=f.CreateFace("A");f.AddLamp("Pending lamp",0,0,20,20);
            f.CreateFace("B");Assert.True(f.HasRetainedChanges);f.OpenFace(fa);Assert.True(f.IsDirty);Assert.Single(f.Elements);
            f.Undo();Assert.Empty(f.Elements);f.Redo();Assert.Single(f.Elements);f.Save();Assert.False(f.HasRetainedChanges);

            var m=new MachineAuthoringSession(p);m.EnableDocumentRetention();var ma=m.CreateMachine("A");m.UpdateDocument(d=>d with {DisplayName="Pending machine"});
            m.CreateMachine("B");Assert.True(m.HasRetainedChanges);m.OpenMachine(ma);Assert.Equal("Pending machine",m.MachineTitle);m.Undo();Assert.Equal("A",m.MachineTitle);m.Redo();m.Save();
            Assert.False(m.HasRetainedChanges);

            var reel=new ReelAuthoringSession(p);reel.EnableDocumentRetention();var ra=reel.Create("A");reel.Update(widthMm:72);
            reel.Create("B");reel.Open(ra);Assert.Equal(72,reel.Document.WidthMm);Assert.True(reel.IsDirty);reel.Undo();Assert.False(reel.IsDirty);reel.Redo();reel.Save();
            var prefab=new PrefabAuthoringSession(p);prefab.EnableDocumentRetention();var pa=prefab.Create("A");prefab.AddNode(prefab.Document.Root.Id,"Pending child");
            prefab.Create("B");prefab.Open(pa);Assert.Single(prefab.Document.Root.Children);Assert.True(prefab.IsDirty);prefab.Undo();Assert.Empty(prefab.Document.Root.Children);prefab.Redo();prefab.Save();
        }
        finally{Directory.Delete(root,true);}
    }

    [Fact]
    public void RetainedDocumentStillRejectsExternalOverwrite()
    {
        var root=Path.Combine(Path.GetTempPath(),"OasisTabConflict-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            var p=new AuthoringSession(root);p.CreateProject("Tabs");p.EnableDocumentRetention();
            var a=p.CreatePanel("A");p.AddRectangle();p.CreatePanel("B");
            File.AppendAllText(a," ");p.OpenPanel(a);Assert.True(p.IsDirty);Assert.Throws<IOException>(p.Save);
            Assert.True(p.HasRetainedChanges);Assert.Throws<InvalidOperationException>(p.CloseActiveDocument);
        }
        finally{Directory.Delete(root,true);}
    }
}

[Collection("Avalonia headless UI")]
public sealed class DocumentTabViewTests
{
    [Fact]
    public async Task ReelTabsRetainTheirOwnCameraAndNativeDimensions()
    {
        await HeadlessTestHost.RunAsync(async()=>
        {
            var root=Path.Combine(Path.GetTempPath(),"OasisReelTabs-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
            try
            {
                var p=new AuthoringSession(root);p.CreateProject("Tabs");using var capture=new WorkspaceCapture(p);var v=capture.View;
                var a=v.Reels.Create("A");await v.OpenReelAsync(a);
                v.SceneView.SetCamera(v.SceneView.Camera with {Yaw=.7f,Pitch=.3f,Distance=2});
                var b=v.Reels.Create("B");await v.OpenReelAsync(b);v.SceneView.SetCamera(v.SceneView.Camera with {Distance=3});
                await v.ActivateDocumentAsync("Reel",a);Assert.Equal(2,v.SceneView.Camera.Distance);Assert.Equal(.7f,v.SceneView.Camera.Yaw);
                await v.ActivateDocumentAsync("Reel",b);Assert.Equal(3,v.SceneView.Camera.Distance);
            }
            finally{Directory.Delete(root,true);}
        });
    }

    [Fact]
    public void TabsCloseGuardsAndWorkspaceReopenUseRealSessions()
    {
        HeadlessTestHost.Run(()=>
        {
            var root=Path.Combine(Path.GetTempPath(),"OasisWorkspaceTabs-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
            try
            {
                var p=new AuthoringSession(root);p.CreateProject("Tabs");
                using var capture=new WorkspaceCapture(p);var v=capture.View;
                var a=p.CreatePanel("A");p.AddRectangle();var b=p.CreatePanel("B");v.ShowPanel();
                Assert.Equal(2,v.InspectOpenDocuments().Count);Assert.True(v.HasUnsavedChanges);
                Assert.ThrowsAsync<InvalidOperationException>(()=>v.CloseDocumentAsync("Panel2D",a)).GetAwaiter().GetResult();
                v.ActivateDocumentAsync("Panel2D",a).GetAwaiter().GetResult();Assert.True(p.IsDirty);
                v.SaveAllDocumentsAsync().GetAwaiter().GetResult();Assert.False(v.HasUnsavedChanges);
                v.Faces.CreateFace("Face");v.ShowFace();
                var state=v.InspectWorkspaceState();Assert.Equal(3,state.Documents!.Length);
                Assert.True(capture.Capture().Length>5000);
                v.CloseProjectAsync().GetAwaiter().GetResult();Assert.Empty(v.InspectOpenDocuments());
                v.ApplyWorkspaceState(state);Assert.Equal(3,v.InspectOpenDocuments().Count);Assert.Equal("Face",v.InspectOpenDocuments().Single(t=>t.IsActive).Kind);
                File.Delete(b); // Closing a clean background tab must not need its source file.
                v.CloseDocumentAsync("Panel2D",b).GetAwaiter().GetResult();Assert.Equal(2,v.InspectOpenDocuments().Count);
                Assert.Equal("Face",v.InspectOpenDocuments().Single(t=>t.IsActive).Kind);
                var invalid=state with{Documents=[new("Face","../escape/asset.face")]};
                Assert.ThrowsAny<Exception>(()=>v.ApplyWorkspaceState(invalid));Assert.Equal(2,v.InspectOpenDocuments().Count);
            }
            finally{Directory.Delete(root,true);}
        });
    }
}
