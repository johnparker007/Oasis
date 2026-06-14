using System.Windows;

namespace OasisEditor.Progress;

public partial class EditorProgressDialogWindow : Window
{
    public EditorProgressDialogWindow(EditorProgressDialogViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
