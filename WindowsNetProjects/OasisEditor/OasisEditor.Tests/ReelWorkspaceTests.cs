using Xunit;

namespace OasisEditor.Avalonia.Tests;

[Collection("Avalonia headless UI")]
public sealed class ReelWorkspaceTests
{
    [Fact]
    public async Task ReelEditsPreviewAndWorkspaceRestoreRetainNativeIdentityAndDimensions()
    {
        await HeadlessTestHost.RunAsync(async()=>
        {
            var root=Path.Combine(Path.GetTempPath(),"OasisReelWorkspace-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
            try
            {
                var session=new AuthoringSession(root);session.CreateProject("Project");
                using var capture=new WorkspaceCapture(session);var view=capture.View;
                var path=view.Reels.Create("Reel");await view.OpenReelAsync(path);var id=view.Reels.Document.Id;
                view.Reels.Update("Edited Reel",290,70);await view.WaitForSceneAsync();
                Assert.True(view.HasUnsavedChanges);Assert.Equal(.145f,view.SceneView.Scene.Maximum.Y,5);
                Assert.Equal(.035f,view.SceneView.Scene.Maximum.X,5);
                view.Reels.Save();Assert.True(view.Reels.IsProjectCurrent);
                Assert.Equal("Reel",view.SceneKind);Assert.NotNull(view.Reels.FilePath);var state=view.InspectWorkspaceState();Assert.True(state.Scene3D!.Reel is not null,WorkspaceLayoutState.Serialize(state));
                view.ShowPanel();view.ApplyWorkspaceState(WorkspaceLayoutState.Deserialize(WorkspaceLayoutState.Serialize(state)));await view.WaitForSceneAsync();
                Assert.Equal("Reel",view.SceneKind);Assert.Equal(id,view.Reels.Document.Id);Assert.Equal(290,view.Reels.Document.DiameterMm);
                Assert.Throws<InvalidDataException>(()=>view.ApplyWorkspaceState(state with{Scene3D=state.Scene3D with{Reel="../escape"}}));
                Assert.Equal(id,view.Reels.Document.Id);
            }
            finally{Directory.Delete(root,true);}
        });
    }
}
