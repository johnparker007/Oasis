using System.Windows;

namespace OasisEditor;

public interface IApplicationThemeService
{
    void EnsureFluentThemeResources(Application application);
    void ApplyTheme(Application application, ThemePreference preference);
}
