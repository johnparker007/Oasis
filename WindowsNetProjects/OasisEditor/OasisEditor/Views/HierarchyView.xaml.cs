using System.Windows.Controls;
using System.Windows;

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
}
