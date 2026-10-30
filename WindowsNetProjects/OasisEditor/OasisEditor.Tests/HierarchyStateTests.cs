using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using Xunit;

namespace OasisEditor.Avalonia.Tests;

[Collection("Avalonia headless UI")]
public sealed class HierarchyStateTests
{
    [Theory]
    [InlineData("Dark")]
    [InlineData("Light")]
    [InlineData("System")]
    public async Task FixedColumnsToggleLocalStatesWithoutSelectingAndPreserveInheritedRestrictions(string theme)
    {
        await HeadlessTestHost.RunAsync(async () =>
        {
            var root = Path.Combine(Path.GetTempPath(), "OasisHierarchyState-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var author = new AuthoringSession(root); author.CreateProject("States"); var path = author.CreatePanel("Panel");
                var child = author.AddRectangle(); var group = author.CreateGroup("Controls");
                var other = author.AddRectangle(); author.ReparentSelection(null); author.Save();
                using var capture = new WorkspaceCapture(author); await capture.View.SynchronizeAssetDocumentsAsync();
                var editor = capture.View.CommandView; var session = editor.CommandSession;
                editor.SetPanelGroupExpanded(group, true); session.Select(other); capture.Capture(theme: theme);
                var owner = (Window)TopLevel.GetTopLevel(capture.View)!;
                StackPanel Row(string id) => owner.GetVisualDescendants().OfType<StackPanel>().Single(r => Equals(r.Tag, id));
                Button State(string id, bool visible) => Row(id).Children.OfType<Button>().Single(b => b.Name == (visible ? "HierarchyVisibility" : "HierarchyLock"));
                void Click(string id, bool visible)
                {
                    owner.UpdateLayout(); var b = State(id, visible);
                    var p = b.TranslatePoint(new Point(10, 10), owner)!.Value;
                    owner.MouseDown(p, MouseButton.Left); owner.MouseUp(p, MouseButton.Left);
                    capture.Capture(theme: theme);
                    Assert.Equal(new[] { other }, session.SelectedIds);
                }
                foreach (var visible in new[] { true, false })
                {
                    Assert.Equal(State(group, visible).TranslatePoint(default, owner)!.Value.X, State(child, visible).TranslatePoint(default, owner)!.Value.X);
                    var idle = State(child, visible); Assert.Contains("inactive", idle.Classes);
                    owner.MouseMove(new Point(1390, 890)); Assert.Equal(0, ((EditorIcon)idle.Content!).Opacity);
                    owner.MouseMove(idle.TranslatePoint(new Point(10, 10), owner)!.Value);
                    Assert.Equal(.4, ((EditorIcon)idle.Content!).Opacity);
                    Click(group, visible);
                    Assert.Contains("inherited", State(child, visible).Classes);
                    Assert.Equal(.55, ((EditorIcon)State(child, visible).Content!).Opacity);
                    Click(child, visible);
                    var e = session.Elements.Single(e => e.Id == child);
                    Assert.True(visible ? !e.Visible : e.Locked);
                    session.Undo(); capture.Capture(theme: theme);
                    Assert.Contains("inherited", State(child, visible).Classes);
                    session.Redo(); capture.Capture(theme: theme);
                    Click(child, visible); // Local restore cannot remove the parent's restriction.
                    Assert.Contains("inherited", State(child, visible).Classes);
                    Click(group, visible);
                    Assert.Contains("inactive", State(child, visible).Classes);
                }
                Click(child, true); Click(child, false); session.Save();
                var reopened = new AuthoringSession(root); reopened.OpenProject(session.ProjectFilePath!); reopened.OpenPanel(path);
                Assert.False(reopened.Elements.Single(e => e.Id == child).Visible);
                Assert.True(reopened.Elements.Single(e => e.Id == child).Locked);
                var output = Environment.GetEnvironmentVariable("OASIS_HIERARCHY_STATE_OUTPUT");
                if (output is not null) File.WriteAllBytes(Path.Combine(output, $"hierarchy-states-{theme}.png"), capture.Capture(theme: theme));
            }
            finally { Directory.Delete(root, true); }
        });
    }

    [Fact]
    public void FaceRowsUseTheSameUndoableControlsAndPreserveSelection()
    {
        HeadlessTestHost.Run(() =>
        {
            var root = Path.Combine(Path.GetTempPath(), "OasisFaceStates-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            try
            {
                var author = new AuthoringSession(root); author.CreateProject("Face");
                using var capture = new WorkspaceCapture(author); var view = capture.View;
                view.Faces.CreateFace("Front"); view.ShowFace();
                var id = view.Faces.AddLamp("Lamp", 0, 0, 20, 20); view.Faces.Select(null); capture.Capture();
                var owner = (Window)TopLevel.GetTopLevel(view)!;
                foreach (var name in new[] { "HierarchyVisibility", "HierarchyLock" })
                {
                    var row = owner.GetVisualDescendants().OfType<StackPanel>().Single(r => Equals(r.Tag, id));
                    var b = row.Children.OfType<Button>().Single(b => b.Name == name);
                    var p = b.TranslatePoint(new Point(10, 10), owner)!.Value;
                    owner.MouseDown(p, MouseButton.Left); owner.MouseUp(p, MouseButton.Left);
                    Assert.Null(view.Faces.SelectedId);
                    var e = view.Faces.Elements.Single(e => e.Id == id);
                    Assert.True(name == "HierarchyLock" ? e.Locked : !e.Visible);
                    view.Faces.Undo(); e = view.Faces.Elements.Single(e => e.Id == id);
                    Assert.True(name == "HierarchyLock" ? !e.Locked : e.Visible);
                    capture.Capture();
                }
            }
            finally { Directory.Delete(root, true); }
        });
    }

    [Fact]
    public void ApprovedHiddenArtworkRetainsItsThreeRoundedStrokes()
    {
        HeadlessTestHost.Run(() =>
        {
            var drawing = new DrawingGroup();
            using (var context = drawing.Open()) EditorIcons.Draw(context, EditorIconKind.Hidden, Brushes.White);
            var shapes = drawing.Children.OfType<GeometryDrawing>().ToArray(); Assert.Equal(3, shapes.Length);
            Assert.Equal(new Rect(2, 2, 12, 12), shapes[2].Geometry!.Bounds);
            Assert.All(shapes, s => { Assert.Null(s.Brush); Assert.Equal(1.25, s.Pen!.Thickness); Assert.Equal(PenLineCap.Round, s.Pen.LineCap); Assert.Equal(PenLineJoin.Round, s.Pen.LineJoin); });
        });
    }
}
