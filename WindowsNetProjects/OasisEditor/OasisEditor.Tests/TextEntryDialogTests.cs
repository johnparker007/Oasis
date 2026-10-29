using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Xunit;

namespace OasisEditor.Avalonia.Tests;

[Collection("Avalonia headless UI")]
public sealed class TextEntryDialogTests
{
    [Theory]
    [InlineData("Panel2D", "New Panel2D", true)]
    [InlineData("Face", "New Face", true)]
    [InlineData("Machine", "New Machine", true)]
    [InlineData("Reel", "Create Reel", true)]
    [InlineData("Prefab", "Create Prefab", true)]
    [InlineData("Panel2D", "New Panel2D", false)]
    public async Task CreateDialogsAcceptEnterAndEscapeAndRetainValidation(string kind, string title, bool accept)
    {
        await HeadlessTestHost.RunAsync(async () =>
        {
            var root = Path.Combine(Path.GetTempPath(), "OasisTextDialog-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            try
            {
                var session = new AuthoringSession(root); session.CreateProject("Dialogs");
                using var capture = new WorkspaceCapture(session); capture.Capture();
                var before = session.ListAssets().Count;
                capture.View.FindMenu("File", "Create", kind).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                var owner = Assert.IsType<Window>(TopLevel.GetTopLevel(capture.View));
                await Until(() => owner.OwnedWindows.Any(w => w.Title == title));
                var dialog = owner.OwnedWindows.Single(w => w.Title == title);
                var input = dialog.GetLogicalDescendants().OfType<TextBox>().Single();
                Assert.Contains(dialog.GetLogicalDescendants().OfType<Button>(), b => b.IsCancel && Equals(b.Content, "Cancel"));
                Press(dialog, Key.Enter); await Task.Delay(20); Assert.True(dialog.IsVisible); // Empty input must not create.
                input.Text = "Keyboard asset"; input.Focus();
                Press(dialog, accept ? Key.Enter : Key.Escape);
                await Until(() => !dialog.IsVisible);
                Assert.Equal(before + (accept ? 1 : 0), session.ListAssets().Count);
            }
            finally { Directory.Delete(root, true); }
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RenameAcceptsEnterOrCancelsWithoutChangingThePackage(bool accept)
    {
        await HeadlessTestHost.RunAsync(async () =>
        {
            var root = Path.Combine(Path.GetTempPath(), "OasisRenameKeys-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            try
            {
                var session = new AuthoringSession(root); session.CreateProject("Dialogs"); session.CreatePanel("Original"); session.CloseActiveDocument();
                using var capture = new WorkspaceCapture(session); capture.Capture();
                var asset = session.ListAssets().Single(a => a.Type == "Panel2D");
                var task = capture.View.RenameProjectAssetAsync(asset);
                var owner = Assert.IsType<Window>(TopLevel.GetTopLevel(capture.View));
                await Until(() => owner.OwnedWindows.Any(w => w.Title == "Rename asset package"));
                var dialog = owner.OwnedWindows.Single(w => w.Title == "Rename asset package");
                dialog.GetLogicalDescendants().OfType<TextBox>().Single().Text = "Renamed";
                Press(dialog, accept ? Key.Enter : Key.Escape); await task;
                var panel = session.ListAssets().Single(a => a.Type == "Panel2D");
                Assert.Equal(accept ? "Renamed" : "Original", Path.GetFileName(Path.GetDirectoryName(panel.ManifestPath)));
            }
            finally { Directory.Delete(root, true); }
        });
    }

    private static void Press(Window window, Key key)
    {
        var physical = key == Key.Enter ? PhysicalKey.Enter : PhysicalKey.Escape;
        window.KeyPress(key, RawInputModifiers.None, physical, key.ToString());
        if (window.IsVisible) window.KeyRelease(key, RawInputModifiers.None, physical, key.ToString());
    }
    private static async Task Until(Func<bool> condition)
    { for (var i = 0; i < 150 && !condition(); i++) await Task.Delay(10); Assert.True(condition()); }
}
