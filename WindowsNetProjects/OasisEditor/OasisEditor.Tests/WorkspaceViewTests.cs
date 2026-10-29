using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Xunit;

namespace OasisEditor.Avalonia.Tests;

[Collection("Avalonia headless UI")]
public sealed class WorkspaceViewTests
{
    [Fact]
    public async Task RealWorkspaceSelectionAndInspectorEditShareHistoryAndRenderInBothThemes()
    {
        await HeadlessTestHost.RunAsync(async () =>
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
            var workspace = capture.View;
            await workspace.SynchronizeAssetDocumentsAsync();
            var panel = workspace.CommandSession;
            var dark = capture.Capture();
            var hierarchy = workspace.GetVisualDescendants().OfType<ListBox>().Single(list => list.ItemsSource is IReadOnlyList<ElementSnapshot>);
            hierarchy.SelectedIndex = 0;
            Assert.Equal(first, panel.SelectedId);
            capture.Capture();
            var name = workspace.GetVisualDescendants().OfType<TextBox>().Single(box => box.Text == "First");
            name.Text = "Edited through inspector";
            InspectorTestInput.Commit(name);
            Assert.Equal("Edited through inspector", panel.Elements.Single(element => element.Id == first).Name);
            Assert.True(panel.IsDirty);
            panel.Undo(); Assert.False(panel.IsDirty);
            var light = capture.Capture(theme: "Light");
            Assert.True(dark.Length > 5000);
            Assert.True(light.Length > 5000);
            Assert.NotEqual(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(dark)), Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(light)));
            Assert.Equal("First", panel.Elements.Single(element => element.Id == first).Name);
            Assert.Equal(second, panel.Elements[1].Id);
            panel.AddSourceShape("Source region", 0, 0, 300, 200);
            capture.Capture();
            var sourceList = workspace.GetVisualDescendants().OfType<ListBox>().Single(list => list.ItemsSource is IReadOnlyList<FaceSourceShapeSnapshot>);
            sourceList.SelectedIndex = 0;
            capture.Capture();
            Assert.Contains(workspace.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "FACE SOURCE REGION");
            panel.Save();
            panel.UpdateElement(first, name: "Retained dirty panel");
            var facePath = workspace.Faces.CreateFace("Face"); workspace.ShowFace();
            await workspace.SynchronizeAssetDocumentsAsync();
            await workspace.ActivateDocumentAsync("Panel2D", panel.PanelPath!);
            capture.Capture();
            workspace.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "Save")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.False(panel.IsDirty);
            Assert.Equal("Retained dirty panel", panel.Elements.Single(e => e.Id == first).Name);
            await workspace.ActivateDocumentAsync("Face", facePath);
            var faceEditor = workspace.CommandView;
            var faces = faceEditor.Faces;
            var lamp = faces.AddLamp("Lamp", 24, 24, 120, 80, 7);
            await workspace.SynchronizeAssetDocumentsAsync();
            await workspace.ActivateDocumentAsync("Face", facePath);
            capture.Capture();
            faceEditor = workspace.CommandView;
            faces = faceEditor.Faces;
            var lampId = workspace.GetVisualDescendants().OfType<TextBox>().Single(box => box.Text == "7");
            lampId.Text = "12";
            InspectorTestInput.Commit(lampId);
            Assert.Equal(12, faces.Elements.Single().LampId);
            Assert.True(workspace.HasUnsavedChanges);
            Assert.Throws<InvalidOperationException>(() => workspace.OpenProject(project));
            workspace.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "Save")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.False(workspace.HasUnsavedChanges);
            faces.UpdateElement(lamp, lampIndex: 19);
            capture.Capture();
            Assert.Contains(workspace.GetVisualDescendants().OfType<TextBox>(), box => box.Text == "19");
            faces.Undo();
        }
        finally { Directory.Delete(folder, recursive: true); }
        });
    }
}
