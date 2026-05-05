using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Collections.Specialized;
using Microsoft.Win32;
using OasisEditor.Features.MfmeImport;
using EditorCommands = OasisEditor.Commands;
using OasisEditor.Views;

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
    private string _mameVersion = "0267";
    private string _mameExecutablePath = string.Empty;
    private string _mameInstallRootDirectory = string.Empty;
    private string _mameReleaseSource = string.Empty;
    private string _mameLuaPluginPath = string.Empty;
    private string _mameCommandLineOverrides = string.Empty;
    private string _mameValidationSummary = "Not validated.";
    private readonly AssetBrowserViewModel _assetBrowser;
    private readonly OutputLogViewModel _outputLog;
    private readonly InspectorViewModel _inspector;
    private readonly HierarchyViewModel _hierarchy;
    private readonly DocumentWorkspaceViewModel _documentWorkspace;
    private readonly ActiveDocumentContextService _activeDocumentContext;
    private readonly PanelRuntimeStateStore _panelRuntimeStates;
    private readonly HierarchyPanelCommandService _hierarchyPanelCommands;
    private bool _isRefreshingHierarchy;
    private readonly MfmeImportService _mfmeImportService = new();
    private readonly MameDownloadService _mameDownloadService = new();

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action<EditorToolWindowId>? ToolWindowOpenRequested;
    public event Action<EditorToolWindowId>? ToolWindowCloseRequested;

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
        ImportMfmeExtractCommand = new RelayCommand(ImportMfmeExtract, CanImportMfmeExtract);
        SaveSelectedDocumentCommand = new RelayCommand(SaveSelectedDocument, CanSaveSelectedDocument);
        CloseSelectedDocumentCommand = new RelayCommand(CloseSelectedDocument, CanCloseSelectedDocument);
        OpenPreferencesCommand = new RelayCommand(OpenPreferences);
        OpenProjectSettingsCommand = new RelayCommand(OpenProjectSettings);
        ClosePreferencesCommand = new RelayCommand(ClosePreferences);
        BrowseMameExecutableCommand = new RelayCommand(BrowseMameExecutable);
        ValidateMamePreferencesCommand = new RelayCommand(ValidateMamePreferences);
        RefreshMameVersionsCommand = new RelayCommand(RefreshMameVersions);
        DownloadMameVersionCommand = new RelayCommand(DownloadMameVersion);
        OpenMameInstallRootCommand = new RelayCommand(OpenMameInstallRoot);
        RemoveCachedMameVersionCommand = new RelayCommand(RemoveCachedMameVersion);
        CloseProjectSettingsCommand = new RelayCommand(CloseProjectSettings);
        CloseProjectCommand = new RelayCommand(CloseProject, CanCloseProject);
        ExitCommand = new RelayCommand(ExitApplication);

        _outputLog = new OutputLogViewModel();
        _outputLog.PropertyChanged += OnOutputLogPropertyChanged;
        _activeDocumentContext = new ActiveDocumentContextService();
        _panelRuntimeStates = new PanelRuntimeStateStore();
        _assetBrowser = new AssetBrowserViewModel(
            () => LoadedProject,
            () => OnPropertyChanged(nameof(SelectedAsset)),
            NotifyInspectorChanged,
            AddOutputEntry,
            OpenAssetDocument,
            PromptForAssetRename);
        _assetBrowser.StateChanged += OnAssetBrowserStateChanged;
        _inspector = new InspectorViewModel(
            () => SelectedAsset,
            () => SelectedDocument,
            () => LoadedProject,
            _activeDocumentContext,
            ExecuteDocumentCanvasCommand,
            ApplyInspectorSummary);
        _hierarchy = new HierarchyViewModel(
            () => SelectedDocument,
            [new Panel2DHierarchyProvider()]);
        _hierarchyPanelCommands = new HierarchyPanelCommandService(
            () => SelectedDocument,
            ExecuteDocumentCanvasCommand,
            UpdateDocumentPanelSelection,
            NotifyHierarchyCommands);

        var preferences = _preferencesStore.Load();
        _selectedThemePreference = preferences.ThemePreference;
        _mameVersion = preferences.Mame.Version;
        _mameExecutablePath = preferences.Mame.ExecutablePath;
        _mameInstallRootDirectory = preferences.Mame.InstallRootDirectory;
        _mameReleaseSource = preferences.Mame.ReleaseSource;
        _mameLuaPluginPath = preferences.Mame.LuaPluginPath;
        _mameCommandLineOverrides = preferences.Mame.CommandLineOverrides;

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
            _panelRuntimeStates,
            documentId =>
            {
                _activeDocumentContext.ClearDocumentState(documentId);
                _panelRuntimeStates.ClearDocumentState(documentId);
            });
        AssetBrowserItems = _assetBrowser.AssetBrowserItems;
        AssetBrowserItems.CollectionChanged += OnAssetBrowserItemsChanged;
        OutputEntries = _outputLog.OutputEntries;
        RefreshAssetBrowserCommand = _assetBrowser.RefreshAssetBrowserCommand;
        OpenAssetCommand = _assetBrowser.OpenAssetCommand;
        ShowAssetInExplorerCommand = _assetBrowser.ShowInExplorerCommand;
        RenameAssetCommand = _assetBrowser.RenameAssetCommand;
        DeleteAssetCommand = _assetBrowser.DeleteAssetCommand;
        DeleteSelectedHierarchyItemCommand = new PaneItemCommand<HierarchyItemViewModel>(
            GetSelectedHierarchyEntity,
            item => DeleteHierarchyItem(item),
            CanDeleteHierarchyItem);
        RenameSelectedHierarchyItemCommand = new RelayCommand(
            RenameSelectedHierarchyItemWithPrompt,
            CanRenameSelectedHierarchyItem);
        CutSelectedHierarchyItemCommand = new RelayCommand(
            () => _hierarchyPanelCommands.ExecuteCutSelected(),
            () => _hierarchyPanelCommands.CanCutSelected());
        CopySelectedHierarchyItemCommand = new RelayCommand(
            () => _hierarchyPanelCommands.ExecuteCopySelected(),
            () => _hierarchyPanelCommands.CanCopySelected());
        PasteHierarchyItemCommand = new RelayCommand(
            () => _hierarchyPanelCommands.ExecutePasteSelected(),
            () => _hierarchyPanelCommands.CanPasteSelected());
        DuplicateSelectedHierarchyItemCommand = new RelayCommand(
            () => _hierarchyPanelCommands.ExecuteDuplicateSelected(),
            () => _hierarchyPanelCommands.CanDuplicateSelected());
        BringToFrontHierarchyItemCommand = new RelayCommand(
            () => _hierarchyPanelCommands.ExecuteBringToFrontSelected(),
            () => _hierarchyPanelCommands.CanBringToFrontSelected());
        SendToBackHierarchyItemCommand = new RelayCommand(
            () => _hierarchyPanelCommands.ExecuteSendToBackSelected(),
            () => _hierarchyPanelCommands.CanSendToBackSelected());
        BringForwardHierarchyItemCommand = new RelayCommand(
            () => _hierarchyPanelCommands.ExecuteBringForwardSelected(),
            () => _hierarchyPanelCommands.CanBringForwardSelected());
        SendBackwardHierarchyItemCommand = new RelayCommand(
            () => _hierarchyPanelCommands.ExecuteSendBackwardSelected(),
            () => _hierarchyPanelCommands.CanSendBackwardSelected());
        LockSelectedHierarchyItemCommand = new RelayCommand(
            () => _hierarchyPanelCommands.ExecuteLockSelected(),
            () => _hierarchyPanelCommands.CanLockSelected());
        UnlockSelectedHierarchyItemCommand = new RelayCommand(
            () => _hierarchyPanelCommands.ExecuteUnlockSelected(),
            () => _hierarchyPanelCommands.CanUnlockSelected());
        HideSelectedHierarchyItemCommand = new RelayCommand(
            () => _hierarchyPanelCommands.ExecuteHideSelected(),
            () => _hierarchyPanelCommands.CanHideSelected());
        ShowSelectedHierarchyItemCommand = new RelayCommand(
            () => _hierarchyPanelCommands.ExecuteShowSelected(),
            () => _hierarchyPanelCommands.CanShowSelected());
        ClearOutputCommand = _outputLog.ClearOutputCommand;
        ApplyInspectorSummaryCommand = _inspector.ApplyInspectorSummaryCommand;
        AddOutputEntry("Editor shell initialized.", OutputLogStatus.Info);
        AddOutputEntry($"Theme preference loaded: {_selectedThemePreference}", OutputLogStatus.Info);

        LoadStartupProject(startupProjectFilePath.Trim());
        RefreshHierarchy();
    }

    public ICommand OpenUntitledDocumentCommand { get; }
    public ICommand OpenPanel2DStubCommand { get; }
    public ICommand OpenCabinet3DStubCommand { get; }
    public ICommand OpenMachineStubCommand { get; }
    public ICommand OpenDocumentCommand { get; }
    public ICommand ImportMfmeExtractCommand { get; }
    public ICommand SaveSelectedDocumentCommand { get; }
    public ICommand CloseSelectedDocumentCommand { get; }
    public ICommand RefreshAssetBrowserCommand { get; }
    public ICommand OpenAssetCommand { get; }
    public ICommand ShowAssetInExplorerCommand { get; }
    public ICommand RenameAssetCommand { get; }
    public ICommand DeleteAssetCommand { get; }
    public ICommand DeleteSelectedHierarchyItemCommand { get; }
    public ICommand RenameSelectedHierarchyItemCommand { get; }
    public ICommand CutSelectedHierarchyItemCommand { get; }
    public ICommand CopySelectedHierarchyItemCommand { get; }
    public ICommand PasteHierarchyItemCommand { get; }
    public ICommand DuplicateSelectedHierarchyItemCommand { get; }
    public ICommand BringToFrontHierarchyItemCommand { get; }
    public ICommand SendToBackHierarchyItemCommand { get; }
    public ICommand BringForwardHierarchyItemCommand { get; }
    public ICommand SendBackwardHierarchyItemCommand { get; }
    public ICommand LockSelectedHierarchyItemCommand { get; }
    public ICommand UnlockSelectedHierarchyItemCommand { get; }
    public ICommand HideSelectedHierarchyItemCommand { get; }
    public ICommand ShowSelectedHierarchyItemCommand { get; }
    public ICommand ClearOutputCommand { get; }
    public ICommand OpenPreferencesCommand { get; }
    public ICommand OpenProjectSettingsCommand { get; }
    public ICommand ClosePreferencesCommand { get; }
    public ICommand BrowseMameExecutableCommand { get; }
    public ICommand ValidateMamePreferencesCommand { get; }
    public ICommand RefreshMameVersionsCommand { get; }
    public ICommand DownloadMameVersionCommand { get; }
    public ICommand OpenMameInstallRootCommand { get; }
    public ICommand RemoveCachedMameVersionCommand { get; }
    public ICommand CloseProjectSettingsCommand { get; }
    public ICommand ApplyInspectorSummaryCommand { get; }
    public ICommand CloseProjectCommand { get; }
    public ICommand ExitCommand { get; }
    public ObservableCollection<string> RecentProjects { get; }
    public ObservableCollection<DocumentTabViewModel> OpenDocuments { get; }
    public ObservableCollection<AssetBrowserItemViewModel> AssetBrowserItems { get; }
    public ObservableCollection<OutputLogEntry> OutputEntries { get; }
    public IReadOnlyList<AssetDirectoryNodeViewModel> AssetDirectoryTree => _assetBrowser.AssetDirectoryTree;


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
            SavePreferences();
            AddOutputEntry($"Theme preference changed: {value}", OutputLogStatus.Info);
        }
    }

    public string MameVersion { get => _mameVersion; set { if (SetProperty(ref _mameVersion, value)) SavePreferences(); } }
    public string MameExecutablePath { get => _mameExecutablePath; set { if (SetProperty(ref _mameExecutablePath, value)) SavePreferences(); } }
    public string MameInstallRootDirectory { get => _mameInstallRootDirectory; set { if (SetProperty(ref _mameInstallRootDirectory, value)) SavePreferences(); } }
    public string MameReleaseSource { get => _mameReleaseSource; set { if (SetProperty(ref _mameReleaseSource, value)) SavePreferences(); } }
    public string MameLuaPluginPath { get => _mameLuaPluginPath; set { if (SetProperty(ref _mameLuaPluginPath, value)) SavePreferences(); } }
    public string MameCommandLineOverrides { get => _mameCommandLineOverrides; set { if (SetProperty(ref _mameCommandLineOverrides, value)) SavePreferences(); } }
    public string MameValidationSummary { get => _mameValidationSummary; private set => SetProperty(ref _mameValidationSummary, value); }

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
            if (ReferenceEquals(_selectedDocument, value))
            {
                return;
            }

            if (_selectedDocument is not null)
            {
                _selectedDocument.PropertyChanged -= OnSelectedDocumentPropertyChanged;
                _selectedDocument.PanelChanged -= OnSelectedDocumentPanelChanged;
            }

            if (SetProperty(ref _selectedDocument, value))
            {
                if (_selectedDocument is not null)
                {
                    _selectedDocument.PropertyChanged += OnSelectedDocumentPropertyChanged;
                    _selectedDocument.PanelChanged += OnSelectedDocumentPanelChanged;
                }

                _activeDocumentContext.SetActiveDocument(value);
                NotifyInspectorChanged();
                NotifyDocumentCommands();
                RefreshHierarchy();
                NotifyHierarchyCommands();
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

    public AssetDirectoryNodeViewModel? SelectedAssetDirectory
    {
        get => _assetBrowser.SelectedDirectory;
        set
        {
            if (ReferenceEquals(_assetBrowser.SelectedDirectory, value))
            {
                return;
            }

            _assetBrowser.SelectedDirectory = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedAssetDirectoryLabel));
        }
    }

    public string SelectedAssetDirectoryLabel => SelectedAssetDirectory?.DisplayPath ?? "Assets";
    public bool HasAssetBrowserItems => AssetBrowserItems.Count > 0;

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

    public bool ShowLampTestButton => _inspector.ShowLampTestButton;

    public IReadOnlyList<InspectorPropertyRowViewModel> InspectorPropertyRows => _inspector.InspectorPropertyRows;

    public IReadOnlyList<HierarchyItemViewModel> HierarchyItems => _hierarchy.Items;

    public bool HasHierarchyItems => _hierarchy.HasItems;

    public string HierarchyEmptyStateMessage => _hierarchy.EmptyStateMessage;

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

    public void SelectHierarchyItem(HierarchyItemViewModel? hierarchyItem)
    {
        if (SelectedDocument is null || SelectedDocument.Document.DocumentType != EditorDocumentType.Panel2D)
        {
            return;
        }

        if (hierarchyItem is null || hierarchyItem.IsGroup || hierarchyItem.PanelSelection is not PanelSelectionInfo selection)
        {
            return;
        }

        SelectedDocument.HierarchySelectedPanelSelection = selection;
        NotifyHierarchyCommands();
    }

    public void SelectHierarchyItemForContextMenu(HierarchyItemViewModel? hierarchyItem)
    {
        if (SelectedDocument is null || SelectedDocument.Document.DocumentType != EditorDocumentType.Panel2D)
        {
            return;
        }

        if (hierarchyItem is null || hierarchyItem.IsGroup || hierarchyItem.PanelSelection is not PanelSelectionInfo selection)
        {
            SelectedDocument.HierarchySelectedPanelSelection = null;
            NotifyHierarchyCommands();
            return;
        }

        SelectedDocument.HierarchySelectedPanelSelection = selection;
        NotifyHierarchyCommands();
    }

    public bool DeleteSelectedHierarchyItem()
    {
        return _hierarchyPanelCommands.DeleteSelected();
    }

    public bool TryGetSelectedHierarchyItemName(out string currentName)
    {
        return _hierarchyPanelCommands.TryGetSelectedName(out currentName);
    }

    public bool RenameSelectedHierarchyItem(string newName)
    {
        return _hierarchyPanelCommands.RenameSelected(newName);
    }

    private bool CanRenameSelectedHierarchyItem()
    {
        return TryGetSelectedHierarchyItemName(out _);
    }

    private void RenameSelectedHierarchyItemWithPrompt()
    {
        if (!TryGetSelectedHierarchyItemName(out var currentName))
        {
            return;
        }

        var renameDialog = new HierarchyRenameDialog(currentName)
        {
            Owner = _ownerWindow
        };

        if (renameDialog.ShowDialog() != true)
        {
            return;
        }

        RenameSelectedHierarchyItem(renameDialog.NameText);
    }

    private string? PromptForAssetRename(string currentName)
    {
        var renameDialog = new HierarchyRenameDialog(
            currentName,
            "Rename Asset",
            "Rename asset or folder")
        {
            Owner = _ownerWindow
        };

        return renameDialog.ShowDialog() == true ? renameDialog.NameText : null;
    }

    private HierarchyItemViewModel? GetSelectedHierarchyEntity() => _hierarchy.GetSelectedEntity();

    private bool CanDeleteHierarchyItem(HierarchyItemViewModel hierarchyItem) => _hierarchyPanelCommands.CanDeleteItem(hierarchyItem);

    private void DeleteHierarchyItem(HierarchyItemViewModel hierarchyItem) => _hierarchyPanelCommands.DeleteItem(hierarchyItem);

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
            OpenDocumentFromPath(dialog.FileName);
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            AddOutputEntry($"Open document failed: {ex.Message}", OutputLogStatus.Error);
            MessageBox.Show(ex.Message, "Open Document Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private bool CanImportMfmeExtract()
    {
        return LoadedProject is not null
               && SelectedDocument?.Document.DocumentType == EditorDocumentType.Panel2D;
    }

    private void ImportMfmeExtract()
    {
        if (LoadedProject is null)
        {
            return;
        }

        if (SelectedDocument?.Document.DocumentType != EditorDocumentType.Panel2D)
        {
            AddOutputEntry("MFME import is supported only when a Panel2D document is active.", OutputLogStatus.Warning);
            MessageBox.Show(
                "MFME import is currently supported only for Panel2D documents.",
                "Import MFME Extract",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = "Import MFME Extract Manifest",
            Filter = "MFME Extract Manifest|*.json|All Files|*.*",
            InitialDirectory = LoadedProject.ProjectDirectory,
            CheckFileExists = true
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var context = new MfmeImportContext
        {
            SourceExtractPath = dialog.FileName,
            ProjectRootPath = LoadedProject.ProjectDirectory,
            ProjectAssetsPath = LoadedProject.AssetsDirectory,
            CopyAssets = true
        };

        var result = _mfmeImportService.Import(context);
        foreach (var warning in result.Warnings)
        {
            AddOutputEntry($"MFME import warning ({warning.Code}): {warning.Message}", OutputLogStatus.Warning);
        }

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                AddOutputEntry($"MFME import failed: {error}", OutputLogStatus.Error);
            }

            MessageBox.Show(
                "MFME import failed. See Output for details.",
                "Import MFME Extract",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var activeDocument = SelectedDocument;
        if (activeDocument is null)
        {
            return;
        }

        var importCommand = new ImportMfmeExtractCommand(
            activeDocument.DocumentId,
            activeDocument,
            result.ImportedElements);
        var inserted = _documentWorkspace.ExecuteDocumentCanvasCommand(activeDocument.DocumentId, importCommand);
        if (!inserted)
        {
            AddOutputEntry("MFME import completed but no elements were inserted.", OutputLogStatus.Warning);
            return;
        }

        _assetBrowser.RefreshAssetBrowser();
        RefreshHierarchy();
        NotifyInspectorChanged();

        var grouped = result.ImportedElements
            .GroupBy(element => element.Kind)
            .OrderBy(group => group.Key.ToString(), StringComparer.Ordinal)
            .Select(group => $"{group.Key}: {group.Count()}");

        AddOutputEntry($"MFME import completed. Imported {result.ImportedElements.Count} elements.", OutputLogStatus.Info);
        AddOutputEntry($"MFME import kinds -> {string.Join(", ", grouped)}", OutputLogStatus.Info);
        AddOutputEntry($"MFME import skipped {result.SkippedLegacyComponentTypes.Count} unsupported components.", OutputLogStatus.Info);
        AddOutputEntry($"MFME import copied {result.CopiedAssetRelativePaths.Count} assets.", OutputLogStatus.Info);
    }

    private void OpenAssetDocument(AssetBrowserItemViewModel? asset)
    {
        if (asset is null)
        {
            return;
        }

        try
        {
            OpenDocumentFromPath(asset.FullPath);
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            AddOutputEntry($"Open asset failed: {ex.Message}", OutputLogStatus.Error);
            MessageBox.Show(ex.Message, "Open Asset Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OpenDocumentFromPath(string path)
    {
        var content = File.ReadAllText(path);
        var openData = DocumentWorkspaceViewModel.BuildOpenDocumentData(path, content);

        var openedNewTab = _documentWorkspace.OpenOrSelectDocument(
            path,
            openData.Summary,
            openData.PanelLayoutJson,
            openData.PanelTitle);
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

    private void OnAssetBrowserStateChanged()
    {
        OnPropertyChanged(nameof(AssetDirectoryTree));
        OnPropertyChanged(nameof(SelectedAssetDirectory));
        OnPropertyChanged(nameof(SelectedAssetDirectoryLabel));
        OnPropertyChanged(nameof(HasAssetBrowserItems));
    }

    private void OnAssetBrowserItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasAssetBrowserItems));
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
                current.CommandService)
            {
                PanelZoom = current.PanelZoom,
                PanelPanX = current.PanelPanX,
                PanelPanY = current.PanelPanY
            };
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
        ToolWindowOpenRequested?.Invoke(EditorToolWindowId.Preferences);
        AddOutputEntry("Opened Preferences pane.", OutputLogStatus.Info);
    }

    private void BrowseMameExecutable()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select mame.exe",
            Filter = "MAME Executable|mame.exe|Executable Files|*.exe|All Files|*.*",
            CheckFileExists = true,
            Multiselect = false,
            FileName = Path.GetFileName(MameExecutablePath)
        };

        if (dialog.ShowDialog(_ownerWindow) == true)
        {
            MameExecutablePath = dialog.FileName;
            if (string.IsNullOrWhiteSpace(MameInstallRootDirectory))
            {
                MameInstallRootDirectory = Path.GetDirectoryName(dialog.FileName) ?? string.Empty;
            }

            AddOutputEntry($"Selected MAME executable: {dialog.FileName}", OutputLogStatus.Info);
            ValidateMamePreferences();
        }
    }

    private void ValidateMamePreferences()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(MameExecutablePath) || !File.Exists(MameExecutablePath))
            errors.Add("MAME executable is missing or does not exist.");
        else if (!string.Equals(Path.GetFileName(MameExecutablePath), "mame.exe", StringComparison.OrdinalIgnoreCase))
            errors.Add("MAME executable path must point to mame.exe.");

        if (string.IsNullOrWhiteSpace(MameInstallRootDirectory) || !Directory.Exists(MameInstallRootDirectory))
            errors.Add("MAME install root directory is missing or does not exist.");

        if (string.IsNullOrWhiteSpace(MameLuaPluginPath) || !Directory.Exists(MameLuaPluginPath))
            errors.Add("Lua plugin directory is missing or does not exist.");

        if (string.IsNullOrWhiteSpace(MameVersion) || MameVersion.Any(c => !char.IsDigit(c)))
            errors.Add("MAME version must be numeric (example: 0267).");

        if (errors.Count == 0)
        {
            MameValidationSummary = "MAME preferences validation passed.";
            AddOutputEntry(MameValidationSummary, OutputLogStatus.Info);
        }
        else
        {
            MameValidationSummary = string.Join(" ", errors);
            AddOutputEntry($"MAME preferences validation failed: {MameValidationSummary}", OutputLogStatus.Warning);
        }
    }


    private async void RefreshMameVersions()
    {
        try
        {
            var versions = await _mameDownloadService.GetKnownVersionsAsync(CancellationToken.None);
            AddOutputEntry($"Known MAME versions: {string.Join(", ", versions)}", OutputLogStatus.Info);
        }
        catch (Exception ex)
        {
            AddOutputEntry($"Failed to refresh MAME versions: {ex.Message}", OutputLogStatus.Warning);
        }
    }

    private async void DownloadMameVersion()
    {
        try
        {
            ValidateMamePreferences();
            if (MameValidationSummary.StartsWith("MAME preferences validation failed", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var executablePath = await _mameDownloadService.DownloadAndExtractAsync(
                MameReleaseSource,
                MameVersion,
                MameInstallRootDirectory,
                new Progress<string>(message => AddOutputEntry(message, OutputLogStatus.Info)),
                CancellationToken.None);
            MameExecutablePath = executablePath;
            AddOutputEntry($"MAME download completed. Executable: {executablePath}", OutputLogStatus.Info);
            ValidateMamePreferences();
        }
        catch (Exception ex)
        {
            AddOutputEntry($"MAME download failed: {ex.Message}", OutputLogStatus.Warning);
        }
    }

    private void OpenMameInstallRoot()
    {
        if (string.IsNullOrWhiteSpace(MameInstallRootDirectory) || !Directory.Exists(MameInstallRootDirectory))
        {
            AddOutputEntry("MAME install root directory does not exist.", OutputLogStatus.Warning);
            return;
        }

        System.Diagnostics.Process.Start("explorer.exe", MameInstallRootDirectory);
    }

    private void RemoveCachedMameVersion()
    {
        try
        {
            var removed = _mameDownloadService.RemoveCachedVersion(MameInstallRootDirectory, MameVersion);
            AddOutputEntry(removed
                ? $"Removed cached MAME version {MameVersion}."
                : $"No cached MAME version directory found for {MameVersion}.", removed ? OutputLogStatus.Info : OutputLogStatus.Warning);
        }
        catch (Exception ex)
        {
            AddOutputEntry($"Failed to remove cached MAME version: {ex.Message}", OutputLogStatus.Warning);
        }
    }
    private void SavePreferences()
    {
        _preferencesStore.Save(new EditorPreferences
        {
            ThemePreference = SelectedThemePreference,
            Mame = new MamePreferences
            {
                Version = MameVersion,
                ExecutablePath = MameExecutablePath,
                InstallRootDirectory = MameInstallRootDirectory,
                ReleaseSource = MameReleaseSource,
                LuaPluginPath = MameLuaPluginPath,
                CommandLineOverrides = MameCommandLineOverrides
            }
        });
    }

    private void OpenProjectSettings()
    {
        ToolWindowOpenRequested?.Invoke(EditorToolWindowId.ProjectSettings);
        AddOutputEntry("Opened Project Settings pane.", OutputLogStatus.Info);
    }

    private void ClosePreferences()
    {
        ToolWindowCloseRequested?.Invoke(EditorToolWindowId.Preferences);
    }

    private void CloseProjectSettings()
    {
        ToolWindowCloseRequested?.Invoke(EditorToolWindowId.ProjectSettings);
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
        _panelRuntimeStates.ClearAll();
        PanelElementFactory.ProjectDirectoryPath = null;

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
        PanelElementFactory.ProjectDirectoryPath = project.ProjectDirectory;
        ProjectFilePath = project.ProjectFilePath;
        UpdateRecentProjects(project.ProjectFilePath);
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
        var document = OpenDocuments.FirstOrDefault(tab => tab.DocumentId == documentId);
        if (document is not null)
        {
            document.HierarchySelectedPanelSelection = selection;
        }

        _activeDocumentContext.SetPanelSelection(documentId, selection);
        _hierarchy.SyncSelection(selection);
        NotifyInspectorChanged();
        OnPropertyChanged(nameof(HierarchyItems));
    }


    private void NotifyHierarchyCommands()
    {
        if (DeleteSelectedHierarchyItemCommand is PaneItemCommand<HierarchyItemViewModel> deleteHierarchyCommand)
        {
            deleteHierarchyCommand.RaiseCanExecuteChanged();
        }

        if (RenameSelectedHierarchyItemCommand is RelayCommand renameHierarchyCommand)
        {
            renameHierarchyCommand.RaiseCanExecuteChanged();
        }

        if (CutSelectedHierarchyItemCommand is RelayCommand cutHierarchyCommand)
        {
            cutHierarchyCommand.RaiseCanExecuteChanged();
        }

        if (CopySelectedHierarchyItemCommand is RelayCommand copyHierarchyCommand)
        {
            copyHierarchyCommand.RaiseCanExecuteChanged();
        }

        if (PasteHierarchyItemCommand is RelayCommand pasteHierarchyCommand)
        {
            pasteHierarchyCommand.RaiseCanExecuteChanged();
        }

        if (DuplicateSelectedHierarchyItemCommand is RelayCommand duplicateHierarchyCommand)
        {
            duplicateHierarchyCommand.RaiseCanExecuteChanged();
        }

        if (BringToFrontHierarchyItemCommand is RelayCommand bringToFrontCommand)
        {
            bringToFrontCommand.RaiseCanExecuteChanged();
        }

        if (SendToBackHierarchyItemCommand is RelayCommand sendToBackCommand)
        {
            sendToBackCommand.RaiseCanExecuteChanged();
        }

        if (BringForwardHierarchyItemCommand is RelayCommand bringForwardCommand)
        {
            bringForwardCommand.RaiseCanExecuteChanged();
        }

        if (SendBackwardHierarchyItemCommand is RelayCommand sendBackwardCommand)
        {
            sendBackwardCommand.RaiseCanExecuteChanged();
        }

        if (LockSelectedHierarchyItemCommand is RelayCommand lockCommand)
        {
            lockCommand.RaiseCanExecuteChanged();
        }

        if (UnlockSelectedHierarchyItemCommand is RelayCommand unlockCommand)
        {
            unlockCommand.RaiseCanExecuteChanged();
        }

        if (HideSelectedHierarchyItemCommand is RelayCommand hideCommand)
        {
            hideCommand.RaiseCanExecuteChanged();
        }

        if (ShowSelectedHierarchyItemCommand is RelayCommand showCommand)
        {
            showCommand.RaiseCanExecuteChanged();
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

        if (ImportMfmeExtractCommand is RelayCommand importMfmeRelayCommand)
        {
            importMfmeRelayCommand.RaiseCanExecuteChanged();
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
        OnPropertyChanged(nameof(ShowLampTestButton));
        OnPropertyChanged(nameof(InspectorPropertyRows));
    }

    public void SetLampTestActive(bool isActive)
    {
        _inspector.SetLampTestActive(isActive);
    }

    private void RefreshHierarchy()
    {
        if (_isRefreshingHierarchy)
        {
            return;
        }

        _isRefreshingHierarchy = true;
        try
        {
            _hierarchy.Refresh();
            OnPropertyChanged(nameof(HierarchyItems));
            OnPropertyChanged(nameof(HasHierarchyItems));
            OnPropertyChanged(nameof(HierarchyEmptyStateMessage));
        }
        finally
        {
            _isRefreshingHierarchy = false;
        }
    }

    private void OnSelectedDocumentPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(DocumentTabViewModel.HierarchySelectedPanelSelection))
        {
            _hierarchy.SyncSelection(SelectedDocument?.HierarchySelectedPanelSelection);
            OnPropertyChanged(nameof(HierarchyItems));
            NotifyInspectorChanged();
            NotifyHierarchyCommands();
        }
    }

    private void OnSelectedDocumentPanelChanged(PanelChangeEvent panelChange)
    {
        if (SelectedDocument is null || panelChange.DocumentId != SelectedDocument.DocumentId)
        {
            return;
        }

        if (panelChange.AffectsHierarchy)
        {
            RefreshHierarchy();
            NotifyHierarchyCommands();
        }

        if (panelChange.AffectsInspectorRows)
        {
            _inspector.NotifyPanelChanged(panelChange);
            OnPropertyChanged(nameof(InspectorSummary));
            OnPropertyChanged(nameof(InspectorPropertyRows));
        }
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

internal readonly record struct OpenDocumentData(string Summary, string? PanelLayoutJson, string? PanelTitle = null);
