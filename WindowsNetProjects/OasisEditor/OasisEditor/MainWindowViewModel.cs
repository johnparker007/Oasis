using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
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
    private DocumentTabViewModel? _selectedDocument;
    private AssetBrowserItemViewModel? _selectedAsset;
    private int _untitledDocumentCounter = 1;

    public event PropertyChangedEventHandler? PropertyChanged;

    public MainWindowViewModel()
    {
        CreateProjectCommand = new RelayCommand(CreateProject, CanCreateProject);
        OpenProjectCommand = new RelayCommand(OpenProject, CanOpenProject);
        OpenRecentProjectCommand = new RelayCommand(OpenSelectedRecentProject, CanOpenSelectedRecentProject);
        OpenUntitledDocumentCommand = new RelayCommand(OpenUntitledDocument, CanOpenUntitledDocument);
        CloseSelectedDocumentCommand = new RelayCommand(CloseSelectedDocument, CanCloseSelectedDocument);
        RefreshAssetBrowserCommand = new RelayCommand(RefreshAssetBrowser, CanRefreshAssetBrowser);
        ExitCommand = new RelayCommand(ExitApplication);

        RecentProjects = new ObservableCollection<string>(_recentProjectsStore.Load());
        OpenDocuments = new ObservableCollection<DocumentTabViewModel>();
        AssetBrowserItems = new ObservableCollection<AssetBrowserItemViewModel>();
    }

    public ICommand CreateProjectCommand { get; }
    public ICommand OpenProjectCommand { get; }
    public ICommand OpenRecentProjectCommand { get; }
    public ICommand OpenUntitledDocumentCommand { get; }
    public ICommand CloseSelectedDocumentCommand { get; }
    public ICommand RefreshAssetBrowserCommand { get; }
    public ICommand ExitCommand { get; }
    public ObservableCollection<string> RecentProjects { get; }
    public ObservableCollection<DocumentTabViewModel> OpenDocuments { get; }
    public ObservableCollection<AssetBrowserItemViewModel> AssetBrowserItems { get; }

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
                NotifyInspectorChanged();
                NotifyDocumentCommands();
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

    public DocumentTabViewModel? SelectedDocument
    {
        get => _selectedDocument;
        set
        {
            if (SetProperty(ref _selectedDocument, value))
            {
                NotifyInspectorChanged();
                NotifyDocumentCommands();
            }
        }
    }

    public AssetBrowserItemViewModel? SelectedAsset
    {
        get => _selectedAsset;
        set
        {
            if (SetProperty(ref _selectedAsset, value))
            {
                NotifyInspectorChanged();
                NotifyAssetBrowserCommand();
            }
        }
    }

    public string InspectorTitle
    {
        get
        {
            if (SelectedAsset is not null)
            {
                return $"Asset: {SelectedAsset.DisplayPath}";
            }

            if (SelectedDocument is not null)
            {
                return $"Document: {SelectedDocument.Title}";
            }

            if (LoadedProject is not null)
            {
                return $"Project: {LoadedProject.Name}";
            }

            return "No selection";
        }
    }

    public string InspectorType
    {
        get
        {
            if (SelectedAsset is not null)
            {
                return "Asset File";
            }

            if (SelectedDocument is not null)
            {
                return SelectedDocument.TypeLabel;
            }

            if (LoadedProject is not null)
            {
                return "Editor Project";
            }

            return "None";
        }
    }

    public string InspectorPath
    {
        get
        {
            if (SelectedAsset is not null)
            {
                return SelectedAsset.FullPath;
            }

            if (SelectedDocument is not null)
            {
                return SelectedDocument.FilePath;
            }

            if (LoadedProject is not null)
            {
                return LoadedProject.ProjectFilePath;
            }

            return "Select an asset or document to inspect details.";
        }
    }

    public string InspectorSummary
    {
        get
        {
            if (SelectedAsset is not null)
            {
                return "Use this panel as the starting point for future property editing.";
            }

            if (SelectedDocument is not null)
            {
                return SelectedDocument.ContentSummary;
            }

            if (LoadedProject is not null)
            {
                return "Project loaded. Select a document tab or asset file to inspect it.";
            }

            return "Open or create a project to enable the inspector.";
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

    private bool CanOpenUntitledDocument()
    {
        return LoadedProject is not null;
    }

    private void OpenUntitledDocument()
    {
        if (LoadedProject is null)
        {
            return;
        }

        var document = new DocumentTabViewModel(
            $"Untitled {_untitledDocumentCounter++}",
            "Document Type",
            "No file associated yet.",
            "Create or open a project asset to begin editing.");

        OpenDocuments.Add(document);
        SelectedDocument = document;
        StatusMessage = $"Opened document tab: {document.Title}";
    }

    private bool CanCloseSelectedDocument()
    {
        return SelectedDocument is not null;
    }

    private void CloseSelectedDocument()
    {
        if (SelectedDocument is null)
        {
            return;
        }

        var documentToClose = SelectedDocument;
        var index = OpenDocuments.IndexOf(documentToClose);

        OpenDocuments.Remove(documentToClose);

        if (OpenDocuments.Count == 0)
        {
            SelectedDocument = null;
            StatusMessage = "Closed document tab.";
            return;
        }

        var nextIndex = Math.Clamp(index, 0, OpenDocuments.Count - 1);
        SelectedDocument = OpenDocuments[nextIndex];
        StatusMessage = $"Closed document tab: {documentToClose.Title}";
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
            EnsureProjectOverviewDocument();
            RefreshAssetBrowser();
            StatusMessage = successMessage ?? $"Project opened: {openedProjectName} ({projectFile})";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            MessageBox.Show(ex.Message, "Open Project Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void EnsureProjectOverviewDocument()
    {
        if (LoadedProject is null)
        {
            return;
        }

        OpenDocuments.Clear();
        var overviewDocument = new DocumentTabViewModel(
            "Project Overview",
            "Project",
            LoadedProject.ProjectFilePath,
            $"Assets: {LoadedProject.AssetsDirectory}\nMachines: {LoadedProject.MachinesDirectory}\nGenerated: {LoadedProject.GeneratedDirectory}");

        OpenDocuments.Add(overviewDocument);
        SelectedDocument = overviewDocument;
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

    private bool CanRefreshAssetBrowser()
    {
        return LoadedProject is not null;
    }

    private void RefreshAssetBrowser()
    {
        if (LoadedProject is null)
        {
            AssetBrowserItems.Clear();
            SelectedAsset = null;
            NotifyInspectorChanged();
            return;
        }

        var assetsRoot = LoadedProject.AssetsDirectory;
        var assetFiles = Directory.EnumerateFiles(assetsRoot, "*", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        AssetBrowserItems.Clear();
        foreach (var file in assetFiles)
        {
            var relativePath = Path.GetRelativePath(assetsRoot, file);
            AssetBrowserItems.Add(new AssetBrowserItemViewModel(relativePath, file));
        }

        SelectedAsset = AssetBrowserItems.FirstOrDefault();
        NotifyInspectorChanged();
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

    private void NotifyDocumentCommands()
    {
        if (OpenUntitledDocumentCommand is RelayCommand openRelayCommand)
        {
            openRelayCommand.RaiseCanExecuteChanged();
        }

        if (CloseSelectedDocumentCommand is RelayCommand closeRelayCommand)
        {
            closeRelayCommand.RaiseCanExecuteChanged();
        }

        NotifyAssetBrowserCommand();
    }

    private void NotifyAssetBrowserCommand()
    {
        if (RefreshAssetBrowserCommand is RelayCommand refreshRelayCommand)
        {
            refreshRelayCommand.RaiseCanExecuteChanged();
        }
    }

    private void NotifyInspectorChanged()
    {
        OnPropertyChanged(nameof(InspectorTitle));
        OnPropertyChanged(nameof(InspectorType));
        OnPropertyChanged(nameof(InspectorPath));
        OnPropertyChanged(nameof(InspectorSummary));
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

public sealed class DocumentTabViewModel
{
    public DocumentTabViewModel(string title, string typeLabel, string filePath, string contentSummary)
    {
        Title = title;
        TypeLabel = typeLabel;
        FilePath = filePath;
        ContentSummary = contentSummary;
    }

    public string Title { get; }
    public string TypeLabel { get; }
    public string FilePath { get; }
    public string ContentSummary { get; }
}

public sealed class AssetBrowserItemViewModel
{
    public AssetBrowserItemViewModel(string displayPath, string fullPath)
    {
        DisplayPath = displayPath;
        FullPath = fullPath;
    }

    public string DisplayPath { get; }
    public string FullPath { get; }
}
