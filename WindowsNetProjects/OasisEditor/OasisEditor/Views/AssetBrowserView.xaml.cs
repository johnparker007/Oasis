using System.Windows.Controls;
using System.Windows.Input;
using System.Windows;
using System.Windows.Media;
using OasisEditor;

namespace OasisEditor.Views;

public partial class AssetBrowserView : UserControl
{
    public AssetBrowserView()
    {
        InitializeComponent();
    }

    private void OnAssetListMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var command = viewModel.OpenAssetCommand;
        var selectedAsset = viewModel.SelectedAsset;
        if (command.CanExecute(selectedAsset))
        {
            command.Execute(selectedAsset);
        }
    }

    private void OnAssetListPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs eventArgs)
    {
        if (sender is not ListBox listBox)
        {
            return;
        }

        if (eventArgs.OriginalSource is not DependencyObject source)
        {
            return;
        }

        var listBoxItem = FindAncestor<ListBoxItem>(source);
        if (listBoxItem is null)
        {
            return;
        }

        listBoxItem.IsSelected = true;
        listBox.Focus();
    }

    private void OnDirectoryTreeSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> eventArgs)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        if (eventArgs.NewValue is AssetDirectoryNodeViewModel directoryNode)
        {
            viewModel.SelectedAssetDirectory = directoryNode;
        }
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
