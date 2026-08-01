using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace OasisEditor.Views;

public partial class InputMapView : UserControl
{
    public InputMapView()
    {
        InitializeComponent();
    }

    private void InputMapGrid_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.A && Keyboard.Modifiers == ModifierKeys.Control)
        {
            InputMapGrid.SelectAll();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Delete && Keyboard.Modifiers == ModifierKeys.None)
        {
            DeleteSelectedRows();
            e.Handled = true;
        }
    }

    private void InputMapGrid_OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var row = FindVisualParent<DataGridRow>(e.OriginalSource as DependencyObject);
        if (row is null || row.IsSelected)
        {
            return;
        }

        InputMapGrid.SelectedItems.Clear();
        row.IsSelected = true;
        InputMapGrid.CurrentItem = row.Item;
    }

    private void InputMapContextMenu_OnOpened(object sender, RoutedEventArgs e)
    {
        DeleteMenuItem.IsEnabled = InputMapGrid.SelectedItems.Count > 0;
    }

    private void DeleteMenuItem_OnClick(object sender, RoutedEventArgs e) => DeleteSelectedRows();

    private void DeleteSelectedRows()
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var selectedInputs = InputMapGrid.SelectedItems.OfType<InputDefinitionModel>().ToArray();
        if (selectedInputs.Length == 0)
        {
            return;
        }

        viewModel.DeleteInputDefinitions(selectedInputs);
        InputMapGrid.SelectedItems.Clear();
    }

    private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child is not null)
        {
            if (child is T parent)
            {
                return parent;
            }

            child = VisualTreeHelper.GetParent(child);
        }

        return null;
    }
}
