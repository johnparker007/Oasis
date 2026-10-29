using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace OasisEditor.Avalonia.Tests;

[Collection("Avalonia headless UI")]
public sealed class HierarchyEditingTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InlineRenameConfirmsCancelsAndSharesUndoHistory(bool group)
    {
        await HeadlessTestHost.RunAsync(async () =>
        {
            var root = Path.Combine(Path.GetTempPath(), "OasisHierarchyEdit-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            try
            {
                var author = new AuthoringSession(root); author.CreateProject("Edit"); author.CreatePanel("Panel");
                var id = author.AddSpecialistElement("lamp", 20, 20);
                if (group) id = author.CreateGroup();
                author.Save();
                using var capture = new WorkspaceCapture(author); await capture.View.SynchronizeAssetDocumentsAsync(); capture.Capture();
                var session = capture.View.CommandSession; var window = (Window)TopLevel.GetTopLevel(capture.View)!;
                StackPanel Row() => window.GetVisualDescendants().OfType<StackPanel>().Single(p => Equals(p.Tag, id));
                var hierarchy = Row().FindAncestorOfType<ListBox>()!;
                session.Select(id); hierarchy.Focusable = true; hierarchy.Focus();
                void Press(Key key, PhysicalKey physical)
                { window.KeyPress(key, RawInputModifiers.None, physical, key.ToString()); window.KeyRelease(key, RawInputModifiers.None, physical, key.ToString()); Dispatcher.UIThread.RunJobs(); }
                Press(Key.F2, PhysicalKey.F2);
                var rename = window.GetVisualDescendants().OfType<TextBox>().Single(t => t.Name == "HierarchyRename");
                rename.Text = "Gamble ladder"; Press(Key.Enter, PhysicalKey.Enter);
                Assert.Equal("Gamble ladder", group ? session.Groups.Single().Name : session.Elements.Single(e => e.Id == id).Name);
                Assert.True(session.IsDirty); session.Undo(); Assert.False(session.IsDirty); session.Redo();
                capture.Capture(); Row().ContextMenu!.Open(Row());
                Row().ContextMenu!.Items.OfType<MenuItem>().Single(m => Equals(m.Header, "Rename")).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                Row().ContextMenu!.Close(); Dispatcher.UIThread.RunJobs();
                rename = window.GetVisualDescendants().OfType<TextBox>().Single(t => t.Name == "HierarchyRename");
                rename.Text = "Cancelled"; Press(Key.Escape, PhysicalKey.Escape);
                Assert.Equal("Gamble ladder", group ? session.Groups.Single().Name : session.Elements.Single(e => e.Id == id).Name);
                session.Undo(); Assert.False(session.IsDirty);
                if (group) Assert.True(session.Groups.Single().AutomaticName);
            }
            finally { Directory.Delete(root, true); }
        });
    }

    [Fact]
    public async Task EmptyAreaCreatesRootGroupsAndAcceptsReparentDrops()
    {
        await HeadlessTestHost.RunAsync(async () =>
        {
            var root = Path.Combine(Path.GetTempPath(), "OasisHierarchyRoot-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            try
            {
                var author = new AuthoringSession(root); author.CreateProject("Root"); author.CreatePanel("Panel");
                var lamp = author.AddRectangle(); var group = author.CreateGroup("Parent"); author.UpdateGroup(group, x: 50, y: 70); author.Save();
                using var capture = new WorkspaceCapture(author); await capture.View.SynchronizeAssetDocumentsAsync();
                var editor = capture.View.CommandView; var session = editor.CommandSession; editor.SetPanelGroupExpanded(group, true); capture.Capture();
                var window = (Window)TopLevel.GetTopLevel(capture.View)!;
                var hierarchy = window.GetVisualDescendants().OfType<StackPanel>().Single(p => Equals(p.Tag, lamp)).FindAncestorOfType<ListBox>()!;
                var empty = new Point(hierarchy.Bounds.Width / 2, hierarchy.Bounds.Height - 10);
                hierarchy.RaiseEvent(new ContextRequestedEventArgs());
                var menu = Assert.IsType<ContextMenu>(hierarchy.ContextMenu); Assert.True(menu.IsOpen);
                menu.Items.OfType<MenuItem>().Single(m => Equals(m.Header, "Create Group")).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); menu.Close();
                Assert.Null(session.Groups.Single(g => g.Id != group).ParentId);
                session.Undo();
                window.UpdateLayout();
                var parentRow = window.GetVisualDescendants().OfType<StackPanel>().Single(p => Equals(p.Tag, group));
                parentRow.ContextMenu!.Open(parentRow);
                parentRow.ContextMenu.Items.OfType<MenuItem>().Single(m => Equals(m.Header, "Create Group")).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); parentRow.ContextMenu.Close();
                var nested = session.Groups.Single(g => g.Id != group); Assert.Equal(group, nested.ParentId);
                var before = session.ViewportElements.Single(e => e.Id == lamp);
                using var data = new DataTransfer(); var item = new DataTransferItem(); item.Set(DataFormat.CreateInProcessFormat<WorkspaceView>("Oasis.PanelHierarchy"), editor); data.Add(item);
                hierarchy.RaiseEvent(new DragEventArgs(DragDrop.DropEvent, data, hierarchy, empty, KeyModifiers.None));
                Assert.Null(session.Groups.Single(g => g.Id == nested.Id).ParentId);
                session.Undo(); Assert.Equal(group, session.Groups.Single(g => g.Id == nested.Id).ParentId);
                session.Undo(); session.Select(lamp);
                var drop = new DragEventArgs(DragDrop.DropEvent, data, hierarchy, empty, KeyModifiers.None); hierarchy.RaiseEvent(drop);
                Assert.True(drop.Handled); Assert.Null(session.Elements.Single(e => e.Id == lamp).ParentId);
                var after = session.ViewportElements.Single(e => e.Id == lamp); Assert.Equal(before.X, after.X, 8); Assert.Equal(before.Y, after.Y, 8);
                session.Undo(); Assert.Equal(group, session.Elements.Single(e => e.Id == lamp).ParentId);
                session.Redo(); Assert.Null(session.Elements.Single(e => e.Id == lamp).ParentId);
            }
            finally { Directory.Delete(root, true); }
        });
    }
}
