using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;

namespace OasisEditor;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly ProjectScaffolder _projectScaffolder = new();
    private string _projectName = string.Empty;
    private string _projectLocation = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    private string _statusMessage = "Create a new project to get started.";

    public event PropertyChangedEventHandler? PropertyChanged;

    public MainWindowViewModel()
    {
        CreateProjectCommand = new RelayCommand(CreateProject, CanCreateProject);
    }

    public ICommand CreateProjectCommand { get; }

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

    private void NotifyCreateCommand()
    {
        if (CreateProjectCommand is RelayCommand relayCommand)
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
