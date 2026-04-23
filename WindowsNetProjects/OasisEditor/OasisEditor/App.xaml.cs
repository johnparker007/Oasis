using System.Windows;

namespace OasisEditor;

public partial class App : Application
{
    private readonly IApplicationThemeService _applicationThemeService = new ApplicationThemeService();

    protected override void OnStartup(StartupEventArgs e)
    {
        _applicationThemeService.EnsureFluentThemeResources(this);
        base.OnStartup(e);

        var mainWindow = new MainWindow();
        mainWindow.Show();
    }
}
