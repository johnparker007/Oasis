using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Xunit;

namespace OasisEditor.Avalonia.Tests;

[Collection("Avalonia headless UI")]
public sealed class SourceRegionInteractionTests
{
    [Fact]
    public void DraggingCornerCommitsOneUndoableConvexRegionAndRejectsCrossing()
    {
        HeadlessTestHost.Run(()=>
        {
            var root=Path.Combine(Path.GetTempPath(),"OasisCorner-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
            try
            {
                var p=new AuthoringSession(root);p.CreateProject("Corners");p.CreatePanel("Panel");
                var id=p.AddSourceShape("Front",0,0,200,120);p.Save();
                using var viewport=new PanelViewport(p);viewport.RestoreViewState(new(1,30,30,false));
                string? selected=null,error=null;viewport.SourceRegionSelected+=v=>selected=v;viewport.SelectedSourceRegion=()=>selected;viewport.CommandFailed+=v=>error=v;
                var window=new Window{Content=viewport,Width=600,Height=400};window.Show();window.UpdateLayout();Dispatcher.UIThread.RunJobs();
                try
                {
                    window.MouseDown(new Point(30,30),MouseButton.Left);window.MouseMove(new Point(40,45));window.MouseUp(new Point(40,45),MouseButton.Left);
                    Assert.Equal(id,selected);Assert.Equal(new PointSnapshot(10,15),p.SourceShapes.Single().Points[0]);Assert.True(p.IsDirty);
                    p.Undo();Assert.False(p.IsDirty);Assert.Equal(new PointSnapshot(0,0),p.SourceShapes.Single().Points[0]);p.Redo();
                    window.MouseDown(new Point(40,45),MouseButton.Left);window.MouseMove(new Point(300,240));window.MouseUp(new Point(300,240),MouseButton.Left);
                    Assert.NotNull(error);Assert.Equal(new PointSnapshot(10,15),p.SourceShapes.Single().Points[0]);
                    p.Undo();Assert.False(p.IsDirty);
                }
                finally{window.Close();}
            }
            finally{Directory.Delete(root,true);}
        });
    }
}
