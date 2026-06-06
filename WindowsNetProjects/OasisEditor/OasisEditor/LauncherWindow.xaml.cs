using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

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

    private void RecentProjectListBoxItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not LauncherWindowViewModel viewModel || sender is not ListBoxItem item)
        {
            return;
        }

        if (item.DataContext is string projectFilePath)
        {
            viewModel.SelectedRecentProject = projectFilePath;
        }

        if (!viewModel.OpenRecentProjectCommand.CanExecute(null))
        {
            return;
        }

        viewModel.OpenRecentProjectCommand.Execute(null);
        e.Handled = true;
    }

    private void ApplyDefaultSize()
    {
        var workArea = SystemParameters.WorkArea;

        Width = Math.Max(MinWidth, workArea.Width * DefaultScreenCoverage);
        Height = Math.Max(MinHeight, workArea.Height * DefaultScreenCoverage);
    }
}
