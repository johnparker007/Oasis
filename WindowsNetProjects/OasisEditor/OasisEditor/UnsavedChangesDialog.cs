using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace OasisEditor.Avalonia;

public enum UnsavedChangesChoice
{
    Cancel,
    Save,
    Discard
}

public sealed record UnsavedAssetInfo(string Id, string Title, string Kind, string DisplayPath, bool IsNew);

public sealed record UnsavedChangesRequest(string Operation, IReadOnlyList<UnsavedAssetInfo> Assets);

/// <summary>One shared confirmation dialog for operations that would lose unsaved assets.</summary>
public static class UnsavedChangesDialog
{
    public static async Task<UnsavedChangesChoice> ShowAsync(
        Window owner,
        UnsavedChangesRequest request,
        Action<Window>? opened = null)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(request);

        var assets = request.Assets ?? Array.Empty<UnsavedAssetInfo>();
        var isMultiple = assets.Count > 1;
        var hasNewAsset = assets.Any(asset => asset.IsNew);
        var result = UnsavedChangesChoice.Cancel;

        var window = new Window
        {
            Title = "Save changes?",
            Width = 520,
            MinWidth = 440,
            SizeToContent = SizeToContent.Height,
            CanResize = true,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        window.Name = "UnsavedChangesDialog";
        AutomationProperties.SetName(window, "UnsavedChangesDialog");

        var root = new StackPanel { Margin = new Thickness(20), Spacing = 14 };
        root.Children.Add(new TextBlock
        {
            Text = $"Save changes before {request.Operation}?",
            FontSize = 14,
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap
        });
        var context = new TextBlock
        {
            Text = isMultiple
                ? "The following assets have unsaved changes."
                : "This asset has unsaved changes.",
            TextWrapping = TextWrapping.Wrap
        };
        context.Bind(TextBlock.ForegroundProperty, context.GetResourceObservable("MutedBrush"));
        root.Children.Add(context);

        var list = new StackPanel { Spacing = 7 };
        foreach (var asset in assets)
        {
            var details = new StackPanel { Spacing = 3 };
            details.Children.Add(new TextBlock
            {
                Text = $"{asset.Kind}  ·  {asset.Title}{(asset.IsNew ? " (new)" : string.Empty)}",
                FontWeight = FontWeight.SemiBold,
                TextWrapping = TextWrapping.Wrap
            });
            var path = new TextBlock
            {
                Text = asset.DisplayPath,
                TextWrapping = TextWrapping.Wrap
            };
            path.Bind(TextBlock.ForegroundProperty, path.GetResourceObservable("MutedBrush"));
            details.Children.Add(path);
            var card = new Border
            {
                Padding = new Thickness(9, 7),
                BorderThickness = new Thickness(1),
                Child = details
            };
            card.Bind(Border.BackgroundProperty, card.GetResourceObservable("PanelBrush"));
            card.Bind(Border.BorderBrushProperty, card.GetResourceObservable("DividerBrush"));
            list.Children.Add(card);
        }

        var listScroller = new ScrollViewer
        {
            Content = list,
            MaxHeight = 260,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        root.Children.Add(listScroller);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };
        var cancel = MakeButton("Cancel", "UnsavedChangesCancel", () => Finish(UnsavedChangesChoice.Cancel));
        var discardLabel = isMultiple ? "Discard All Changes" : hasNewAsset ? "Don't Save" : "Discard Changes";
        var discard = MakeButton(discardLabel, "UnsavedChangesDiscard", () => Finish(UnsavedChangesChoice.Discard));
        var saveLabel = isMultiple ? "Save All" : hasNewAsset ? "Save…" : "Save";
        var save = MakeButton(saveLabel, "UnsavedChangesSave", () => Finish(UnsavedChangesChoice.Save));
        actions.Children.Add(cancel);
        actions.Children.Add(discard);
        actions.Children.Add(save);
        root.Children.Add(actions);
        window.Content = root;

        void Finish(UnsavedChangesChoice choice)
        {
            result = choice;
            window.Close();
        }

        window.KeyDown += (_, args) =>
        {
            if (args.Key != Key.Escape) return;
            args.Handled = true;
            Finish(UnsavedChangesChoice.Cancel);
        };
        window.Opened += (_, _) => cancel.Focus();

        var dialogTask = window.ShowDialog(owner);
        opened?.Invoke(window);
        await dialogTask;
        return result;
    }

    private static Button MakeButton(string text, string automationName, Action action)
    {
        var button = new Button { Content = text, MinWidth = 78 };
        button.Name = automationName;
        AutomationProperties.SetName(button, automationName);
        button.Click += (_, _) => action();
        return button;
    }
}
