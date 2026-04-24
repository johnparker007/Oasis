using System.Windows;
using System.Windows.Input;

namespace OasisEditor;

public partial class MainWindow : Window
{
    public MainWindow(
        IApplicationThemeService applicationThemeService,
        EditorPreferencesStore preferencesStore,
        string startupProjectFilePath)
    {
        if (string.IsNullOrWhiteSpace(startupProjectFilePath))
        {
            throw new InvalidOperationException("Editor shell requires an active loaded project.");
        }

        InitializeComponent();

        ApplyAvalonDockThemeIfAvailable();
        EditorKeyboardShortcuts.RegisterWindowBindings(this);
        var viewModel = new MainWindowViewModel(applicationThemeService, preferencesStore, this, startupProjectFilePath);
        DataContext = viewModel;

        CommandBindings.Add(new CommandBinding(CanvasPanBehavior.UndoCommand, (_, args) =>
        {
            viewModel.UndoActiveDocument();
            args.Handled = true;
        }, (_, args) =>
        {
            args.CanExecute = viewModel.CanUndoActiveDocument();
            args.Handled = true;
        }));

        CommandBindings.Add(new CommandBinding(CanvasPanBehavior.RedoCommand, (_, args) =>
        {
            viewModel.RedoActiveDocument();
            args.Handled = true;
        }, (_, args) =>
        {
            args.CanExecute = viewModel.CanRedoActiveDocument();
            args.Handled = true;
        }));
    }


    private void ApplyAvalonDockThemeIfAvailable()
    {
        var darkThemeType = Type.GetType("AvalonDock.Themes.VS2013.VS2013DarkTheme, AvalonDock.Themes.VS2013")
                            ?? Type.GetType("AvalonDock.Themes.VS2013.Vs2013DarkTheme, AvalonDock.Themes.VS2013");

        if (darkThemeType is null)
        {
            return;
        }

        var darkTheme = Activator.CreateInstance(darkThemeType);
        var themeProperty = DockingManager.GetType().GetProperty("Theme");
        themeProperty?.SetValue(DockingManager, darkTheme);
    }
}
