using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

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

    public void ApplyTheme(Application application, ThemePreference preference)
    {
        ArgumentNullException.ThrowIfNull(application);

        var effectiveTheme = ResolveEffectiveTheme(preference);
        var palette = effectiveTheme == ThemePreference.Dark ? BuildDarkPalette() : BuildLightPalette();

        foreach (var (key, color) in palette)
        {
            application.Resources[key] = new SolidColorBrush(color);
        }
    }

    private static ThemePreference ResolveEffectiveTheme(ThemePreference preference)
    {
        if (preference is not ThemePreference.System)
        {
            return preference;
        }

        const string personalizeKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
        const string appsUseLightTheme = "AppsUseLightTheme";

        try
        {
            var value = Registry.GetValue($@"HKEY_CURRENT_USER\{personalizeKeyPath}", appsUseLightTheme, 1);
            var isLight = value is int intValue ? intValue != 0 : true;
            return isLight ? ThemePreference.Light : ThemePreference.Dark;
        }
        catch
        {
            return ThemePreference.Light;
        }
    }

    private static Dictionary<string, Color> BuildLightPalette()
    {
        return new Dictionary<string, Color>
        {
            ["EditorBackgroundBrush"] = (Color)ColorConverter.ConvertFromString("#FFF4F7FC"),
            ["PanelBackgroundBrush"] = Colors.White,
            ["InspectorBackgroundBrush"] = (Color)ColorConverter.ConvertFromString("#FFF9FAFC"),
            ["ToolBarBackgroundBrush"] = (Color)ColorConverter.ConvertFromString("#FFF2F5FB"),
            ["ControlBackgroundBrush"] = Colors.White,
            ["ControlForegroundBrush"] = (Color)ColorConverter.ConvertFromString("#FF20242C"),
            ["ControlBorderBrush"] = (Color)ColorConverter.ConvertFromString("#FFCED7E6"),
            ["ControlHoverBrush"] = (Color)ColorConverter.ConvertFromString("#FFEAF0FA"),
            ["ControlPressedBrush"] = (Color)ColorConverter.ConvertFromString("#FFDCE6F7"),
            ["MenuBackgroundBrush"] = Colors.White,
            ["MenuForegroundBrush"] = (Color)ColorConverter.ConvertFromString("#FF20242C"),
            ["TextPrimaryBrush"] = (Color)ColorConverter.ConvertFromString("#FF20242C"),
            ["TextSecondaryBrush"] = (Color)ColorConverter.ConvertFromString("#FF626A78"),
            ["BorderSubtleBrush"] = (Color)ColorConverter.ConvertFromString("#FFD9E0EE"),
            ["SelectionBrush"] = (Color)ColorConverter.ConvertFromString("#FFCCE0FF")
        };
    }

    private static Dictionary<string, Color> BuildDarkPalette()
    {
        return new Dictionary<string, Color>
        {
            ["EditorBackgroundBrush"] = (Color)ColorConverter.ConvertFromString("#FF151A22"),
            ["PanelBackgroundBrush"] = (Color)ColorConverter.ConvertFromString("#FF1C2535"),
            ["InspectorBackgroundBrush"] = (Color)ColorConverter.ConvertFromString("#FF18202E"),
            ["ToolBarBackgroundBrush"] = (Color)ColorConverter.ConvertFromString("#FF202B3C"),
            ["ControlBackgroundBrush"] = (Color)ColorConverter.ConvertFromString("#FF253145"),
            ["ControlForegroundBrush"] = (Color)ColorConverter.ConvertFromString("#FFF6F8FD"),
            ["ControlBorderBrush"] = (Color)ColorConverter.ConvertFromString("#FF445873"),
            ["ControlHoverBrush"] = (Color)ColorConverter.ConvertFromString("#FF30405A"),
            ["ControlPressedBrush"] = (Color)ColorConverter.ConvertFromString("#FF3A4D6B"),
            ["MenuBackgroundBrush"] = (Color)ColorConverter.ConvertFromString("#FF1B2433"),
            ["MenuForegroundBrush"] = (Color)ColorConverter.ConvertFromString("#FFF6F8FD"),
            ["TextPrimaryBrush"] = (Color)ColorConverter.ConvertFromString("#FFF2F5FB"),
            ["TextSecondaryBrush"] = (Color)ColorConverter.ConvertFromString("#FFC2CBDA"),
            ["BorderSubtleBrush"] = (Color)ColorConverter.ConvertFromString("#FF334156"),
            ["SelectionBrush"] = (Color)ColorConverter.ConvertFromString("#FF2C4F7E")
        };
    }
}
