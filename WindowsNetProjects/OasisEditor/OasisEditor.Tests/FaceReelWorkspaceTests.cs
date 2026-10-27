using Avalonia.Controls;
using Avalonia.VisualTree;
using Xunit;

namespace OasisEditor.Avalonia.Tests;

[Collection("Avalonia headless UI")]
public sealed class FaceReelWorkspaceTests
{
    [Fact]
    public void InspectorTracksReelSettingsAcrossEditsAndHistoryEvenWhenGeometryIsUnchanged()
    {
        HeadlessTestHost.Run(()=>
        {
            var root=Path.Combine(Path.GetTempPath(),"OasisFaceReelView-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
            try
            {
                var project=new AuthoringSession(root);project.CreateProject("Project");using var capture=new WorkspaceCapture(project);var view=capture.View;
                view.Faces.CreateFace("Face");view.ShowFace();var id=view.Faces.AddReelMount("Reel",10,20,160,240,0);
                view.Faces.UpdateReelMount(id,new(){Stops=16,ReelLampsEnabled=false});capture.Capture();Assert.Equal("16",Stops());
                view.Faces.UpdateReelMount(id,new(){Stops=24});capture.Capture();Assert.Equal("24",Stops());
                view.Faces.Undo();capture.Capture();Assert.Equal("16",Stops());view.Faces.Redo();capture.Capture();Assert.Equal("24",Stops());
                Assert.Throws<ArgumentOutOfRangeException>(()=>view.Faces.UpdateReelMount(id,new(){BandOffset=2}));Assert.Equal("24",Stops());
                string? Stops()=>view.GetVisualDescendants().OfType<Grid>().Single(grid=>grid.Children.OfType<TextBlock>().Any(text=>text.Text=="Stops")).Children.OfType<TextBox>().Single().Text;
            }
            finally{Directory.Delete(root,true);}
        });
    }
}
