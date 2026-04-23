using System.Windows;

namespace OasisEditor;

public partial class MainWindow : Window
{
    public MainWindow(IApplicationThemeService applicationThemeService, EditorPreferencesStore preferencesStore)
    {
        InitializeComponent();
        EditorKeyboardShortcuts.RegisterWindowBindings(this);
        DataContext = new MainWindowViewModel(applicationThemeService, preferencesStore, this);
    }
}
