using System.Windows.Controls;
using System.Windows;
using System.Windows.Input;
using Microsoft.VisualBasic;

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
            if (viewModel.DeleteSelectedHierarchyItem())
            {
                eventArgs.Handled = true;
            }

            return;
        }

        if (eventArgs.Key != Key.F2)
        {
            return;
        }

        if (!viewModel.TryGetSelectedHierarchyItemName(out var currentName))
        {
            return;
        }

        var renamed = Interaction.InputBox(
            "Enter a new name for the selected hierarchy object:",
            "Rename Hierarchy Item",
            currentName);
        if (string.IsNullOrWhiteSpace(renamed))
        {
            return;
        }

        if (viewModel.RenameSelectedHierarchyItem(renamed))
        {
            eventArgs.Handled = true;
        }
    }
}
