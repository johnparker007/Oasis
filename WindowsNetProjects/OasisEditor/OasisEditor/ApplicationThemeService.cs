using System;
using System.Linq;
using System.Windows;

namespace OasisEditor;

public sealed class ApplicationThemeService : IApplicationThemeService
{
    private static readonly Uri FluentThemeDictionaryUri = new(
        "pack://application:,,,/PresentationFramework.Fluent;component/Themes/Fluent.xaml",
        UriKind.Absolute);

    public void EnsureFluentThemeResources(Application application)
    {
        ArgumentNullException.ThrowIfNull(application);

        var dictionaries = application.Resources.MergedDictionaries;
        var hasFluentDictionary = dictionaries.Any(dictionary => FluentThemeDictionaryUri.Equals(dictionary.Source));
        if (hasFluentDictionary)
        {
            return;
        }

        dictionaries.Add(new ResourceDictionary
        {
            Source = FluentThemeDictionaryUri
        });
    }
}
