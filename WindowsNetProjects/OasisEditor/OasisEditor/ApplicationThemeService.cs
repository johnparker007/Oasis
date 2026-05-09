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
        var palette = effectiveTheme == ThemePreference.Dark ? BuildScreenshotDarkPalette() : BuildLightPalette();

        foreach (var (key, color) in palette)
        {
            application.Resources[key] = new SolidColorBrush(color);
        }

        ApplyComboBoxPaletteResources(application, palette);
    }

    private static void ApplyComboBoxPaletteResources(Application application, IReadOnlyDictionary<string, Color> palette)
    {
        var panelBackground = palette["PanelBackgroundBrush"];
        var textPrimary = palette["TextPrimaryBrush"];
        var selection = palette["SelectionBrush"];
        var borderSubtle = palette["BorderSubtleBrush"];
        var borderStrong = palette["BorderStrongBrush"];
        var hover = palette["ControlHoverBrush"];
        var textMuted = palette["TextMutedBrush"];

        application.Resources[SystemColors.WindowBrushKey] = new SolidColorBrush(panelBackground);
        application.Resources[SystemColors.ControlBrushKey] = new SolidColorBrush(panelBackground);
        application.Resources[SystemColors.WindowTextBrushKey] = new SolidColorBrush(textPrimary);
        application.Resources[SystemColors.ControlTextBrushKey] = new SolidColorBrush(textPrimary);
        application.Resources[SystemColors.HighlightBrushKey] = new SolidColorBrush(selection);
        application.Resources[SystemColors.HighlightTextBrushKey] = new SolidColorBrush(textPrimary);
        application.Resources[SystemColors.InactiveSelectionHighlightBrushKey] = new SolidColorBrush(selection);
        application.Resources[SystemColors.InactiveSelectionHighlightTextBrushKey] = new SolidColorBrush(textPrimary);

        application.Resources["ComboBox.Static.Background"] = new SolidColorBrush(panelBackground);
        application.Resources["ComboBox.Static.Foreground"] = new SolidColorBrush(textPrimary);
        application.Resources["ComboBox.Static.Border"] = new SolidColorBrush(borderSubtle);
        application.Resources["ComboBox.Static.Editable.Background"] = new SolidColorBrush(panelBackground);
        application.Resources["ComboBox.Static.Editable.Foreground"] = new SolidColorBrush(textPrimary);
        application.Resources["ComboBox.Static.Editable.Border"] = new SolidColorBrush(borderSubtle);
        application.Resources["ComboBox.MouseOver.Background"] = new SolidColorBrush(hover);
        application.Resources["ComboBox.MouseOver.Foreground"] = new SolidColorBrush(textPrimary);
        application.Resources["ComboBox.MouseOver.Border"] = new SolidColorBrush(borderStrong);
        application.Resources["ComboBox.Focused.Background"] = new SolidColorBrush(panelBackground);
        application.Resources["ComboBox.Focused.Foreground"] = new SolidColorBrush(textPrimary);
        application.Resources["ComboBox.Focused.Border"] = new SolidColorBrush(borderStrong);
        application.Resources["ComboBox.Disabled.Foreground"] = new SolidColorBrush(textMuted);
        application.Resources["ComboBox.DropDown.Background"] = new SolidColorBrush(panelBackground);
        application.Resources["ComboBox.DropDown.Border"] = new SolidColorBrush(borderSubtle);
        application.Resources["ComboBox.DropDown.Glyph"] = new SolidColorBrush(textPrimary);
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
            ["WorkspaceBackgroundBrush"] = Colors.White,
            ["ControlHoverBrush"] = (Color)ColorConverter.ConvertFromString("#FFEAF0FA"),
            ["ControlPressedBrush"] = (Color)ColorConverter.ConvertFromString("#FFDCE8FA"),
            ["TextPrimaryBrush"] = (Color)ColorConverter.ConvertFromString("#FF20242C"),
            ["TextSecondaryBrush"] = (Color)ColorConverter.ConvertFromString("#FF626A78"),
            ["TextMutedBrush"] = (Color)ColorConverter.ConvertFromString("#FF7A8392"),
            ["BorderSubtleBrush"] = (Color)ColorConverter.ConvertFromString("#FFD9E0EE"),
            ["BorderStrongBrush"] = (Color)ColorConverter.ConvertFromString("#FFB9C7DA"),
            ["SelectionBrush"] = (Color)ColorConverter.ConvertFromString("#FFCCE0FF"),
            ["DisabledBrush"] = (Color)ColorConverter.ConvertFromString("#FFB7BFCC")
        };
    }

    private static Dictionary<string, Color> BuildScreenshotDarkPalette()
    {
        return new Dictionary<string, Color>
        {
            ["EditorBackgroundBrush"] = (Color)ColorConverter.ConvertFromString("#FF25282C"),
            ["PanelBackgroundBrush"] = (Color)ColorConverter.ConvertFromString("#FF2A2D31"),
            ["InspectorBackgroundBrush"] = (Color)ColorConverter.ConvertFromString("#FF2D3034"),
            ["ToolBarBackgroundBrush"] = (Color)ColorConverter.ConvertFromString("#FF303338"),
            ["WorkspaceBackgroundBrush"] = (Color)ColorConverter.ConvertFromString("#FF4A4E53"),
            ["ControlHoverBrush"] = (Color)ColorConverter.ConvertFromString("#FF3A3E44"),
            ["ControlPressedBrush"] = (Color)ColorConverter.ConvertFromString("#FF454A51"),
            ["TextPrimaryBrush"] = (Color)ColorConverter.ConvertFromString("#FFF4F4F4"),
            ["TextSecondaryBrush"] = (Color)ColorConverter.ConvertFromString("#FFD7D9DC"),
            ["TextMutedBrush"] = (Color)ColorConverter.ConvertFromString("#FFA6ABB1"),
            ["BorderSubtleBrush"] = (Color)ColorConverter.ConvertFromString("#FF3B3F45"),
            ["BorderStrongBrush"] = (Color)ColorConverter.ConvertFromString("#FF50555C"),
            ["SelectionBrush"] = (Color)ColorConverter.ConvertFromString("#FF3B82F6"),
            ["DisabledBrush"] = (Color)ColorConverter.ConvertFromString("#FF6B7077")
        };
    }
}
