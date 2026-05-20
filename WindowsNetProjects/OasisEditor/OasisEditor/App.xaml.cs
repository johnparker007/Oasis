using OasisEditor.Automation;
using System.Windows;

namespace OasisEditor;

public partial class App : Application
{
    private readonly IApplicationThemeService _applicationThemeService = new ApplicationThemeService();
    private readonly EditorPreferencesStore _preferencesStore = new();

    protected override async void OnStartup(StartupEventArgs e)
    {
        _applicationThemeService.EnsureFluentThemeResources(this);

        var preferences = _preferencesStore.Load();
        _applicationThemeService.ApplyTheme(this, preferences.ThemePreference);

        var parsed = HeadlessAutomationCli.Parse(e.Args);
        if (parsed.IsHeadless)
        {
            if (parsed.Options is null)
            {
                if (!string.IsNullOrWhiteSpace(parsed.ErrorMessage))
                {
                    Console.Error.WriteLine(parsed.ErrorMessage);
                }

                Shutdown((int)parsed.ErrorCode);
                return;
            }

            var exitCode = await HeadlessAutomationCli.RunAsync(parsed.Options).ConfigureAwait(true);
            Shutdown((int)exitCode);
            return;
        }

        base.OnStartup(e);

        var launcherWindow = new LauncherWindow(_applicationThemeService, _preferencesStore);
        launcherWindow.Show();
    }
}
