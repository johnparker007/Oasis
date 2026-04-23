using System.Windows;

namespace OasisEditor;

public partial class App : Application
{
    private readonly IApplicationThemeService _applicationThemeService = new ApplicationThemeService();
    private readonly EditorPreferencesStore _preferencesStore = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        _applicationThemeService.EnsureFluentThemeResources(this);

        var preferences = _preferencesStore.Load();
        _applicationThemeService.ApplyTheme(this, preferences.ThemePreference);

        base.OnStartup(e);

        var mainWindow = new MainWindow(_applicationThemeService, _preferencesStore);
        mainWindow.Show();
    }
}
