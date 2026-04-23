using System.Windows;

namespace OasisEditor;

public partial class LauncherWindow : Window
{
    public LauncherWindow(IApplicationThemeService applicationThemeService, EditorPreferencesStore preferencesStore)
    {
        InitializeComponent();
        DataContext = new LauncherWindowViewModel(applicationThemeService, preferencesStore, this);
    }
}
