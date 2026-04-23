using System.Windows;

namespace OasisEditor;

public partial class MainWindow : Window
{
    public MainWindow(IApplicationThemeService applicationThemeService, EditorPreferencesStore preferencesStore)
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel(applicationThemeService, preferencesStore, this);
    }
}
