using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using OasisEditor.Progress;

namespace OasisEditor;

public sealed class LauncherWindowViewModel : INotifyPropertyChanged
{
    private readonly Automation.IProjectContainerCreationService _projectContainerCreationService;
    private readonly RecentProjectsStore _recentProjectsStore = new();
    private readonly IApplicationThemeService _applicationThemeService;
    private readonly EditorPreferencesStore _preferencesStore;
    private readonly Window _launcherWindow;
    private string _projectName = string.Empty;
    private string _projectLocation = GetDefaultProjectLocation();
    private string _projectFilePath = string.Empty;
    private string _statusMessage = "Create or open a project to continue.";
    private string? _selectedRecentProject;
    private bool _isOpeningEditor;

    public event PropertyChangedEventHandler? PropertyChanged;

    public LauncherWindowViewModel(
        IApplicationThemeService applicationThemeService,
        EditorPreferencesStore preferencesStore,
        Window launcherWindow,
        Automation.IProjectContainerCreationService? projectContainerCreationService = null)
    {
        _applicationThemeService = applicationThemeService;
        _projectContainerCreationService = projectContainerCreationService ?? new Automation.ProjectContainerCreationService();
        _preferencesStore = preferencesStore;
        _launcherWindow = launcherWindow;

        CreateProjectCommand = new RelayCommand(CreateProject, CanCreateProject);
        BrowseProjectFileCommand = new RelayCommand(BrowseProjectFile);
        OpenProjectCommand = new RelayCommand(OpenProject, CanOpenProject);
        OpenRecentProjectCommand = new RelayCommand(OpenSelectedRecentProject, CanOpenSelectedRecentProject);
        ExitCommand = new RelayCommand(ExitApplication);

        RecentProjects = new ObservableCollection<string>(_recentProjectsStore.Load());
    }

    public ICommand CreateProjectCommand { get; }
    public ICommand BrowseProjectFileCommand { get; }
    public ICommand OpenProjectCommand { get; }
    public ICommand OpenRecentProjectCommand { get; }
    public ICommand ExitCommand { get; }
    public ObservableCollection<string> RecentProjects { get; }

    public static string GetDefaultProjectLocation()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Oasis",
            "Editor",
            "Projects");
    }

    public string ProjectName
    {
        get => _projectName;
        set
        {
            if (SetProperty(ref _projectName, value))
            {
                NotifyCreateCommand();
            }
        }
    }

    public string ProjectLocation
    {
        get => _projectLocation;
        set
        {
            if (SetProperty(ref _projectLocation, value))
            {
                NotifyCreateCommand();
            }
        }
    }

    public string ProjectFilePath
    {
        get => _projectFilePath;
        set
        {
            if (SetProperty(ref _projectFilePath, value))
            {
                NotifyOpenCommand();
            }
        }
    }

    public string? SelectedRecentProject
    {
        get => _selectedRecentProject;
        set
        {
            if (SetProperty(ref _selectedRecentProject, value))
            {
                NotifyOpenRecentCommand();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    private void CreateProject()
    {
        try
        {
            var projectPath = _projectContainerCreationService.CreateProjectContainer(ProjectName, ProjectLocation);
            var projectFilePath = Path.Combine(projectPath, $"{ProjectName.Trim()}.oasisproj");
            BeginOpenEditor(projectFilePath);
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            MessageBox.Show(ex.Message, "Create Project Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private bool CanCreateProject()
    {
        return !_isOpeningEditor && !string.IsNullOrWhiteSpace(ProjectName) && !string.IsNullOrWhiteSpace(ProjectLocation);
    }

    private void BrowseProjectFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Open Project",
            Filter = "Oasis Project|*.oasisproj|All Files|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog() == true)
        {
            ProjectFilePath = dialog.FileName;
            StatusMessage = $"Selected project file: {ProjectFilePath}";
        }
        else
        {
            ProjectFilePath = string.Empty;
            StatusMessage = "Project selection canceled. You are still in the Launcher.";
        }
    }

    private void OpenProject()
    {
        BeginOpenEditor(ProjectFilePath);
    }

    private bool CanOpenProject()
    {
        return !_isOpeningEditor && !string.IsNullOrWhiteSpace(ProjectFilePath);
    }

    private void OpenSelectedRecentProject()
    {
        if (string.IsNullOrWhiteSpace(SelectedRecentProject))
        {
            return;
        }

        BeginOpenEditor(SelectedRecentProject);
    }

    private bool CanOpenSelectedRecentProject()
    {
        return !_isOpeningEditor && !string.IsNullOrWhiteSpace(SelectedRecentProject);
    }

    private async void BeginOpenEditor(string projectFilePath)
    {
        if (_isOpeningEditor)
        {
            return;
        }

        _isOpeningEditor = true;
        NotifyProjectCommands();

        MainWindow? mainWindow = null;
        try
        {
            var trimmed = projectFilePath.Trim();
            var progressService = new WpfProgressDialogService(() => _launcherWindow, _launcherWindow.Dispatcher);
            mainWindow = await progressService.RunAsync(
                new EditorProgressRequest(
                    "Starting Editor",
                    "Starting Editor...",
                    EditorProgressMode.Indeterminate,
                    CanCancel: false,
                    ShowDelay: TimeSpan.Zero),
                async (progress, token) =>
                {
                    progress.ReportIndeterminate("Opening project...");
                    ValidateProjectFile(trimmed);

                    token.ThrowIfCancellationRequested();
                    progress.ReportIndeterminate("Preparing editor shell...");
                    var preparedWindow = new MainWindow(_applicationThemeService, _preferencesStore, trimmed);

                    await preparedWindow.PrepareForFirstShowAsync(progress, token).ConfigureAwait(true);

                    progress.ReportIndeterminate("Finalizing editor...");
                    await _launcherWindow.Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                    return preparedWindow;
                }).ConfigureAwait(true);

            mainWindow.Closed += (_, _) =>
            {
                if (_launcherWindow.IsVisible)
                {
                    return;
                }

                _launcherWindow.Close();
            };

            _launcherWindow.Hide();
            mainWindow.Show();
            StatusMessage = $"Opened project: {trimmed}";
            mainWindow = null;
        }
        catch (Exception ex)
        {
            mainWindow?.CloseWithoutSavingPlacement();
            StatusMessage = ex.Message;
            if (!_launcherWindow.IsVisible)
            {
                _launcherWindow.Show();
            }
            MessageBox.Show(ex.Message, "Open Project Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            _isOpeningEditor = false;
            NotifyProjectCommands();
        }
    }

    private static void ValidateProjectFile(string projectFilePath)
    {
        if (!File.Exists(projectFilePath))
        {
            throw new FileNotFoundException("Project file was not found.", projectFilePath);
        }

        if (!string.Equals(Path.GetExtension(projectFilePath), ".oasisproj", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Project file must use the .oasisproj extension.");
        }

        using var projectStream = File.OpenRead(projectFilePath);
        using var projectDocument = JsonDocument.Parse(projectStream);

        if (!projectDocument.RootElement.TryGetProperty("name", out var projectNameElement))
        {
            throw new InvalidOperationException("Project metadata is missing required 'name' field.");
        }

        var projectName = projectNameElement.GetString();
        if (string.IsNullOrWhiteSpace(projectName))
        {
            throw new InvalidOperationException("Project metadata contains an empty 'name' field.");
        }
    }

    private void NotifyProjectCommands()
    {
        NotifyCreateCommand();
        NotifyOpenCommand();
        NotifyOpenRecentCommand();
    }

    private void NotifyCreateCommand()
    {
        (CreateProjectCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    private void NotifyOpenCommand()
    {
        (OpenProjectCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    private void NotifyOpenRecentCommand()
    {
        (OpenRecentProjectCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    private static void ExitApplication()
    {
        Application.Current.Shutdown();
    }

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
