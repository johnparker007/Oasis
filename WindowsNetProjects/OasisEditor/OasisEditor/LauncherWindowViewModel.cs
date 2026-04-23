using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;

namespace OasisEditor;

public sealed class LauncherWindowViewModel : INotifyPropertyChanged
{
    private readonly ProjectScaffolder _projectScaffolder = new();
    private readonly IApplicationThemeService _applicationThemeService;
    private readonly EditorPreferencesStore _preferencesStore;
    private readonly Window _launcherWindow;
    private string _projectName = string.Empty;
    private string _projectLocation = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    private string _statusMessage = "Create a new project to continue.";

    public event PropertyChangedEventHandler? PropertyChanged;

    public LauncherWindowViewModel(IApplicationThemeService applicationThemeService, EditorPreferencesStore preferencesStore, Window launcherWindow)
    {
        _applicationThemeService = applicationThemeService;
        _preferencesStore = preferencesStore;
        _launcherWindow = launcherWindow;

        CreateProjectCommand = new RelayCommand(CreateProject, CanCreateProject);
        ExitCommand = new RelayCommand(ExitApplication);
    }

    public ICommand CreateProjectCommand { get; }

    public ICommand ExitCommand { get; }

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
            var projectFilePath = Path.Combine(projectPath, $"{ProjectName.Trim()}.oasisproj");
            var mainWindow = new MainWindow(_applicationThemeService, _preferencesStore, projectFilePath);
            mainWindow.Show();
            _launcherWindow.Close();
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
        (CreateProjectCommand as RelayCommand)?.RaiseCanExecuteChanged();
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
