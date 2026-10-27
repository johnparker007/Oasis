using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Xunit;

namespace OasisEditor.Avalonia.Tests;

[Collection("Avalonia headless UI")]
public sealed class FloatingAssetWindowTests
{
    [Fact]
    public async Task JustOpenedMachineFinishesFramingItsModelBeforeItsCameraMovesToTheWindow()
    {
        await HeadlessTestHost.RunAsync(async()=>
        {
            var root=Temp();
            try
            {
                var session=new AuthoringSession(root);session.CreateProject("Camera");using var capture=new WorkspaceCapture(session);var view=capture.View;
                var source=Path.Combine(Path.GetDirectoryName(session.ProjectFilePath!)!,"Assets","Camera.glb");
                const string json="""{"asset":{"version":"2.0"},"scene":0,"scenes":[{"nodes":[0]}],"nodes":[{"mesh":0}],"meshes":[{"primitives":[{"attributes":{"POSITION":0}}]}],"buffers":[{"byteLength":36}],"bufferViews":[{"buffer":0,"byteLength":36}],"accessors":[{"bufferView":0,"componentType":5126,"count":3,"type":"VEC3","min":[0,0,0],"max":[1000,1000,0]}]}""";
                var text=System.Text.Encoding.UTF8.GetBytes(json.PadRight((json.Length+3)/4*4));
                var binary=new float[]{0,0,0,1000,0,0,0,1000,0}.SelectMany(BitConverter.GetBytes).ToArray();
                using(var writer=new BinaryWriter(File.Create(source)))
                {writer.Write(0x46546c67);writer.Write(2);writer.Write(12+8+text.Length+8+binary.Length);writer.Write(text.Length);writer.Write(0x4e4f534a);writer.Write(text);writer.Write(binary.Length);writer.Write(0x004e4942);writer.Write(binary);}
                var cabinet=view.Cabinets.Create("Camera Cabinet","Assets/Camera.glb");
                var machine=view.Machines.ListMachines().Single().ManifestPath;view.Machines.OpenMachine(machine);
                view.Machines.UpdateDocument(d=>d with{CabinetAsset=AssetReference.Project(Path.GetRelativePath(Path.GetDirectoryName(session.ProjectFilePath!)!,cabinet).Replace('\\','/'))});
                view.ShowMachine();
                await view.FloatDocumentAsync("Machine",machine);
                Assert.True(view.GetFloatingDocumentView("Machine",machine).SceneView.Camera.Distance>100);
                await view.DockDocumentAsync("Machine",machine);Assert.True(view.SceneView.Camera.Distance>100);
            }
            finally{Directory.Delete(root,true);}
        });
    }

    [Fact]
    public async Task TwoFloatingDocumentsEditIndependentlyAndWindowCloseRedocksDirtyHistory()
    {
        await HeadlessTestHost.RunAsync(async()=>
        {
            var root=Temp();
            try
            {
                var session=new AuthoringSession(root);session.CreateProject("Floating");using var capture=new WorkspaceCapture(session);var view=capture.View;
                var a=session.CreatePanel("Controls");var rectangle=session.AddRectangle();var source=session.AddSourceShape("Front",0,0,120,80);var original=File.ReadAllText(a);
                session.CreatePanel("Other");
                var face=view.Faces.CreateFace("Face");var lamp=view.Faces.AddLamp("Lamp",10,10,40,30);view.ShowFace();
                await view.FloatDocumentAsync("Panel2D",a);await view.FloatDocumentAsync("Face",face);
                Assert.Equal(2,view.InspectFloatingDocuments().Count);Assert.Equal(3,view.InspectOpenDocuments().Count);
                Assert.Equal(2,view.InspectOpenDocuments().Count(t=>t.IsFloating));Assert.Empty(session.Elements);
                Assert.True(view.HasUnsavedChanges);await Assert.ThrowsAsync<InvalidOperationException>(view.CloseProjectAsync);
                capture.Capture();Assert.Equal(2,capture.CaptureFloatingWindows().Count);
                var child=view.GetFloatingDocumentView("Panel2D",a);
                var name=child.GetVisualDescendants().OfType<TextBox>().Single(b=>b.Text=="Rectangle");name.Text="Floating edit";
                child.GetVisualDescendants().OfType<Button>().Single(b=>Equals(b.Content,"Apply")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                view.UpdateFloatingElement("Face",face,lamp,name:"Independent lamp");
                view.FloatingDocumentHistory("Panel2D",a);Assert.Equal(original,File.ReadAllText(a));
                view.FloatingDocumentHistory("Panel2D",a,true);
                Assert.Equal("Independent lamp",view.GetFloatingDocumentView("Face",face).Faces.Elements.Single().Name);
                child.GetVisualDescendants().OfType<ListBox>().Single(l=>l.ItemsSource is IReadOnlyList<FaceSourceShapeSnapshot>).SelectedIndex=0;
                var window=view.FloatingWorkspaceWindows.Single(w=>ReferenceEquals(w.Content,child));window.Close();await view.WaitForSceneAsync();
                Assert.Single(view.InspectFloatingDocuments());Assert.Equal(a,session.PanelPath);Assert.Equal("Floating edit",session.Elements.Single().Name);Assert.Equal(source,view.InspectWorkspaceState().SourceSelection);
                session.Undo();Assert.Equal("Rectangle",session.Elements.Single().Name);session.Redo();
                await view.SaveAllDocumentsAsync();Assert.False(view.HasUnsavedChanges);
                await view.DockDocumentAsync("Face",face);Assert.Empty(view.InspectFloatingDocuments());
                Assert.Equal("Independent lamp",view.Faces.Elements.Single().Name);Assert.True(view.Faces.CanUndo);
            }
            finally{Directory.Delete(root,true);}
        });
    }

    [Fact]
    public async Task FloatingWindowsPersistBoundsSelectionAndCameraAndRestorePreflightsBeforeReplacement()
    {
        await HeadlessTestHost.RunAsync(async()=>
        {
            var root=Temp();
            try
            {
                var session=new AuthoringSession(root);session.CreateProject("Floating");using var capture=new WorkspaceCapture(session);var view=capture.View;
                var panel=session.CreatePanel("Panel");var id=session.AddRectangle();session.Save();
                await view.FloatDocumentAsync("Panel2D",panel,new(130,160,1100,760));
                var reel=view.Reels.Create("Reel");await view.OpenReelAsync(reel);await view.FloatDocumentAsync("Reel",reel,new(180,190,1150,800));
                var reelView=view.GetFloatingDocumentView("Reel",reel);reelView.SceneView.SetCamera(reelView.SceneView.Camera with {Distance=2,Yaw=.6f});
                var state=view.InspectWorkspaceState();Assert.Equal(2,state.FloatingDocuments!.Length);
                await view.CloseProjectAsync();Assert.Empty(view.FloatingWorkspaceWindows);
                view.ApplyWorkspaceState(state);await view.WaitForSceneAsync();
                Assert.Equal(2,view.InspectFloatingDocuments().Count);
                Assert.Equal(id,view.GetFloatingDocumentView("Panel2D",panel).InspectWorkspaceState().PanelSelection!.Single());
                Assert.Equal(2,view.GetFloatingDocumentView("Reel",reel).SceneView.Camera.Distance);
                Assert.Equal(.6f,view.GetFloatingDocumentView("Reel",reel).SceneView.Camera.Yaw);
                Assert.Contains(view.FloatingWorkspaceWindows,w=>w.Position.X==130&&w.Position.Y==160&&w.Width==1100);
                var invalid=state with{FloatingDocuments=[state.FloatingDocuments[0] with{Bounds=new(0,0,double.NaN,800)}]};
                Assert.Throws<InvalidDataException>(()=>view.ApplyWorkspaceState(invalid));Assert.Equal(2,view.InspectFloatingDocuments().Count);
                await view.DockDocumentAsync("Reel",reel);Assert.Single(view.InspectFloatingDocuments());
                view.Reels.Update(widthMm:72);await view.WaitForSceneAsync();Assert.Equal(2,view.SceneView.Camera.Distance);
            }
            finally{Directory.Delete(root,true);}
        });
    }

    [Fact]
    public async Task FloatingMissingAssetRedocksWithoutReadingOrSavingItAndStillRejectsOverwrite()
    {
        await HeadlessTestHost.RunAsync(async()=>
        {
            var root=Temp();
            try
            {
                var session=new AuthoringSession(root);session.CreateProject("Floating");using var capture=new WorkspaceCapture(session);var view=capture.View;
                var panel=session.CreatePanel("Panel");var id=session.AddRectangle();File.Delete(panel);
                await view.FloatDocumentAsync("Panel2D",panel);view.UpdateFloatingElement("Panel2D",panel,id,name:"Still retained");
                await view.DockDocumentAsync("Panel2D",panel);Assert.Equal("Still retained",session.Elements.Single().Name);
                Assert.False(File.Exists(panel));Assert.ThrowsAny<IOException>(session.Save);
            }
            finally{Directory.Delete(root,true);}
        });
    }
    private static string Temp(){var root=Path.Combine(Path.GetTempPath(),"OasisAssetFloat-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);return root;}
}
