using System.Windows.Controls;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace OasisEditor.Views;

public partial class HierarchyView : UserControl
{
    public HierarchyView()
    {
        InitializeComponent();
    }

    private void OnTreeViewSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> eventArgs)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        viewModel.SelectHierarchyItem(eventArgs.NewValue as HierarchyItemViewModel);
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

        treeViewItem.IsSelected = true;
        viewModel.SelectHierarchyItemForContextMenu(hierarchyItem);
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
