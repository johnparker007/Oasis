using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Xunit;
namespace OasisEditor.Avalonia.Tests;
[Collection("Avalonia headless UI")]
public sealed class CompactWorkspaceVerificationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ProjectAssetsRemainVisibleAtMinimumWorkspaceSize(bool restore)
    {
        HeadlessTestHost.Run(() =>
        {
            var root=Path.Combine(Path.GetTempPath(),"OasisCompact-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            try
            {
                var session=new AuthoringSession(root);session.CreateProject("Compact");session.CreatePanel("A lengthy asset name for compact layout verification");
                using var capture=new WorkspaceCapture(session);
                capture.Capture(960,640);
                if (restore)
                {
                    capture.View.ConfigureWorkspacePersistence(Path.Combine(root,"workspace.json"),restore:false);
                    capture.View.SaveWorkspace();capture.View.ResetLayout();capture.View.RestoreWorkspace();
                    capture.Capture(960,640);
                }
                var assets=capture.View.GetVisualDescendants().OfType<ListBox>().Single(list=>list.ItemsSource is AssetSummary[]);
                Assert.True(assets.Bounds.Height>=48,$"Only {assets.Bounds.Height} logical pixels remain for project assets.");
                Assert.Contains(capture.View.GetVisualDescendants().OfType<Button>(),b=>Equals(b.Content,"New panel")&&b.IsEffectivelyVisible);
            }
            finally {Directory.Delete(root,true);}
        });
    }
    [Fact]
    public void OutputActionsStayInsideCompactPane()
    {
        HeadlessTestHost.Run(() =>
        {
            using var service = new OutputLogService();
            using var pane = new OutputPaneControl(service);
            var window = new Window { Width=500,Height=250,Content=pane };window.Show();window.UpdateLayout();
            try
            {
                foreach(var caption in new[]{"Copy","Clear"})
                {
                    var button=pane.GetVisualDescendants().OfType<Button>().Single(b=>Equals(b.Content,caption));
                    var position=button.TranslatePoint(new Point(button.Bounds.Width/2,button.Bounds.Height/2),pane)!.Value;
                    Assert.True(position.X>=0 && position.X<=pane.Bounds.Width && position.Y<=pane.Bounds.Height,$"{caption} is clipped at {position} in {pane.Bounds}.");
                }
            }
            finally {window.Close();}
        });
    }

    [Fact]
    public async Task CompactSceneToolbarCanScrollToItsLastAction()
    {
        await HeadlessTestHost.RunAsync(async()=>
        {
            var root=Path.Combine(Path.GetTempPath(),"OasisCompactScene-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
            try
            {
                var session=new AuthoringSession(root);session.CreateProject("Compact scene");using var capture=new WorkspaceCapture(session);
                var reel=capture.View.Reels.Create("Reel");await capture.View.OpenReelAsync(reel);capture.Capture(960,640);
                var trigger=capture.View.GetVisualDescendants().OfType<CheckBox>().Single(c=>Equals(c.Content,"Triggers"));
                var scroll=trigger.GetVisualAncestors().OfType<ScrollViewer>().First();
                Assert.True(scroll.Extent.Width>scroll.Viewport.Width);
                scroll.Offset=new Vector(scroll.Extent.Width,0);capture.Capture(960,640);
                var center=trigger.TranslatePoint(new Point(trigger.Bounds.Width/2,trigger.Bounds.Height/2),scroll)!.Value;
                Assert.InRange(center.X,0,scroll.Bounds.Width);
                Assert.Equal(global::Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,scroll.HorizontalScrollBarVisibility);
            }
            finally{Directory.Delete(root,true);}
        });
    }

}
