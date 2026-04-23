using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;

namespace OasisEditor;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly ProjectScaffolder _projectScaffolder = new();
    private readonly RecentProjectsStore _recentProjectsStore = new();
    private string _projectName = string.Empty;
    private string _projectLocation = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    private string _projectFilePath = string.Empty;
    private string _statusMessage = "Create a new project to get started.";
    private string? _selectedRecentProject;
    private EditorProject? _loadedProject;

    public event PropertyChangedEventHandler? PropertyChanged;

    public MainWindowViewModel()
    {
        CreateProjectCommand = new RelayCommand(CreateProject, CanCreateProject);
        OpenProjectCommand = new RelayCommand(OpenProject, CanOpenProject);
        OpenRecentProjectCommand = new RelayCommand(OpenSelectedRecentProject, CanOpenSelectedRecentProject);
        ExitCommand = new RelayCommand(ExitApplication);

        RecentProjects = new ObservableCollection<string>(_recentProjectsStore.Load());
    }

    public ICommand CreateProjectCommand { get; }
    public ICommand OpenProjectCommand { get; }
    public ICommand OpenRecentProjectCommand { get; }
    public ICommand ExitCommand { get; }
    public ObservableCollection<string> RecentProjects { get; }

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

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public EditorProject? LoadedProject
    {
        get => _loadedProject;
        private set
        {
            if (SetProperty(ref _loadedProject, value))
            {
                OnPropertyChanged(nameof(HasLoadedProject));
            }
        }
    }

    public bool HasLoadedProject => LoadedProject is not null;

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

    private void CreateProject()
    {
        try
        {
            var projectPath = _projectScaffolder.CreateProject(ProjectName, ProjectLocation);
            var projectFilePath = Path.Combine(projectPath, $"{ProjectName.Trim()}.oasisproj");

            OpenProjectFile(projectFilePath, $"Project created and loaded: {projectPath}");
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            MessageBox.Show(ex.Message, "Create Project Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private bool CanCreateProject()
    {
        return !string.IsNullOrWhiteSpace(ProjectName) && !string.IsNullOrWhiteSpace(ProjectLocation);
    }

    private void OpenProject()
    {
        OpenProjectFile(ProjectFilePath, null);
    }

    private bool CanOpenProject()
    {
        return !string.IsNullOrWhiteSpace(ProjectFilePath);
    }

    private void OpenSelectedRecentProject()
    {
        if (string.IsNullOrWhiteSpace(SelectedRecentProject))
        {
            return;
        }

        OpenProjectFile(SelectedRecentProject, null);
    }

    private bool CanOpenSelectedRecentProject()
    {
        return !string.IsNullOrWhiteSpace(SelectedRecentProject);
    }

    private static void ExitApplication()
    {
        Application.Current.Shutdown();
    }

    private void OpenProjectFile(string projectFilePath, string? successMessage)
    {
        try
        {
            var projectFile = projectFilePath.Trim();

            if (!File.Exists(projectFile))
            {
                throw new FileNotFoundException("Project file was not found.", projectFile);
            }

            if (!string.Equals(Path.GetExtension(projectFile), ".oasisproj", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Project file must use the .oasisproj extension.");
            }

            using var projectStream = File.OpenRead(projectFile);
            using var projectDocument = JsonDocument.Parse(projectStream);

            if (!projectDocument.RootElement.TryGetProperty("name", out var projectNameElement))
            {
                throw new InvalidOperationException("Project metadata is missing required 'name' field.");
            }

            var openedProjectName = projectNameElement.GetString();
            if (string.IsNullOrWhiteSpace(openedProjectName))
            {
                throw new InvalidOperationException("Project metadata contains an empty 'name' field.");
            }

            var projectDirectory = Path.GetDirectoryName(projectFile);
            if (string.IsNullOrWhiteSpace(projectDirectory))
            {
                throw new InvalidOperationException("Unable to determine project directory.");
            }

            var layoutElement = projectDocument.RootElement.GetProperty("layout");
            var assetsDirectory = ResolveProjectDirectory(projectDirectory, layoutElement, "assets");
            var machinesDirectory = ResolveProjectDirectory(projectDirectory, layoutElement, "machines");
            var generatedDirectory = ResolveProjectDirectory(projectDirectory, layoutElement, "generated");

            LoadedProject = new EditorProject
            {
                Name = openedProjectName,
                ProjectFilePath = projectFile,
                ProjectDirectory = projectDirectory,
                AssetsDirectory = assetsDirectory,
                MachinesDirectory = machinesDirectory,
                GeneratedDirectory = generatedDirectory
            };

            ProjectFilePath = projectFile;
            UpdateRecentProjects(projectFile);
            StatusMessage = successMessage ?? $"Project opened: {openedProjectName} ({projectFile})";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            MessageBox.Show(ex.Message, "Open Project Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private static string ResolveProjectDirectory(string projectDirectory, JsonElement layoutElement, string propertyName)
    {
        if (!layoutElement.TryGetProperty(propertyName, out var directoryElement))
        {
            throw new InvalidOperationException($"Project metadata is missing required layout '{propertyName}' field.");
        }

        var relativePath = directoryElement.GetString();
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new InvalidOperationException($"Project metadata layout '{propertyName}' field is empty.");
        }

        var fullPath = Path.GetFullPath(Path.Combine(projectDirectory, relativePath));
        if (!Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException($"Project layout folder was not found: {fullPath}");
        }

        return fullPath;
    }

    private void UpdateRecentProjects(string projectFilePath)
    {
        var updated = _recentProjectsStore.Add(projectFilePath);

        RecentProjects.Clear();
        foreach (var item in updated)
        {
            RecentProjects.Add(item);
        }
    }

    private void NotifyCreateCommand()
    {
        if (CreateProjectCommand is RelayCommand relayCommand)
        {
            relayCommand.RaiseCanExecuteChanged();
        }
    }

    private void NotifyOpenCommand()
    {
        if (OpenProjectCommand is RelayCommand relayCommand)
        {
            relayCommand.RaiseCanExecuteChanged();
        }
    }

    private void NotifyOpenRecentCommand()
    {
        if (OpenRecentProjectCommand is RelayCommand relayCommand)
        {
            relayCommand.RaiseCanExecuteChanged();
        }
    }

    private bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
        {
            return false;
        }

        storage = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
