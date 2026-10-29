using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Xunit;

namespace OasisEditor.Avalonia.Tests;

[Collection("Avalonia headless UI")]
public sealed class HierarchyUndoRoutingTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task DeleteCanBeUndoneAndRedoneWithoutRefocusingHierarchy(bool floating, bool contextMenu)
    {
        await HeadlessTestHost.RunAsync(async () =>
        {
            var root = Path.Combine(Path.GetTempPath(), "OasisHierarchyUndo-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var author = new AuthoringSession(root); author.CreateProject("Undo"); author.CreatePanel("Panel");
                var lamp = author.AddSpecialistElement("lamp", 20, 20); author.Save();
                using var capture = new WorkspaceCapture(author);
                await capture.View.SynchronizeAssetDocumentsAsync(); capture.Capture();
                var session = capture.View.CommandSession;
                if (floating) { capture.View.MoveWorkspacePane("Hierarchy", "float"); capture.Capture(); capture.CaptureFloatingWindows(); }
                var host = floating ? Assert.Single(capture.View.FloatingWorkspaceWindows) : (Window)TopLevel.GetTopLevel(capture.View)!;
                var row = host.GetVisualDescendants().OfType<StackPanel>().Single(p => Equals(p.Tag, lamp));
                var point = row.TranslatePoint(new Point(35, row.Bounds.Height / 2), host)!.Value;
                host.MouseDown(point, MouseButton.Left); host.MouseUp(point, MouseButton.Left);
                void Press(Key key, RawInputModifiers modifiers = RawInputModifiers.None)
                {
                    var physical = Enum.Parse<PhysicalKey>(key.ToString());
                    host.KeyPress(key, modifiers, physical, key.ToString()); host.KeyRelease(key, modifiers, physical, key.ToString());
                }
                if (contextMenu)
                {
                    row.ContextMenu!.Open(row);
                    row.ContextMenu.Items.OfType<MenuItem>().Single(i => Equals(i.Header, "Delete")).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                    row.ContextMenu.Close();
                }
                else Press(Key.Delete);
                Assert.Empty(session.Elements);
                Press(Key.Z, RawInputModifiers.Control);
                Assert.Equal(lamp, Assert.Single(session.Elements).Id);
                Press(Key.Z, RawInputModifiers.Control | RawInputModifiers.Shift);
                Assert.Empty(session.Elements);
                // Also cover the host retaining focus after a destroyed menu/row.
                host.Focusable = true; Assert.True(host.Focus());
                Press(Key.Z, RawInputModifiers.Control);
                Assert.Equal(lamp, Assert.Single(session.Elements).Id);
            }
            finally { Directory.Delete(root, true); }
        });
    }
}
