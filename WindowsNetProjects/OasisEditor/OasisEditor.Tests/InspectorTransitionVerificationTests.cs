using Avalonia.Controls;
using Avalonia.VisualTree;
using Avalonia.Interactivity;
using Xunit;
namespace OasisEditor.Avalonia.Tests;
[Collection("Avalonia headless UI")]
public sealed class InspectorTransitionVerificationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReturningFromReelRestoresTheEditablePanelOrFaceInspector(bool face)
    {
        await HeadlessTestHost.RunAsync(async()=>
        {
            var root=Path.Combine(Path.GetTempPath(),"OasisInspectorTransition-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
            try
            {
                var session=new AuthoringSession(root);session.CreateProject("Transitions");var panel=session.CreatePanel("Panel");
                var rectangle=session.AddRectangle();session.UpdateElement(rectangle,name:"Panel rectangle");session.Save();
                using var capture=new WorkspaceCapture(session);var view=capture.View;
                await view.SynchronizeAssetDocumentsAsync();
                var path=panel;
                if(face)
                {
                    path=view.Faces.CreateFace("Face");view.Faces.AddLamp("Face lamp",10,10,100,60,1);view.ShowFace();
                    await view.SynchronizeAssetDocumentsAsync();
                }
                capture.Capture();
                var reel=view.Reels.Create("Physical Reel");await view.OpenReelAsync(reel);await view.SynchronizeAssetDocumentsAsync();capture.Capture();
                var reelEditor=view.GetDocumentEditor("Reel");
                Assert.Contains(view.GetVisualDescendants().OfType<Button>(),b=>Equals(b.Content,"Apply Reel settings"));
                await view.ActivateDocumentAsync(face?"Face":"Panel2D",path);capture.Capture();
                Assert.DoesNotContain(view.GetVisualDescendants().OfType<Button>(),b=>Equals(b.Content,"Apply Reel settings"));
                var editor=view.CommandView;
                var name=view.GetVisualDescendants().OfType<TextBox>().Single(b=>b.Text==(face?"Face lamp":"Panel rectangle"));
                name.Text="Edited after returning";
                view.GetVisualDescendants().OfType<Button>().Single(b=>Equals(b.Content,"Apply")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.Equal("Edited after returning",face?Assert.Single(editor.Faces.Elements).Name:Assert.Single(editor.CommandSession.Elements).Name);
                Assert.Equal("Physical Reel",reelEditor.Reels.Document.DisplayName);
            }
            finally{Directory.Delete(root,true);}
        });
    }
}
