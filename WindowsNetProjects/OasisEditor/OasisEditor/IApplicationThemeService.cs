using System.Windows;

namespace OasisEditor;

public interface IApplicationThemeService
{
    void EnsureFluentThemeResources(Application application);
}
