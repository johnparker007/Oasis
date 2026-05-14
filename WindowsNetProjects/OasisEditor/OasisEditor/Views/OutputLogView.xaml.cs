using System.Linq;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace OasisEditor.Views;

public partial class OutputLogView : UserControl
{
    private INotifyCollectionChanged? _observedOutputEntries;

    public OutputLogView()
    {
        InitializeComponent();
        CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy, OnCopySelectionExecuted));
        InputBindings.Add(new KeyBinding(ApplicationCommands.Copy, Key.C, ModifierKeys.Control));
        DataContextChanged += OnDataContextChanged;
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

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_observedOutputEntries is not null)
        {
            _observedOutputEntries.CollectionChanged -= OnOutputEntriesCollectionChanged;
            _observedOutputEntries = null;
        }

        if (ViewModel?.OutputEntries is INotifyCollectionChanged entries)
        {
            _observedOutputEntries = entries;
            _observedOutputEntries.CollectionChanged += OnOutputEntriesCollectionChanged;
        }
    }

    private void OnOutputEntriesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Add || e.NewItems is null || e.NewItems.Count == 0)
        {
            return;
        }

        if (ViewModel?.AutoScroll != true)
        {
            return;
        }

        if (e.NewItems[e.NewItems.Count - 1] is OutputLogEntry entry)
        {
            OutputList.ScrollIntoView(entry);
        }
    }


    private void OnOutputListPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ListBox listBox)
        {
            return;
        }

        var source = e.OriginalSource as DependencyObject;
        var listBoxItem = ItemsControl.ContainerFromElement(listBox, source) as ListBoxItem;
        if (listBoxItem is null)
        {
            return;
        }

        if (listBoxItem.IsSelected)
        {
            return;
        }

        listBox.SelectedItems.Clear();
        listBoxItem.IsSelected = true;
        listBox.Focus();
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
