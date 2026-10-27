using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Xunit;

namespace OasisEditor.Avalonia.Tests;

[Collection("Avalonia headless UI")]
public sealed class WorkspaceViewTests
{
    [Fact]
    public void RealWorkspaceSelectionAndInspectorEditShareHistoryAndRenderInBothThemes()
    {
        HeadlessTestHost.Run(() =>
        {
        var folder = Path.Combine(Path.GetTempPath(), "OasisAvaloniaUi-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var project = Path.Combine(folder, "Ui.oasisproj");
            File.WriteAllText(project, """{"name":"UI test","version":9,"layout":{"assets":"Assets","generated":"Generated"}}""");
            var session = new AuthoringSession(folder);
            session.OpenProject(project); session.CreatePanel("Panel");
            var first = session.AddRectangle(); session.UpdateElement(first, name: "First");
            var second = session.AddRectangle(); session.UpdateElement(second, name: "Second", x: 180);
            session.Save();
            using var capture = new WorkspaceCapture(session);
            var dark = capture.Capture();
            var hierarchy = capture.View.GetVisualDescendants().OfType<ListBox>().Single(list => list.ItemsSource is IReadOnlyList<ElementSnapshot>);
            hierarchy.SelectedIndex = 0;
            Assert.Equal(first, session.SelectedId);
            capture.Capture();
            var name = capture.View.GetVisualDescendants().OfType<TextBox>().Single(box => box.Text == "First");
            name.Text = "Edited through inspector";
            var apply = capture.View.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "Apply"));
            apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal("Edited through inspector", session.Elements.Single(element => element.Id == first).Name);
            Assert.True(session.IsDirty);
            session.Undo(); Assert.False(session.IsDirty);
            var light = capture.Capture(theme: "Light");
            Assert.True(dark.Length > 5000);
            Assert.True(light.Length > 5000);
            Assert.NotEqual(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(dark)), Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(light)));
            Assert.Equal("First", session.Elements.Single(element => element.Id == first).Name);
            Assert.Equal(second, session.Elements[1].Id);
            session.AddSourceShape("Source region", 0, 0, 300, 200);
            capture.Capture();
            var sourceList = capture.View.GetVisualDescendants().OfType<ListBox>().Single(list => list.ItemsSource is IReadOnlyList<FaceSourceShapeSnapshot>);
            sourceList.SelectedIndex = 0;
            capture.Capture();
            Assert.Contains(capture.View.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "FACE SOURCE REGION");
            session.Save();
            session.UpdateElement(first, name: "Retained dirty panel");
            capture.View.Faces.CreateFace("Face"); capture.View.ShowFace();
            capture.View.OpenAsset(new AssetSummary("Panel2D", "Panel", Path.GetRelativePath(folder, session.PanelPath!).Replace('\\', '/')));
            capture.Capture();
            capture.View.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "Save")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.False(session.IsDirty);
            Assert.Equal("Retained dirty panel", session.Elements.Single(e => e.Id == first).Name);
            capture.View.ShowFace();
            var lamp = capture.View.Faces.AddLamp("Lamp", 24, 24, 120, 80, 7);
            capture.View.ShowPanel();
            capture.View.OpenAsset(new AssetSummary("Face", "Face", Path.GetRelativePath(folder, capture.View.Faces.FacePath!).Replace('\\', '/')));
            capture.Capture();
            var lampId = capture.View.GetVisualDescendants().OfType<TextBox>().Single(box => box.Text == "7");
            lampId.Text = "12";
            capture.View.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "Apply")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(12, capture.View.Faces.Elements.Single().LampId);
            Assert.True(capture.View.HasUnsavedChanges);
            Assert.Throws<InvalidOperationException>(() => capture.View.OpenProject(project));
            capture.View.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "Save")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.False(capture.View.HasUnsavedChanges);
            capture.View.Faces.UpdateElement(lamp, lampIndex: 19);
            capture.Capture();
            Assert.Contains(capture.View.GetVisualDescendants().OfType<TextBox>(), box => box.Text == "19");
            capture.View.Faces.Undo();
        }
        finally { Directory.Delete(folder, recursive: true); }
        });
    }
}
