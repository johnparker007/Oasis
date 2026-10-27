using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Xunit;

namespace OasisEditor.Avalonia.Tests;

public sealed class WorkspaceViewTests
{
    [Fact]
    public void RealWorkspaceSelectionAndInspectorEditShareHistoryAndRenderInBothThemes()
    {
        Program.BuildHeadless().SetupWithoutStarting();
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
        }
        finally { Directory.Delete(folder, recursive: true); }
    }
}
