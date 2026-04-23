using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;

namespace OasisEditor;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly ProjectScaffolder _projectScaffolder = new();
    private readonly RecentProjectsStore _recentProjectsStore = new();
    private readonly IApplicationThemeService _applicationThemeService;
    private readonly EditorPreferencesStore _preferencesStore;
    private readonly Window _ownerWindow;
    private string _projectName = string.Empty;
    private string _projectLocation = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    private string _projectFilePath = string.Empty;
    private string _statusMessage = "Create a new project to get started.";
    private string? _selectedRecentProject;
    private EditorProject? _loadedProject;
    private DocumentTabViewModel? _selectedDocument;
    private AssetBrowserItemViewModel? _selectedAsset;
    private int _untitledDocumentCounter = 1;
    private int _panelDocumentCounter = 1;
    private int _cabinetDocumentCounter = 1;
    private int _machineDocumentCounter = 1;
    private ThemePreference _selectedThemePreference;
    private PreferencesWindow? _preferencesWindow;
    private ProjectSettingsWindow? _projectSettingsWindow;

    public event PropertyChangedEventHandler? PropertyChanged;

    public MainWindowViewModel(IApplicationThemeService applicationThemeService, EditorPreferencesStore preferencesStore, Window ownerWindow)
    {
        _applicationThemeService = applicationThemeService;
        _preferencesStore = preferencesStore;
        _ownerWindow = ownerWindow;

        CreateProjectCommand = new RelayCommand(CreateProject, CanCreateProject);
        OpenProjectCommand = new RelayCommand(OpenProject, CanOpenProject);
        OpenRecentProjectCommand = new RelayCommand(OpenSelectedRecentProject, CanOpenSelectedRecentProject);
        OpenUntitledDocumentCommand = new RelayCommand(OpenUntitledDocument, CanOpenUntitledDocument);
        OpenPanel2DStubCommand = new RelayCommand(OpenPanel2DStubDocument, CanOpenUntitledDocument);
        OpenCabinet3DStubCommand = new RelayCommand(OpenCabinet3DStubDocument, CanOpenUntitledDocument);
        OpenMachineStubCommand = new RelayCommand(OpenMachineStubDocument, CanOpenUntitledDocument);
        OpenDocumentCommand = new RelayCommand(OpenDocument, CanOpenDocument);
        SaveSelectedDocumentCommand = new RelayCommand(SaveSelectedDocument, CanSaveSelectedDocument);
        CloseSelectedDocumentCommand = new RelayCommand(CloseSelectedDocument, CanCloseSelectedDocument);
        RefreshAssetBrowserCommand = new RelayCommand(RefreshAssetBrowser, CanRefreshAssetBrowser);
        ClearOutputCommand = new RelayCommand(ClearOutput, CanClearOutput);
        OpenPreferencesCommand = new RelayCommand(OpenPreferences);
        OpenProjectSettingsCommand = new RelayCommand(OpenProjectSettings);
        ClosePreferencesCommand = new RelayCommand(ClosePreferences);
        CloseProjectSettingsCommand = new RelayCommand(CloseProjectSettings);
        ExitCommand = new RelayCommand(ExitApplication);

        var preferences = _preferencesStore.Load();
        _selectedThemePreference = preferences.ThemePreference;

        RecentProjects = new ObservableCollection<string>(_recentProjectsStore.Load());
        OpenDocuments = new ObservableCollection<DocumentTabViewModel>();
        AssetBrowserItems = new ObservableCollection<AssetBrowserItemViewModel>();
        OutputEntries = new ObservableCollection<string>();
        AddOutputEntry("Editor shell initialized.");
        AddOutputEntry($"Theme preference loaded: {_selectedThemePreference}");
    }

    public ICommand CreateProjectCommand { get; }
    public ICommand OpenProjectCommand { get; }
    public ICommand OpenRecentProjectCommand { get; }
    public ICommand OpenUntitledDocumentCommand { get; }
    public ICommand OpenPanel2DStubCommand { get; }
    public ICommand OpenCabinet3DStubCommand { get; }
    public ICommand OpenMachineStubCommand { get; }
    public ICommand OpenDocumentCommand { get; }
    public ICommand SaveSelectedDocumentCommand { get; }
    public ICommand CloseSelectedDocumentCommand { get; }
    public ICommand RefreshAssetBrowserCommand { get; }
    public ICommand ClearOutputCommand { get; }
    public ICommand OpenPreferencesCommand { get; }
    public ICommand OpenProjectSettingsCommand { get; }
    public ICommand ClosePreferencesCommand { get; }
    public ICommand CloseProjectSettingsCommand { get; }
    public ICommand ExitCommand { get; }
    public ObservableCollection<string> RecentProjects { get; }
    public ObservableCollection<DocumentTabViewModel> OpenDocuments { get; }
    public ObservableCollection<AssetBrowserItemViewModel> AssetBrowserItems { get; }
    public ObservableCollection<string> OutputEntries { get; }


    public IReadOnlyList<ThemePreference> ThemePreferences { get; } = Enum.GetValues<ThemePreference>();

    public ThemePreference SelectedThemePreference
    {
        get => _selectedThemePreference;
        set
        {
            if (!SetProperty(ref _selectedThemePreference, value))
            {
                return;
            }

            _applicationThemeService.ApplyTheme(Application.Current ?? throw new InvalidOperationException("Application is not initialized."), value);
            _preferencesStore.Save(new EditorPreferences { ThemePreference = value });
            AddOutputEntry($"Theme preference changed: {value}");
        }
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
            AddOutputEntry($"Created project at {projectPath}");
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            AddOutputEntry($"Create project failed: {ex.Message}");
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
            EditorDocument.CreateUntitled($"Untitled {_untitledDocumentCounter++}"));

        OpenDocuments.Add(document);
        SelectedDocument = document;
        StatusMessage = $"Opened document tab: {document.Title}";
        AddOutputEntry($"Opened document tab: {document.Title}");
    }

    private void OpenPanel2DStubDocument()
    {
        if (LoadedProject is null)
        {
            return;
        }

        var document = new DocumentTabViewModel(
            EditorDocument.CreatePanel2DStub($"Panel {_panelDocumentCounter++}"));

        OpenDocuments.Add(document);
        SelectedDocument = document;
        StatusMessage = $"Opened panel document stub: {document.Title}";
        AddOutputEntry($"Opened panel document stub: {document.Title}");
    }

    private void OpenCabinet3DStubDocument()
    {
        if (LoadedProject is null)
        {
            return;
        }

        var document = new DocumentTabViewModel(
            EditorDocument.CreateCabinet3DStub($"Cabinet {_cabinetDocumentCounter++}"));

        OpenDocuments.Add(document);
        SelectedDocument = document;
        StatusMessage = $"Opened cabinet document stub: {document.Title}";
        AddOutputEntry($"Opened cabinet document stub: {document.Title}");
    }

    private void OpenMachineStubDocument()
    {
        if (LoadedProject is null)
        {
            return;
        }

        var document = new DocumentTabViewModel(
            EditorDocument.CreateMachineStub($"Machine {_machineDocumentCounter++}"));

        OpenDocuments.Add(document);
        SelectedDocument = document;
        StatusMessage = $"Opened machine document stub: {document.Title}";
        AddOutputEntry($"Opened machine document stub: {document.Title}");
    }

    private bool CanCloseSelectedDocument()
    {
        return SelectedDocument is not null;
    }

    private bool CanOpenDocument()
    {
        return LoadedProject is not null;
    }

    private void OpenDocument()
    {
        if (LoadedProject is null)
        {
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = "Open Document",
            Filter = "Editor Documents|*.panel2d;*.cabinet3d;*.machine|All Files|*.*",
            InitialDirectory = LoadedProject.ProjectDirectory,
            CheckFileExists = true
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var path = dialog.FileName;
            var preview = File.ReadAllText(path);
            var summary = preview.Length > 300 ? $"{preview[..300]}..." : preview;
            if (string.IsNullOrWhiteSpace(summary))
            {
                summary = "Document opened (file is empty).";
            }

            var openedNewTab = OpenOrSelectDocument(path, summary);
            if (!openedNewTab)
            {
                AddOutputEntry($"Switched to already open document tab for {path}");
            }

            var selectedTitle = SelectedDocument?.Title ?? Path.GetFileName(path);
            StatusMessage = openedNewTab
                ? $"Opened document: {selectedTitle}"
                : $"Activated open document tab: {selectedTitle}";
            AddOutputEntry(openedNewTab
                ? $"Opened document file {path}"
                : $"Activated existing document tab for {path}");
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            AddOutputEntry($"Open document failed: {ex.Message}");
            MessageBox.Show(ex.Message, "Open Document Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private bool CanSaveSelectedDocument()
    {
        return SelectedDocument is not null && SelectedDocument.Document.DocumentType != EditorDocumentType.ProjectOverview;
    }

    private void SaveSelectedDocument()
    {
        if (SelectedDocument is null)
        {
            return;
        }

        var current = SelectedDocument;
        var savePath = current.Document.IsUntitled ? PromptSavePath() : current.FilePath;
        if (string.IsNullOrWhiteSpace(savePath))
        {
            return;
        }

        try
        {
            var persisted = new
            {
                title = current.Title,
                type = current.Document.DocumentType.ToString(),
                summary = current.ContentSummary,
                savedAtUtc = DateTime.UtcNow
            };

            var content = JsonSerializer.Serialize(persisted, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(savePath, content);

            var updatedDocument = new DocumentTabViewModel(
                current.Document.SaveAs(savePath, current.ContentSummary).MarkClean());
            var index = OpenDocuments.IndexOf(current);
            if (index >= 0)
            {
                OpenDocuments[index] = updatedDocument;
            }

            SelectedDocument = updatedDocument;
            StatusMessage = $"Saved document: {updatedDocument.Title}";
            AddOutputEntry($"Saved document to {savePath}");
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            AddOutputEntry($"Save document failed: {ex.Message}");
            MessageBox.Show(ex.Message, "Save Document Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private string? PromptSavePath()
    {
        if (LoadedProject is null)
        {
            return null;
        }

        var selectedDocument = SelectedDocument;
        var defaultName = selectedDocument?.Document.Title ?? "Document";
        var (defaultExtension, filter) = selectedDocument?.Document.DocumentType switch
        {
            EditorDocumentType.Cabinet3D => (".cabinet3d", "Cabinet 3D|*.cabinet3d|Panel 2D|*.panel2d|Machine|*.machine|All Files|*.*"),
            EditorDocumentType.Machine => (".machine", "Machine|*.machine|Panel 2D|*.panel2d|Cabinet 3D|*.cabinet3d|All Files|*.*"),
            _ => (".panel2d", "Panel 2D|*.panel2d|Cabinet 3D|*.cabinet3d|Machine|*.machine|All Files|*.*")
        };

        var dialog = new SaveFileDialog
        {
            Title = "Save Document",
            InitialDirectory = LoadedProject.AssetsDirectory,
            FileName = $"{defaultName}{defaultExtension}",
            DefaultExt = defaultExtension,
            Filter = filter
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
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
            AddOutputEntry($"Closed document tab: {documentToClose.Title}");
            return;
        }

        var nextIndex = Math.Clamp(index, 0, OpenDocuments.Count - 1);
        SelectedDocument = OpenDocuments[nextIndex];
        StatusMessage = $"Closed document tab: {documentToClose.Title}";
        AddOutputEntry($"Closed document tab: {documentToClose.Title}");
    }


    private void OpenPreferences()
    {
        if (_preferencesWindow is { IsLoaded: true })
        {
            _preferencesWindow.Activate();
            return;
        }

        _preferencesWindow = new PreferencesWindow
        {
            Owner = _ownerWindow,
            DataContext = this
        };

        _preferencesWindow.Closed += (_, _) => _preferencesWindow = null;
        _preferencesWindow.Show();
        AddOutputEntry("Opened Preferences window.");
    }

    private void OpenProjectSettings()
    {
        if (_projectSettingsWindow is { IsLoaded: true })
        {
            _projectSettingsWindow.Activate();
            return;
        }

        _projectSettingsWindow = new ProjectSettingsWindow
        {
            Owner = _ownerWindow,
            DataContext = this
        };

        _projectSettingsWindow.Closed += (_, _) => _projectSettingsWindow = null;
        _projectSettingsWindow.Show();
        AddOutputEntry("Opened Project Settings window.");
    }

    private void ClosePreferences()
    {
        _preferencesWindow?.Close();
    }

    private void CloseProjectSettings()
    {
        _projectSettingsWindow?.Close();
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
            AddOutputEntry($"Loaded project '{openedProjectName}' from {projectFile}");
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            AddOutputEntry($"Open project failed: {ex.Message}");
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
        var overviewDocument = new DocumentTabViewModel(EditorDocument.CreateProjectOverview(LoadedProject));

        OpenDocuments.Add(overviewDocument);
        SelectedDocument = overviewDocument;
    }

    private bool OpenOrSelectDocument(string path, string summary)
    {
        var existing = OpenDocuments.FirstOrDefault(
            tab => string.Equals(tab.FilePath, path, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            SelectedDocument = existing;
            return false;
        }

        var document = new DocumentTabViewModel(EditorDocument.CreateFromFile(path, summary));
        OpenDocuments.Add(document);
        SelectedDocument = document;
        return true;
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
            AddOutputEntry("Asset browser cleared (no project loaded).");
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
        AddOutputEntry($"Asset browser refreshed ({AssetBrowserItems.Count} files).");
    }

    private bool CanClearOutput()
    {
        return OutputEntries.Count > 0;
    }

    private void ClearOutput()
    {
        OutputEntries.Clear();
        AddOutputEntry("Output log cleared.");
    }

    private void AddOutputEntry(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        OutputEntries.Add($"[{timestamp}] {message}");
        NotifyOutputCommand();
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

        if (OpenPanel2DStubCommand is RelayCommand openPanelRelayCommand)
        {
            openPanelRelayCommand.RaiseCanExecuteChanged();
        }

        if (OpenCabinet3DStubCommand is RelayCommand openCabinetRelayCommand)
        {
            openCabinetRelayCommand.RaiseCanExecuteChanged();
        }

        if (OpenMachineStubCommand is RelayCommand openMachineRelayCommand)
        {
            openMachineRelayCommand.RaiseCanExecuteChanged();
        }

        if (OpenDocumentCommand is RelayCommand openDocumentRelayCommand)
        {
            openDocumentRelayCommand.RaiseCanExecuteChanged();
        }

        if (SaveSelectedDocumentCommand is RelayCommand saveRelayCommand)
        {
            saveRelayCommand.RaiseCanExecuteChanged();
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

    private void NotifyOutputCommand()
    {
        if (ClearOutputCommand is RelayCommand clearRelayCommand)
        {
            clearRelayCommand.RaiseCanExecuteChanged();
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
    public DocumentTabViewModel(EditorDocument document)
    {
        Document = document;
    }

    public EditorDocument Document { get; }
    public string Title => Document.IsDirty ? $"{Document.Title}*" : Document.Title;
    public string TypeLabel => Document.DocumentType switch
    {
        EditorDocumentType.ProjectOverview => "Project",
        EditorDocumentType.Panel2D => "Panel 2D",
        EditorDocumentType.Cabinet3D => "Cabinet 3D",
        EditorDocumentType.Machine => "Machine",
        _ => "Document Type"
    };
    public string FilePath => Document.FilePath;
    public string ContentSummary => Document.ContentSummary;
    public bool IsDirty => Document.IsDirty;
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
