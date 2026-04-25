using System.Windows;
using System.Windows.Input;

namespace OasisEditor;

public partial class MainWindow : Window
{
    private readonly EditorPreferencesStore _preferencesStore;
    private readonly string _startupProjectFilePath;

    public MainWindow(
        IApplicationThemeService applicationThemeService,
        EditorPreferencesStore preferencesStore,
        string startupProjectFilePath)
    {
        if (string.IsNullOrWhiteSpace(startupProjectFilePath))
        {
            throw new InvalidOperationException("Editor shell requires an active loaded project.");
        }

        _preferencesStore = preferencesStore;
        _startupProjectFilePath = NormalizeProjectPath(startupProjectFilePath);

        InitializeComponent();
        Loaded += OnLoaded;
        Closing += OnClosing;
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

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ApplyWindowPlacement();
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        SaveWindowPlacement();
    }

    private void ApplyWindowPlacement()
    {
        var preferences = _preferencesStore.Load();
        if (!preferences.ProjectWindowStates.TryGetValue(_startupProjectFilePath, out var persistedState))
        {
            WindowState = WindowState.Maximized;
            return;
        }

        var width = ClampToMinimum(persistedState.Width, MinWidth);
        var height = ClampToMinimum(persistedState.Height, MinHeight);
        Width = width;
        Height = height;
        Left = persistedState.Left;
        Top = persistedState.Top;
        WindowState = persistedState.IsMaximized ? WindowState.Maximized : WindowState.Normal;
    }

    private void SaveWindowPlacement()
    {
        var preferences = _preferencesStore.Load();
        var states = new Dictionary<string, ProjectWindowState>(preferences.ProjectWindowStates, StringComparer.OrdinalIgnoreCase);
        var bounds = WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;

        states[_startupProjectFilePath] = new ProjectWindowState
        {
            Left = bounds.Left,
            Top = bounds.Top,
            Width = bounds.Width,
            Height = bounds.Height,
            IsMaximized = WindowState == WindowState.Maximized
        };

        _preferencesStore.Save(new EditorPreferences
        {
            ThemePreference = preferences.ThemePreference,
            ProjectWindowStates = states
        });
    }

    private static double ClampToMinimum(double value, double minimum)
    {
        if (double.IsNaN(value) || value <= 0)
        {
            return minimum;
        }

        return value < minimum ? minimum : value;
    }

    private static string NormalizeProjectPath(string projectFilePath)
    {
        return Path.GetFullPath(projectFilePath.Trim());
    }
}
