using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace OasisEditor.Avalonia;

/// <summary>Shared keyboard/actions for single-name create and rename dialogs.</summary>
internal static class TextEntryDialog
{
    public static Control Actions(Window dialog, TextBox input, Button confirm)
    {
        confirm.IsDefault = true;
        var cancel = new Button { Content = "Cancel", IsCancel = true };
        cancel.Click += (_, _) => dialog.Close();
        dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        dialog.Opened += (_, _) => { input.Focus(); input.SelectAll(); };
        return new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 8,
            Children = { confirm, cancel }
        };
    }
}
