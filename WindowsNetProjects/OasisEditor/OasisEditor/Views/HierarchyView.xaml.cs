using System.Windows.Controls;
using System.Windows;
using System.Windows.Input;

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
        if (eventArgs.Key != Key.Delete)
        {
            return;
        }

        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        if (viewModel.DeleteSelectedHierarchyItem())
        {
            eventArgs.Handled = true;
        }
    }
}
