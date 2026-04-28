using System.Windows.Input;
using System.Windows.Controls;
using System.Windows;

namespace OasisEditor.Views;

public partial class InspectorView : UserControl
{
    public InspectorView()
    {
        InitializeComponent();
    }

    private void OnEditableColorControlIsKeyboardFocusWithinChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.DataContext is not InspectorEditablePropertyRowViewModel row)
        {
            return;
        }

        if (e.NewValue is bool hasFocusWithin && !hasFocusWithin)
        {
            row.Commit();
        }
    }

    private void OnEditableTextBoxLostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox textBox || textBox.DataContext is not InspectorEditablePropertyRowViewModel row)
        {
            return;
        }

        row.Commit();
    }

    private void OnEditableTextBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || sender is not TextBox textBox || textBox.DataContext is not InspectorEditablePropertyRowViewModel row)
        {
            return;
        }

        row.Commit();
        e.Handled = true;
    }
}
