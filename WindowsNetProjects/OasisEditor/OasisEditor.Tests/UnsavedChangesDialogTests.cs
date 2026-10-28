using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace OasisEditor.Avalonia.Tests;

[Collection("Avalonia headless UI")]
public sealed class UnsavedChangesDialogTests
{
    [Theory]
    [InlineData("Dark")]
    [InlineData("Light")]
    public async Task ShowsAssetDetailsAutomationNamesAndThemeResources(string theme)
    {
        await HeadlessTestHost.RunAsync(async () =>
        {
            var previousTheme = Application.Current!.RequestedThemeVariant;
            Application.Current.RequestedThemeVariant = theme == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
            var owner = new Window { Width = 800, Height = 600 };
            owner.Show();
            try
            {
                Window? dialog = null;
                var task = UnsavedChangesDialog.ShowAsync(owner,
                    new UnsavedChangesRequest("Close the project", new[]
                    {
                        new UnsavedAssetInfo("panel-1", "Main Panel", "Panel", "Assets/Panels/main.panel", false),
                        new UnsavedAssetInfo("face-2", "Front Face", "Face", "Assets/Faces/front.face", true)
                    }), shown => dialog = shown);

                Assert.NotNull(dialog);
                dialog!.UpdateLayout();
                Dispatcher.UIThread.RunJobs();
                Assert.Equal("UnsavedChangesDialog", dialog.Name);
                Assert.Contains(dialog.GetVisualDescendants().OfType<TextBlock>(), block => block.Text!.Contains("Close the project", StringComparison.Ordinal));
                Assert.Contains(dialog.GetVisualDescendants().OfType<TextBlock>(), block => block.Text!.Contains("Main Panel", StringComparison.Ordinal));
                Assert.Contains(dialog.GetVisualDescendants().OfType<TextBlock>(), block => block.Text == "Assets/Panels/main.panel");
                Assert.Contains(dialog.GetVisualDescendants().OfType<TextBlock>(), block => block.Text!.Contains("Front Face (new)", StringComparison.Ordinal));
                Assert.Contains(dialog.GetVisualDescendants().OfType<TextBlock>(), block => block.Text == "Assets/Faces/front.face");

                var buttons = dialog.GetVisualDescendants().OfType<Button>().ToArray();
                Assert.Equal("Cancel", buttons.Single(button => button.Name == "UnsavedChangesCancel").Content);
                Assert.Equal("Discard All Changes", buttons.Single(button => button.Name == "UnsavedChangesDiscard").Content);
                Assert.Equal("Save All", buttons.Single(button => button.Name == "UnsavedChangesSave").Content);
                Assert.All(buttons, button => Assert.Equal(button.Name, AutomationName(button)));
                var cards = dialog.GetVisualDescendants().OfType<Border>().Where(border => border.Child is StackPanel).ToArray();
                Assert.Equal(2, cards.Length);
                Assert.True(Application.Current.TryFindResource("PanelBrush", Application.Current.ActualThemeVariant, out var expectedPanelBrush));
                Assert.All(cards, border => Assert.Equal(expectedPanelBrush, border.Background));
                Assert.True(buttons.Single(button => button.Name == "UnsavedChangesCancel").IsFocused);

                buttons.Single(button => button.Name == "UnsavedChangesCancel").RaiseEvent(new global::Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                Assert.Equal(UnsavedChangesChoice.Cancel, await task);
            }
            finally
            {
                owner.Close();
                Application.Current.RequestedThemeVariant = previousTheme;
            }
        });
    }

    [Fact]
    public async Task NewSingleAssetOffersSaveEllipsis()
    {
        await HeadlessTestHost.RunAsync(async () =>
        {
            var owner = new Window();
            owner.Show();
            try
            {
                Window? dialog = null;
                var task = UnsavedChangesDialog.ShowAsync(owner,
                    new UnsavedChangesRequest("Close asset", new[]
                    {
                        new UnsavedAssetInfo("new-panel", "Untitled Panel", "Panel", "Assets/Panels/untitled.panel", true)
                    }), shown => dialog = shown);
                dialog!.UpdateLayout();
                Dispatcher.UIThread.RunJobs();
                var save = dialog.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "UnsavedChangesSave");
                Assert.Equal("Save…", save.Content);
                save.RaiseEvent(new global::Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                Assert.Equal(UnsavedChangesChoice.Save, await task);
            }
            finally { owner.Close(); }
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task EscapeAndWindowCloseBothReturnCancel(bool escape)
    {
        await HeadlessTestHost.RunAsync(async () =>
        {
            var owner = new Window();
            owner.Show();
            try
            {
                Window? dialog = null;
                var task = UnsavedChangesDialog.ShowAsync(owner,
                    new UnsavedChangesRequest("Close asset", new[]
                    {
                        new UnsavedAssetInfo("panel", "Main Panel", "Panel", "Assets/Panels/main.panel", false)
                    }), shown => dialog = shown);
                dialog!.UpdateLayout();
                Dispatcher.UIThread.RunJobs();
                if (escape)
                    dialog.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape });
                else
                    dialog.Close();

                Assert.Equal(UnsavedChangesChoice.Cancel, await task);
            }
            finally { owner.Close(); }
        });
    }

    private static string? AutomationName(Control control) =>
        global::Avalonia.Automation.AutomationProperties.GetName(control);
}
