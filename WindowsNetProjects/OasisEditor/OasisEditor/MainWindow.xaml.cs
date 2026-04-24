using System.Windows;

namespace OasisEditor;

public partial class MainWindow : Window
{
    public MainWindow(
        IApplicationThemeService applicationThemeService,
        EditorPreferencesStore preferencesStore,
        string startupProjectFilePath)
    {
        if (string.IsNullOrWhiteSpace(startupProjectFilePath))
        {
            throw new InvalidOperationException("Editor shell requires an active loaded project.");
        }

        InitializeComponent();
        EditorKeyboardShortcuts.RegisterWindowBindings(this);
        DataContext = new MainWindowViewModel(applicationThemeService, preferencesStore, this, startupProjectFilePath);
    }
}
