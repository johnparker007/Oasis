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

    private void OnColorPickerLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is not FrameworkElement element
            || element.DataContext is not InspectorColorPropertyViewModel { CommitMode: InspectorColorCommitMode.Deferred } row)
        {
            return;
        }

        // Popup children retain the row DataContext. Moving between picker controls is still
        // one edit; commit only when keyboard focus actually leaves that row's picker surface.
        if (e.NewFocus is FrameworkElement next && ReferenceEquals(next.DataContext, row)) return;
        row.Commit();
    }

    private void OnLampTestMouseDown(object sender, MouseButtonEventArgs e)
    {
        SetLampTestActive(true);
    }

    private void OnLampTestMouseUp(object sender, MouseButtonEventArgs e)
    {
        SetLampTestActive(false);
    }

    private void OnLampTestMouseLeave(object sender, MouseEventArgs e)
    {
        SetLampTestActive(false);
    }

    private void SetLampTestActive(bool isActive)
    {
        switch (DataContext)
        {
            case InspectorViewModel inspectorViewModel:
                inspectorViewModel.SetLampTestActive(isActive);
                break;
            case MainWindowViewModel mainWindowViewModel:
                mainWindowViewModel.SetLampTestActive(isActive);
                break;
        }
    }
}
