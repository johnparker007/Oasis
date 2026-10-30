using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using Xunit;

namespace OasisEditor.Avalonia.Tests;

[Collection("Avalonia headless UI")]
public sealed class HierarchySelectionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ClicksAndArrowKeysRespectSelectionModifiers(bool grouped)
    {
        await HeadlessTestHost.RunAsync(async () =>
        {
            var root = Path.Combine(Path.GetTempPath(), "OasisHierarchySelection-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var author = new AuthoringSession(root);
                author.CreateProject("Selection");
                author.CreatePanel("Panel");
                var ids = Enumerable.Range(0, 4).Select(_ => author.AddRectangle()).ToArray();
                string? group = null;
                if (grouped)
                {
                    author.SelectElements(ids);
                    group = author.CreateGroup("Controls");
                }
                author.Save();
                using var capture = new WorkspaceCapture(author);
                await capture.View.SynchronizeAssetDocumentsAsync();
                var editor = capture.View.CommandView;
                var session = editor.CommandSession;
                if (group is not null) editor.SetPanelGroupExpanded(group, true);
                session.Select(ids[0]);
                capture.Capture();
                var window = (Window)TopLevel.GetTopLevel(capture.View)!;
                var hierarchy = window.GetVisualDescendants().OfType<StackPanel>()
                    .Single(p => Equals(p.Tag, ids[0])).FindAncestorOfType<ListBox>()!;
                var couldUndo = session.CanUndo;

                void AssertSelection(params string[] selected)
                {
                    Assert.Equal(selected.Order(), session.SelectedIds.Order());
                    Assert.Equal(selected.Order(), hierarchy.SelectedItems!.Cast<ElementSnapshot>().Select(e => e.Id).Order());
                    Assert.False(session.IsDirty);
                    Assert.Equal(couldUndo, session.CanUndo);
                    Assert.False(session.CanRedo);
                }
                void Click(int index, RawInputModifiers modifiers = RawInputModifiers.None)
                {
                    window.UpdateLayout();
                    var row = window.GetVisualDescendants().OfType<StackPanel>().Single(p => Equals(p.Tag, ids[index]));
                    var label = row.Children.OfType<ContentControl>().Single(c => c.Name == "HierarchyLabel");
                    var point = label.TranslatePoint(new Point(20, label.Bounds.Height / 2), window)!.Value;
                    window.MouseDown(point, MouseButton.Left, modifiers);
                    window.MouseUp(point, MouseButton.Left, modifiers);
                }
                void Arrow(Key key, PhysicalKey physical, RawInputModifiers modifiers = RawInputModifiers.None)
                {
                    window.KeyPress(key, modifiers, physical, key.ToString());
                    window.KeyRelease(key, modifiers, physical, key.ToString());
                }

                Click(0); AssertSelection(ids[0]);
                Click(1); AssertSelection(ids[1]);
                Click(3, RawInputModifiers.Control); AssertSelection(ids[1], ids[3]);
                Click(1); AssertSelection(ids[1]);
                Click(1); AssertSelection(ids[1]);
                Click(3, RawInputModifiers.Control); AssertSelection(ids[1], ids[3]);
                Click(1, RawInputModifiers.Control); AssertSelection(ids[3]);
                Click(0); AssertSelection(ids[0]);
                Click(3, RawInputModifiers.Shift); AssertSelection(ids);
                Click(1, RawInputModifiers.Shift); AssertSelection(ids[0], ids[1]);
                Click(1); AssertSelection(ids[1]);
                Arrow(Key.Down, PhysicalKey.ArrowDown); AssertSelection(ids[2]);
                Arrow(Key.Up, PhysicalKey.ArrowUp); AssertSelection(ids[1]);
                Arrow(Key.Down, PhysicalKey.ArrowDown, RawInputModifiers.Shift); AssertSelection(ids[1], ids[2]);
                Arrow(Key.Down, PhysicalKey.ArrowDown, RawInputModifiers.Shift); AssertSelection(ids[1], ids[2], ids[3]);
                Arrow(Key.Up, PhysicalKey.ArrowUp, RawInputModifiers.Shift); AssertSelection(ids[1], ids[2]);
                Arrow(Key.Up, PhysicalKey.ArrowUp); AssertSelection(ids[1]);
                window.UpdateLayout();
                var empty = hierarchy.TranslatePoint(new Point(hierarchy.Bounds.Width / 2, hierarchy.Bounds.Height - 12), window)!.Value;
                window.MouseDown(empty, MouseButton.Left);
                window.MouseUp(empty, MouseButton.Left);
                AssertSelection();
                Click(2); AssertSelection(ids[2]);
                window.MouseDown(empty, MouseButton.Right);
                window.MouseUp(empty, MouseButton.Right);
                AssertSelection(ids[2]);
            }
            finally { Directory.Delete(root, true); }
        });
    }
}
