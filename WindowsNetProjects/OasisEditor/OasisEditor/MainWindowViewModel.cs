using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using EditorCommands = OasisEditor.Commands;

namespace OasisEditor;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly RecentProjectsStore _recentProjectsStore = new();
    private readonly IApplicationThemeService _applicationThemeService;
    private readonly EditorPreferencesStore _preferencesStore;
    private readonly Window _ownerWindow;
    private string _projectFilePath = string.Empty;
    private string _statusMessage = "Create a new project to get started.";
    private EditorProject? _loadedProject;
    private DocumentTabViewModel? _selectedDocument;
    private ThemePreference _selectedThemePreference;
    private PreferencesWindow? _preferencesWindow;
    private ProjectSettingsWindow? _projectSettingsWindow;
    private readonly AssetBrowserViewModel _assetBrowser;
    private readonly OutputLogViewModel _outputLog;
    private readonly InspectorViewModel _inspector;
    private readonly DocumentWorkspaceViewModel _documentWorkspace;
    private readonly ActiveDocumentContextService _activeDocumentContext;

    public event PropertyChangedEventHandler? PropertyChanged;

    public MainWindowViewModel(
        IApplicationThemeService applicationThemeService,
        EditorPreferencesStore preferencesStore,
        Window ownerWindow,
        string startupProjectFilePath)
    {
        _applicationThemeService = applicationThemeService;
        _preferencesStore = preferencesStore;
        _ownerWindow = ownerWindow;

        if (string.IsNullOrWhiteSpace(startupProjectFilePath))
        {
            throw new InvalidOperationException("Editor shell requires an active loaded project.");
        }

        OpenUntitledDocumentCommand = new RelayCommand(OpenUntitledDocument, CanOpenUntitledDocument);
        OpenPanel2DStubCommand = new RelayCommand(OpenPanel2DStubDocument, CanOpenUntitledDocument);
        OpenCabinet3DStubCommand = new RelayCommand(OpenCabinet3DStubDocument, CanOpenUntitledDocument);
        OpenMachineStubCommand = new RelayCommand(OpenMachineStubDocument, CanOpenUntitledDocument);
        OpenDocumentCommand = new RelayCommand(OpenDocument, CanOpenDocument);
        SaveSelectedDocumentCommand = new RelayCommand(SaveSelectedDocument, CanSaveSelectedDocument);
        CloseSelectedDocumentCommand = new RelayCommand(CloseSelectedDocument, CanCloseSelectedDocument);
        OpenPreferencesCommand = new RelayCommand(OpenPreferences);
        OpenProjectSettingsCommand = new RelayCommand(OpenProjectSettings);
        ClosePreferencesCommand = new RelayCommand(ClosePreferences);
        CloseProjectSettingsCommand = new RelayCommand(CloseProjectSettings);
        CloseProjectCommand = new RelayCommand(CloseProject, CanCloseProject);
        ExitCommand = new RelayCommand(ExitApplication);

        _outputLog = new OutputLogViewModel();
        _outputLog.PropertyChanged += OnOutputLogPropertyChanged;
        _activeDocumentContext = new ActiveDocumentContextService();
        _assetBrowser = new AssetBrowserViewModel(
            () => LoadedProject,
            () => OnPropertyChanged(nameof(SelectedAsset)),
            NotifyInspectorChanged,
            AddOutputEntry);
        _inspector = new InspectorViewModel(
            () => SelectedAsset,
            () => SelectedDocument,
            () => LoadedProject,
            _activeDocumentContext,
            ApplyInspectorSummary);

        var preferences = _preferencesStore.Load();
        _selectedThemePreference = preferences.ThemePreference;

        RecentProjects = new ObservableCollection<string>(_recentProjectsStore.Load());
        OpenDocuments = new ObservableCollection<DocumentTabViewModel>();
        _documentWorkspace = new DocumentWorkspaceViewModel(
            () => _loadedProject,
            value => LoadedProject = value,
            OpenDocuments,
            () => _selectedDocument,
            value => SelectedDocument = value,
            NotifyUndoRedoStateChanged,
            value => StatusMessage = value,
            AddOutputEntry,
            documentId => _activeDocumentContext.ClearDocumentState(documentId));
        AssetBrowserItems = _assetBrowser.AssetBrowserItems;
        OutputEntries = _outputLog.OutputEntries;
        RefreshAssetBrowserCommand = _assetBrowser.RefreshAssetBrowserCommand;
        ClearOutputCommand = _outputLog.ClearOutputCommand;
        ApplyInspectorSummaryCommand = _inspector.ApplyInspectorSummaryCommand;
        AddOutputEntry("Editor shell initialized.", OutputLogStatus.Info);
        AddOutputEntry($"Theme preference loaded: {_selectedThemePreference}", OutputLogStatus.Info);

        LoadStartupProject(startupProjectFilePath.Trim());
    }

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
    public ICommand ApplyInspectorSummaryCommand { get; }
    public ICommand CloseProjectCommand { get; }
    public ICommand ExitCommand { get; }
    public ObservableCollection<string> RecentProjects { get; }
    public ObservableCollection<DocumentTabViewModel> OpenDocuments { get; }
    public ObservableCollection<AssetBrowserItemViewModel> AssetBrowserItems { get; }
    public ObservableCollection<OutputLogEntry> OutputEntries { get; }


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
            AddOutputEntry($"Theme preference changed: {value}", OutputLogStatus.Info);
        }
    }

    public string StatusMessage
    {
        get => LastOutputEntry?.Message ?? _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public OutputLogEntry? LastOutputEntry => _outputLog.LastEntry;

    public string StatusIconGlyph => LastOutputEntry?.IconGlyph ?? "\uE946";

    public Brush StatusMessageBrush => LastOutputEntry?.StatusBrush ?? Brushes.White;

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
            SetProperty(ref _projectFilePath, value);
        }
    }

    public DocumentTabViewModel? SelectedDocument
    {
        get => _selectedDocument;
        set
        {
            if (SetProperty(ref _selectedDocument, value))
            {
                _activeDocumentContext.SetActiveDocument(value);
                NotifyInspectorChanged();
                NotifyDocumentCommands();
            }
        }
    }

    public AssetBrowserItemViewModel? SelectedAsset
    {
        get => _assetBrowser.SelectedAsset;
        set
        {
            _assetBrowser.SelectedAsset = value;
            OnPropertyChanged();
        }
    }

    public string InspectorTitle => _inspector.InspectorTitle;

    public string InspectorType => _inspector.InspectorType;

    public string InspectorPath => _inspector.InspectorPath;

    public string InspectorSummary => _inspector.InspectorSummary;

    public string InspectorEditableSummary
    {
        get => _inspector.InspectorEditableSummary;
        set => _inspector.InspectorEditableSummary = value;
    }

    public bool CanEditInspectorSummary => _inspector.CanEditInspectorSummary;

    public string UndoMenuHeader
    {
        get
        {
            var description = SelectedDocument?.CommandService.UndoDescription;
            return string.IsNullOrWhiteSpace(description) ? "_Undo" : $"_Undo {description}";
        }
    }

    public string RedoMenuHeader
    {
        get
        {
            var description = SelectedDocument?.CommandService.RedoDescription;
            return string.IsNullOrWhiteSpace(description) ? "_Redo" : $"_Redo {description}";
        }
    }

    private bool CanOpenUntitledDocument()
    {
        return _documentWorkspace.CanOpenUntitledDocument();
    }

    private void OpenUntitledDocument()
    {
        _documentWorkspace.OpenUntitledDocument();
    }

    private void OpenPanel2DStubDocument()
    {
        _documentWorkspace.OpenPanel2DStubDocument();
    }

    private void OpenCabinet3DStubDocument()
    {
        _documentWorkspace.OpenCabinet3DStubDocument();
    }

    private void OpenMachineStubDocument()
    {
        _documentWorkspace.OpenMachineStubDocument();
    }

    private bool CanCloseSelectedDocument()
    {
        return _documentWorkspace.CanCloseSelectedDocument();
    }

    private bool CanOpenDocument()
    {
        return _documentWorkspace.CanOpenDocument();
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
            var content = File.ReadAllText(path);
            var openData = DocumentWorkspaceViewModel.BuildOpenDocumentData(path, content);

            var openedNewTab = _documentWorkspace.OpenOrSelectDocument(path, openData.Summary, openData.PanelLayoutJson);
            if (!openedNewTab)
            {
                AddOutputEntry($"Switched to already open document tab for {path}", OutputLogStatus.Info);
            }

            var selectedTitle = SelectedDocument?.Title ?? Path.GetFileName(path);
            StatusMessage = openedNewTab
                ? $"Opened document: {selectedTitle}"
                : $"Activated open document tab: {selectedTitle}";
            AddOutputEntry(openedNewTab
                ? $"Opened document file {path}"
                : $"Activated existing document tab for {path}",
                OutputLogStatus.Info);
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            AddOutputEntry($"Open document failed: {ex.Message}", OutputLogStatus.Error);
            MessageBox.Show(ex.Message, "Open Document Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private bool CanSaveSelectedDocument()
    {
        return _documentWorkspace.CanSaveSelectedDocument();
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
            var content = DocumentWorkspaceViewModel.BuildDocumentContent(current);
            File.WriteAllText(savePath, content);

            var updatedDocument = new DocumentTabViewModel(
                current.Document.SaveAs(savePath, current.ContentSummary).MarkClean(),
                current.PanelLayoutJson,
                current.DocumentId,
                current.CommandService);
            _documentWorkspace.ReplaceDocument(current, updatedDocument);
            StatusMessage = $"Saved document: {updatedDocument.Title}";
            AddOutputEntry($"Saved document to {savePath}", OutputLogStatus.Info);
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            AddOutputEntry($"Save document failed: {ex.Message}", OutputLogStatus.Error);
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
        _documentWorkspace.CloseSelectedDocument();
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
        AddOutputEntry("Opened Preferences window.", OutputLogStatus.Info);
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
        AddOutputEntry("Opened Project Settings window.", OutputLogStatus.Info);
    }

    private void ClosePreferences()
    {
        _preferencesWindow?.Close();
    }

    private void CloseProjectSettings()
    {
        _projectSettingsWindow?.Close();
    }

    private bool CanCloseProject()
    {
        return LoadedProject is not null;
    }

    private void CloseProject()
    {
        if (LoadedProject is null)
        {
            return;
        }

        ClosePreferences();
        CloseProjectSettings();
        ClearProjectSessionState();

        var launcherWindow = new LauncherWindow(_applicationThemeService, _preferencesStore);
        launcherWindow.Show();

        _ownerWindow.Close();
        Application.Current.MainWindow = launcherWindow;
        launcherWindow.Activate();
        launcherWindow.Focus();
    }

    private void ClearProjectSessionState()
    {
        _documentWorkspace.ClearProjectSessionState();
        _activeDocumentContext.ClearAll();

        AssetBrowserItems.Clear();
        SelectedAsset = null;
        ProjectFilePath = string.Empty;
    }

    private static void ExitApplication()
    {
        Application.Current.Shutdown();
    }

    private void LoadStartupProject(string startupProjectFilePath)
    {
        var project = LoadProjectFromFile(startupProjectFilePath);
        LoadedProject = project;
        ProjectFilePath = project.ProjectFilePath;
        UpdateRecentProjects(project.ProjectFilePath);
        _documentWorkspace.EnsureProjectOverviewDocument();
        _assetBrowser.RefreshAssetBrowser();
        StatusMessage = $"Project opened: {project.Name} ({project.ProjectFilePath})";
        AddOutputEntry($"Loaded startup project '{project.Name}' from {project.ProjectFilePath}", OutputLogStatus.Info);
    }

    private static EditorProject LoadProjectFromFile(string projectFilePath)
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

        var openedProjectName = projectNameElement.GetString();
        if (string.IsNullOrWhiteSpace(openedProjectName))
        {
            throw new InvalidOperationException("Project metadata contains an empty 'name' field.");
        }

        var projectDirectory = Path.GetDirectoryName(projectFilePath);
        if (string.IsNullOrWhiteSpace(projectDirectory))
        {
            throw new InvalidOperationException("Unable to determine project directory.");
        }

        var layoutElement = projectDocument.RootElement.GetProperty("layout");
        var assetsDirectory = ResolveProjectDirectory(projectDirectory, layoutElement, "assets");
        var machinesDirectory = ResolveProjectDirectory(projectDirectory, layoutElement, "machines");
        var generatedDirectory = ResolveProjectDirectory(projectDirectory, layoutElement, "generated");

        return new EditorProject
        {
            Name = openedProjectName,
            ProjectFilePath = projectFilePath,
            ProjectDirectory = projectDirectory,
            AssetsDirectory = assetsDirectory,
            MachinesDirectory = machinesDirectory,
            GeneratedDirectory = generatedDirectory
        };
    }

    public bool CanUndoActiveDocument()
    {
        return _documentWorkspace.CanUndoActiveDocument();
    }

    public bool CanRedoActiveDocument()
    {
        return _documentWorkspace.CanRedoActiveDocument();
    }

    public bool UndoActiveDocument()
    {
        return _documentWorkspace.UndoActiveDocument();
    }

    public bool RedoActiveDocument()
    {
        return _documentWorkspace.RedoActiveDocument();
    }

    public bool ExecuteDocumentCanvasCommand(Guid documentId, EditorCommands.ICommand command)
    {
        return _documentWorkspace.ExecuteDocumentCanvasCommand(documentId, command);
    }

    public void UpdateDocumentPanelSelection(Guid documentId, PanelSelectionInfo? selection)
    {
        _activeDocumentContext.SetPanelSelection(documentId, selection);
        NotifyInspectorChanged();
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

    private void AddOutputEntry(string message, OutputLogStatus status)
    {
        _outputLog.AddOutputEntry(message, status);
    }

    private void OnOutputLogPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not nameof(OutputLogViewModel.LastEntry))
        {
            return;
        }

        OnPropertyChanged(nameof(LastOutputEntry));
        OnPropertyChanged(nameof(StatusMessage));
        OnPropertyChanged(nameof(StatusIconGlyph));
        OnPropertyChanged(nameof(StatusMessageBrush));
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

        if (CloseProjectCommand is RelayCommand closeProjectRelayCommand)
        {
            closeProjectRelayCommand.RaiseCanExecuteChanged();
        }

        NotifyUndoRedoStateChanged();
        _assetBrowser.NotifyRefreshCommand();
    }

    private void NotifyUndoRedoStateChanged()
    {
        OnPropertyChanged(nameof(UndoMenuHeader));
        OnPropertyChanged(nameof(RedoMenuHeader));
        CommandManager.InvalidateRequerySuggested();
    }

    private void NotifyInspectorChanged()
    {
        _inspector.NotifyContextChanged();
        OnPropertyChanged(nameof(InspectorTitle));
        OnPropertyChanged(nameof(InspectorType));
        OnPropertyChanged(nameof(InspectorPath));
        OnPropertyChanged(nameof(InspectorSummary));
        OnPropertyChanged(nameof(InspectorEditableSummary));
        OnPropertyChanged(nameof(CanEditInspectorSummary));
    }

    private DocumentTabViewModel? ApplyInspectorSummary(DocumentTabViewModel _, string summary)
    {
        return _documentWorkspace.ApplyInspectorSummary(summary);
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

internal readonly record struct OpenDocumentData(string Summary, string? PanelLayoutJson);
