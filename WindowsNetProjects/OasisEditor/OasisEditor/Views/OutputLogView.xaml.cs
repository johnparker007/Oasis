using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace OasisEditor.Views;

public partial class OutputLogView : UserControl
{
    public OutputLogView()
    {
        InitializeComponent();
        CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy, OnCopySelectionExecuted));
        InputBindings.Add(new KeyBinding(ApplicationCommands.Copy, Key.C, ModifierKeys.Control));
    }

    private OutputLogViewModel? ViewModel => DataContext as OutputLogViewModel;

    private void OnOutputSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        ViewModel.UpdateSelectedEntries(OutputList.SelectedItems.Cast<OutputLogEntry>());
    }

    private void OnCopySelectionExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        CopySelection();
    }

    private void OnCopySelectionClicked(object sender, RoutedEventArgs e)
    {
        CopySelection();
    }

    private void OnOpenLogClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        if (!ViewModel.TryOpenCurrentLog(out var failureReason) && !string.IsNullOrWhiteSpace(failureReason))
        {
            ViewModel.AddOutputEntry(failureReason, OutputLogStatus.Warning);
        }
    }

    private void OnShowLogInExplorerClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        if (!ViewModel.TryShowLogInExplorer(out var failureReason) && !string.IsNullOrWhiteSpace(failureReason))
        {
            ViewModel.AddOutputEntry(failureReason, OutputLogStatus.Warning);
        }
    }

    private void CopySelection()
    {
        if (ViewModel is null)
        {
            return;
        }

        var text = ViewModel.BuildClipboardTextForSelection();
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        Clipboard.SetText(text);
    }
}
