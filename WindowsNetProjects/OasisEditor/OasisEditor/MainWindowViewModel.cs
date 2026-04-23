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
    private string _projectName = string.Empty;
    private string _projectLocation = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    private string _projectFilePath = string.Empty;
    private string _statusMessage = "Create a new project to get started.";

    public event PropertyChangedEventHandler? PropertyChanged;

    public MainWindowViewModel()
    {
        CreateProjectCommand = new RelayCommand(CreateProject, CanCreateProject);
        OpenProjectCommand = new RelayCommand(OpenProject, CanOpenProject);
    }

    public ICommand CreateProjectCommand { get; }
    public ICommand OpenProjectCommand { get; }

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

    private void CreateProject()
    {
        try
        {
            var projectPath = _projectScaffolder.CreateProject(ProjectName, ProjectLocation);
            StatusMessage = $"Project created: {projectPath}";
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
        try
        {
            var projectFile = ProjectFilePath.Trim();

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

            StatusMessage = $"Project opened: {openedProjectName} ({projectFile})";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            MessageBox.Show(ex.Message, "Open Project Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private bool CanOpenProject()
    {
        return !string.IsNullOrWhiteSpace(ProjectFilePath);
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

    private bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
        {
            return false;
        }

        storage = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
