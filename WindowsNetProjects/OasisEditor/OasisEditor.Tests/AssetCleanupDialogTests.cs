using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace OasisEditor.Avalonia.Tests;

[Collection("Avalonia headless UI")]
public sealed class AssetCleanupDialogTests
{
    private static AssetCleanupPlan Plan() => new("scan", "project.oasisproj", false,
        Enumerable.Range(0, 30).Select(i => new UnreferencedAsset($"Assets/FmlImport/Layout/image-{i:00}.png", 2048, "hash")).ToArray(), null);

    [Fact]
    public void FolderCheckboxesPropagateAndReflectMixedSelections()
    {
        var root = AssetCleanupDialog.BuildChoices(Plan());
        Assert.All(root.All(), n => Assert.True(n.IsChecked));
        var leaf = root.All().First(n => n.Candidate is not null); leaf.IsChecked = false;
        Assert.Null(root.IsChecked); Assert.Null(leaf.Parent!.IsChecked);
        leaf.Parent.IsChecked = false; Assert.All(leaf.Parent.All(), n => Assert.False(n.IsChecked));
        root.IsChecked = true; Assert.All(root.All(), n => Assert.True(n.IsChecked));
    }

    [Theory]
    [InlineData("Dark", false)]
    [InlineData("Light", false)]
    [InlineData("System", true)]
    public async Task ActualDialogHasScrollableSelectionAndCancel(string theme, bool cancel)
    {
        await HeadlessTestHost.RunAsync(async () =>
        {
            Application.Current!.RequestedThemeVariant = theme == "Dark" ? ThemeVariant.Dark : theme == "Light" ? ThemeVariant.Light : ThemeVariant.Default;
            var owner = new Window { Width = 1000, Height = 700 }; owner.Show();
            string[]? selected = null;
            var task = AssetCleanupDialog.ShowAsync(owner, Plan(), paths => { selected = paths; return new(paths, []); });
            var dialog = owner.OwnedWindows.Single(); dialog.UpdateLayout(); Dispatcher.UIThread.RunJobs();
            var checks = dialog.GetLogicalDescendants().OfType<CheckBox>().ToArray();
            Assert.True(checks.Length >= 30); Assert.All(checks, c => Assert.True(c.IsChecked));
            checks.Single(c => Equals(c.Content, "image-00.png  (2,048 bytes)")).IsChecked = false;
            var tree = dialog.GetLogicalDescendants().OfType<TreeView>().Single();
            var scroll = tree.GetVisualDescendants().OfType<ScrollViewer>().First();
            Assert.True(scroll.Extent.Height > scroll.Viewport.Height);
            var confirm = dialog.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Delete Selected"));
            Assert.False(confirm.IsDefault); Assert.True(confirm.IsEnabled);
            var output = Environment.GetEnvironmentVariable("OASIS_CLEANUP_VERIFICATION_OUTPUT");
            if (output is not null)
            {
                Directory.CreateDirectory(output); AvaloniaHeadlessPlatform.ForceRenderTimerTick(2);
                using var image = new RenderTargetBitmap(new PixelSize(760, 560), new Vector(96, 96)); image.Render(dialog);
                image.Save(Path.Combine(output, $"cleanup-{theme.ToLowerInvariant()}.png"));
            }
            if (cancel) dialog.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, "Escape");
            else confirm.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await task;
            if (cancel) Assert.Null(selected);
            else { Assert.Equal(29, selected!.Length); Assert.DoesNotContain("Assets/FmlImport/Layout/image-00.png", selected); }
            owner.Close();
        });
    }

    [Fact]
    public async Task FailedDeletionCannotBeRetriedByTogglingSelection()
    {
        await HeadlessTestHost.RunAsync(async () =>
        {
            var owner = new Window(); owner.Show();
            var task = AssetCleanupDialog.ShowAsync(owner, Plan(), _ => throw new InvalidOperationException("Scan again"));
            var dialog = owner.OwnedWindows.Single(); dialog.UpdateLayout();
            var confirm = dialog.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Delete Selected"));
            confirm.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Assert.False(confirm.IsEnabled);
            dialog.GetLogicalDescendants().OfType<CheckBox>().First().IsChecked = false;
            Assert.False(confirm.IsEnabled);
            Assert.Contains(dialog.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "Scan again");
            dialog.Close(); await task; owner.Close();
        });
    }
}
