using System.Windows;

namespace OasisEditor;

public partial class HierarchyRenameDialog : Window
{
    public HierarchyRenameDialog(string currentName)
    {
        NameText = currentName;
        InitializeComponent();
        DataContext = this;
        Loaded += OnLoaded;
    }

    public string NameText { get; set; }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        NameTextBox.Focus();
        NameTextBox.SelectAll();
    }

    private void OnRenameClicked(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameText))
        {
            return;
        }

        DialogResult = true;
    }
}
