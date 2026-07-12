using System.Windows.Controls;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace OasisEditor.Views;

public partial class HierarchyView : UserControl
{
    private bool _suppressAutoScrollForUserTreeSelection;

    public HierarchyView()
    {
        InitializeComponent();
    }

    private void OnTreeViewSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> eventArgs)
    {
        // Native TreeView selection is focus/navigation only. DocumentSelectionState is updated by explicit mouse gestures.
    }

    private void OnTreeViewPreviewKeyDown(object sender, KeyEventArgs eventArgs)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        if (eventArgs.Key == Key.Delete)
        {
            var command = viewModel.DeleteSelectedHierarchyItemCommand;
            if (command.CanExecute(null))
            {
                command.Execute(null);
                eventArgs.Handled = true;
            }

            return;
        }

        if (eventArgs.Key != Key.F2)
        {
            return;
        }

        var renameCommand = viewModel.RenameSelectedHierarchyItemCommand;
        if (renameCommand.CanExecute(null))
        {
            renameCommand.Execute(null);
            eventArgs.Handled = true;
        }
    }

    private void OnTreeViewPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs eventArgs)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        if (eventArgs.OriginalSource is not DependencyObject source)
        {
            return;
        }

        var treeViewItem = FindAncestor<TreeViewItem>(source);
        if (treeViewItem?.DataContext is not HierarchyItemViewModel hierarchyItem)
        {
            return;
        }

        treeViewItem.Focus();
        viewModel.SelectHierarchyItemForContextMenu(hierarchyItem);
    }

    private void OnTreeViewPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs eventArgs)
    {
        if (eventArgs.OriginalSource is not DependencyObject source)
        {
            return;
        }

        var treeViewItem = FindAncestor<TreeViewItem>(source);
        if (treeViewItem?.DataContext is not HierarchyItemViewModel hierarchyItem)
        {
            return;
        }

        treeViewItem.Focus();
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.SelectHierarchyItem(hierarchyItem, GetSelectionModifier());
            eventArgs.Handled = hierarchyItem.SelectionItem is not null;
        }

        _suppressAutoScrollForUserTreeSelection = true;
        Dispatcher.BeginInvoke(
            DispatcherPriority.Input,
            new Action(() => _suppressAutoScrollForUserTreeSelection = false));
    }

    private static HierarchySelectionModifier GetSelectionModifier()
    {
        var modifiers = Keyboard.Modifiers;
        var ctrl = (modifiers & ModifierKeys.Control) == ModifierKeys.Control;
        var shift = (modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
        return (ctrl, shift) switch
        {
            (true, true) => HierarchySelectionModifier.ControlShift,
            (true, false) => HierarchySelectionModifier.Control,
            (false, true) => HierarchySelectionModifier.Shift,
            _ => HierarchySelectionModifier.None
        };
    }

    private void OnTreeViewItemSelected(object sender, RoutedEventArgs eventArgs)
    {
        if (sender is not TreeViewItem treeViewItem)
        {
            return;
        }

        if (!ReferenceEquals(sender, eventArgs.OriginalSource))
        {
            return;
        }

        if (_suppressAutoScrollForUserTreeSelection)
        {
            return;
        }

        Dispatcher.BeginInvoke(
            DispatcherPriority.Loaded,
            new Action(() => EnsureTreeViewItemIsVisible(treeViewItem)));
    }

    private static void EnsureTreeViewItemIsVisible(TreeViewItem treeViewItem)
    {
        if (!treeViewItem.IsSelected || !treeViewItem.IsVisible)
        {
            return;
        }

        if (IsVerticallyVisible(treeViewItem))
        {
            return;
        }

        treeViewItem.BringIntoView();
    }

    private static bool IsVerticallyVisible(TreeViewItem treeViewItem)
    {
        var scrollViewer = FindAncestor<ScrollViewer>(treeViewItem);
        if (scrollViewer is null || scrollViewer.ViewportHeight <= 0)
        {
            return false;
        }

        var bounds = treeViewItem.TransformToAncestor(scrollViewer)
            .TransformBounds(new Rect(new Point(0, 0), treeViewItem.RenderSize));

        return bounds.Top >= 0 && bounds.Bottom <= scrollViewer.ViewportHeight;
    }

    private static T? FindAncestor<T>(DependencyObject current)
        where T : DependencyObject
    {
        var node = current;
        while (node is not null)
        {
            if (node is T ancestor)
            {
                return ancestor;
            }

            node = VisualTreeHelper.GetParent(node);
        }

        return null;
    }
}
