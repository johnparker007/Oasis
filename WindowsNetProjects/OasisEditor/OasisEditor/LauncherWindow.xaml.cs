using System;
using System.Windows;

namespace OasisEditor;

public partial class LauncherWindow : Window
{
    private const double DefaultScreenCoverage = 0.8;

    public LauncherWindow(IApplicationThemeService applicationThemeService, EditorPreferencesStore preferencesStore)
    {
        InitializeComponent();
        ApplyDefaultSize();
        DataContext = new LauncherWindowViewModel(applicationThemeService, preferencesStore, this);
    }

    private void ApplyDefaultSize()
    {
        var workArea = SystemParameters.WorkArea;

        Width = Math.Max(MinWidth, workArea.Width * DefaultScreenCoverage);
        Height = Math.Max(MinHeight, workArea.Height * DefaultScreenCoverage);
    }
}
