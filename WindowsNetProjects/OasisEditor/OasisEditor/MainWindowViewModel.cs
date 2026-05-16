using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
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
    private string _mameRomDownloadBaseUrl = MameRomDownloadService.DefaultDownloadRootUrl;
    private string _mameRomArchiveExtension = MameRomDownloadService.DefaultArchiveExtension;
    private string _mameLocalRomSourceDirectory = string.Empty;
    private string _mameLocalRomArchiveExtension = MameRomDownloadService.DefaultArchiveExtension;
    private bool _keepMameUpToDateAutomatically = true;
    private bool _debugOutputLamps;
    private bool _debugOutputStdIn;
    private bool _debugOutputStdOut;
    private string _mameValidationSummary = "Not validated.";
    private string _selectedPreferencesCategory = "Appearance";
    private FruitMachinePlatformType _selectedFruitMachinePlatform = FruitMachinePlatformType.None;
    private string _mameRomName = string.Empty;
    private bool _automaticallyDownloadMissingRoms = true;
    private string _mameRomStatus = "Unknown";
    private bool _isMameRomDownloadInProgress;
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
    private readonly MameRomDownloadService _mameRomDownloadService = new();
    private readonly MamePluginAssetValidator _mamePluginAssetValidator = new();
    private readonly MamePluginDeploymentService _mamePluginDeploymentService = new();
    private readonly IMameSetupOrchestrator _mameSetupOrchestrator;
    private readonly IMameVersionCatalogService _mameVersionCatalogService;
    private bool _isLoadingPreferences;
    private readonly IMameEmulationService _mameEmulationService;
    private MameSetupState _mameSetupState = MameSetupState.NotStarted;
    private bool _isAutoProvisioningMame;
    private MameEmulationState _mameEmulationState = MameEmulationState.Stopped;

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
        ResyncMamePluginsCommand = new RelayCommand(ResyncMamePlugins);
        RemoveCachedMameVersionCommand = new RelayCommand(RemoveCachedMameVersion);
        DownloadMameRomCommand = new RelayCommand(DownloadMameRom, CanDownloadMameRom);
        ResetMameRomSourceDefaultsCommand = new RelayCommand(ResetMameRomSourceDefaults);
        CloseProjectSettingsCommand = new RelayCommand(CloseProjectSettings);
        CloseProjectCommand = new RelayCommand(CloseProject, CanCloseProject);
        ExitCommand = new RelayCommand(ExitApplication);
        StartEmulationCommand = new RelayCommand(StartEmulation, CanStartEmulation);
        StopEmulationCommand = new RelayCommand(StopEmulation, CanStopEmulation);
        PauseEmulationCommand = new RelayCommand(PauseEmulation, CanPauseEmulation);
        ResumeEmulationCommand = new RelayCommand(ResumeEmulation, CanResumeEmulation);

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

        _isLoadingPreferences = true;
        try
        {
            var preferences = _preferencesStore.Load();
            _selectedThemePreference = preferences.ThemePreference;
            _mameVersion = preferences.Mame.Version;
            _mameExecutablePath = preferences.Mame.ExecutablePath;
            _mameInstallRootDirectory = MameRuntimePaths.EnsureManagedRuntimeRootDirectory();
            _mameReleaseSource = preferences.Mame.ReleaseSource;
            _mameLuaPluginPath = MameRuntimePaths.ResolveBundledLuaPluginSourcePath();
            _mameCommandLineOverrides = preferences.Mame.CommandLineOverrides;
            _mameRomDownloadBaseUrl = preferences.Mame.RomDownloadBaseUrl;
            _mameRomArchiveExtension = preferences.Mame.RomArchiveExtension;
            _mameLocalRomSourceDirectory = preferences.Mame.LocalRomSourceDirectory;
            _mameLocalRomArchiveExtension = preferences.Mame.LocalRomArchiveExtension;
            _mameRomDownloadService.DownloadRootUrl = _mameRomDownloadBaseUrl;
            _mameRomDownloadService.ArchiveExtension = _mameRomArchiveExtension;
            _mameRomDownloadService.LocalRomSourceDirectory = _mameLocalRomSourceDirectory;
            _mameRomDownloadService.LocalRomArchiveExtension = _mameLocalRomArchiveExtension;
            _keepMameUpToDateAutomatically = preferences.Mame.KeepMameUpToDateAutomatically;
            _debugOutputLamps = preferences.Mame.DebugOutputLamps;
            _debugOutputStdIn = preferences.Mame.DebugOutputStdIn;
            _debugOutputStdOut = preferences.Mame.DebugOutputStdOut;
            _outputLog.ShowInfoLogs = preferences.OutputLog.ShowInfoLogs;
            _outputLog.ShowWarningLogs = preferences.OutputLog.ShowWarningLogs;
            _outputLog.ShowErrorLogs = preferences.OutputLog.ShowErrorLogs;
            _outputLog.AutoScroll = preferences.OutputLog.AutoScroll;
        }
        finally
        {
            _isLoadingPreferences = false;
        }
        _mameVersionCatalogService = new MameVersionCatalogService(_mameDownloadService);
        var setupValidationService = new MameSetupValidationService(_mamePluginAssetValidator, _mameVersionCatalogService);
        _mameSetupOrchestrator = new MameSetupOrchestrator(setupValidationService);
        var mameStdoutParser = new MameStdoutParser(
            new MameLampStateParser(),
            new MameLampRuntimeAdapter(
                () => OpenDocuments,
                () => DebugOutputLamps,
                message => AddOutputEntry(message, OutputLogStatus.Info),
                work =>
                {
                    var dispatcher = Application.Current.Dispatcher;
                    if (dispatcher.CheckAccess())
                    {
                        work();
                    }
                    else
                    {
                        dispatcher.Invoke(work);
                    }
                }),
            diagnosticLogger: line => AddOutputEntry(line, OutputLogStatus.Info));
        var mameProcessRunner = new MameProcessRunner(
            stdoutLogger: line => ProcessMameStdoutLine(line, mameStdoutParser),
            stdinLogger: line =>
            {
                if (DebugOutputStdIn)
                {
                    AddOutputEntry($"[MAME-STDIN] {line}", OutputLogStatus.Info);
                }
            },
            stderrLogger: line => AddOutputEntry($"[MAME-ERR] {line}", OutputLogStatus.Warning));
        _mameEmulationService = new MameEmulationService(
            new MameProcessStartInfoBuilder(),
            mameProcessRunner,
            BuildMameLaunchRequest);
        _mameEmulationService.StateChanged += (_, state) =>
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                EmulationState = state;
                AddOutputEntry($"Emulation state changed to {state}.", OutputLogStatus.Info);
                if (state is MameEmulationState.Starting or MameEmulationState.Running or MameEmulationState.Stopping or MameEmulationState.Stopped)
                {
                    AddOutputEntry($"[MAME-PROC] Active process id: {(mameProcessRunner.CurrentProcessId?.ToString() ?? "none")}", OutputLogStatus.Info);
                }
            });
        };

        if (_keepMameUpToDateAutomatically)
        {
            SyncMameVersionToLatestInstalled();
        }

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
        _ = ValidateMamePreferencesAsync();
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
    public ICommand ResyncMamePluginsCommand { get; }
    public ICommand RemoveCachedMameVersionCommand { get; }
    public ICommand DownloadMameRomCommand { get; }
    public ICommand CloseProjectSettingsCommand { get; }
    public ICommand ResetMameRomSourceDefaultsCommand { get; }
    public ICommand ApplyInspectorSummaryCommand { get; }
    public ICommand CloseProjectCommand { get; }
    public ICommand ExitCommand { get; }
    public ICommand StartEmulationCommand { get; }
    public ICommand StopEmulationCommand { get; }
    public ICommand PauseEmulationCommand { get; }
    public ICommand ResumeEmulationCommand { get; }
    public ObservableCollection<string> RecentProjects { get; }
    public ObservableCollection<DocumentTabViewModel> OpenDocuments { get; }
    public ObservableCollection<AssetBrowserItemViewModel> AssetBrowserItems { get; }
    public ObservableCollection<OutputLogEntry> OutputEntries { get; }
    public OutputLogViewModel OutputLog => _outputLog;
    public IReadOnlyList<AssetDirectoryNodeViewModel> AssetDirectoryTree => _assetBrowser.AssetDirectoryTree;


    public IReadOnlyList<ThemePreference> ThemePreferences { get; } = Enum.GetValues<ThemePreference>();
    public IReadOnlyList<string> PreferencesCategories { get; } = ["Appearance", "MAME"];
    public IReadOnlyList<FruitMachinePlatformType> FruitMachinePlatformTypes { get; } = Enum.GetValues<FruitMachinePlatformType>();


    public FruitMachinePlatformType SelectedFruitMachinePlatform
    {
        get => _selectedFruitMachinePlatform;
        set
        {
            if (!SetProperty(ref _selectedFruitMachinePlatform, value))
            {
                return;
            }

            if (LoadedProject is not null)
            {
                LoadedProject.FruitMachinePlatform = value;
                SaveLoadedProjectMetadata();
            }
        }
    }
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
    public string MameInstallRootDirectory => _mameInstallRootDirectory;
    public string MameReleaseSource { get => _mameReleaseSource; set { if (SetProperty(ref _mameReleaseSource, value)) SavePreferences(); } }
    public string MameLuaPluginPath => _mameLuaPluginPath;
    public string MameCommandLineOverrides { get => _mameCommandLineOverrides; set { if (SetProperty(ref _mameCommandLineOverrides, value)) SavePreferences(); } }
    public string MameRomDownloadBaseUrl
    {
        get => _mameRomDownloadBaseUrl;
        set
        {
            if (SetProperty(ref _mameRomDownloadBaseUrl, value))
            {
                _mameRomDownloadService.DownloadRootUrl = value;
                SavePreferences();
            }
        }
    }

    public string MameRomArchiveExtension
    {
        get => _mameRomArchiveExtension;
        set
        {
            if (SetProperty(ref _mameRomArchiveExtension, value))
            {
                _mameRomDownloadService.ArchiveExtension = value;
                SavePreferences();
            }
        }
    }

    public string MameLocalRomSourceDirectory
    {
        get => _mameLocalRomSourceDirectory;
        set
        {
            if (SetProperty(ref _mameLocalRomSourceDirectory, value))
            {
                _mameRomDownloadService.LocalRomSourceDirectory = value;
                SavePreferences();
            }
        }
    }

    public string MameLocalRomArchiveExtension
    {
        get => _mameLocalRomArchiveExtension;
        set
        {
            if (SetProperty(ref _mameLocalRomArchiveExtension, value))
            {
                _mameRomDownloadService.LocalRomArchiveExtension = value;
                SavePreferences();
            }
        }
    }
    public bool KeepMameUpToDateAutomatically
    {
        get => _keepMameUpToDateAutomatically;
        set
        {
            if (!SetProperty(ref _keepMameUpToDateAutomatically, value))
            {
                return;
            }

            if (value)
            {
                SyncMameVersionToLatestInstalled();
            }

            OnPropertyChanged(nameof(IsMameVersionEditable));
            SavePreferences();
        }
    }
    public bool IsMameVersionEditable => !KeepMameUpToDateAutomatically;
    public bool DebugOutputLamps
    {
        get => _debugOutputLamps;
        set
        {
            if (SetProperty(ref _debugOutputLamps, value))
            {
                SavePreferences();
            }
        }
    }
    public bool DebugOutputStdIn
    {
        get => _debugOutputStdIn;
        set
        {
            if (SetProperty(ref _debugOutputStdIn, value))
            {
                SavePreferences();
            }
        }
    }
    public bool DebugOutputStdOut
    {
        get => _debugOutputStdOut;
        set
        {
            if (SetProperty(ref _debugOutputStdOut, value))
            {
                SavePreferences();
            }
        }
    }
    public string MameRomName
    {
        get => _mameRomName;
        set
        {
            if (!SetProperty(ref _mameRomName, value))
            {
                return;
            }

            if (LoadedProject is null)
            {
                return;
            }

            LoadedProject.MameRomName = value;
            SaveLoadedProjectMetadata();
            RefreshMameRomStatus();
            if (DownloadMameRomCommand is RelayCommand downloadMameRomCommand)
            {
                downloadMameRomCommand.RaiseCanExecuteChanged();
            }
        }
    }
    public bool AutomaticallyDownloadMissingRoms
    {
        get => _automaticallyDownloadMissingRoms;
        set
        {
            if (!SetProperty(ref _automaticallyDownloadMissingRoms, value))
            {
                return;
            }

            if (LoadedProject is null)
            {
                return;
            }

            LoadedProject.AutomaticallyDownloadMissingRoms = value;
            SaveLoadedProjectMetadata();
        }
    }
    public string MameRomStatus
    {
        get => _mameRomStatus;
        private set => SetProperty(ref _mameRomStatus, value);
    }
    public string MameValidationSummary { get => _mameValidationSummary; private set => SetProperty(ref _mameValidationSummary, value); }
    public string MameSetupPhaseDisplay => _mameSetupState.Phase.ToString();
    public string MameSetupLatestKnownVersion => _mameSetupState.LatestKnownVersion;
    public bool IsMameSetupInProgress => _mameSetupState.IsInProgress;
    public string SelectedPreferencesCategory
    {
        get => _selectedPreferencesCategory;
        set => SetProperty(ref _selectedPreferencesCategory, value);
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
    public MameEmulationState EmulationState
    {
        get => _mameEmulationState;
        private set
        {
            if (SetProperty(ref _mameEmulationState, value))
            {
                NotifyEmulationCommands();
            }
        }
    }

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
            if (ShouldOpenAssetInEditor(asset.FullPath))
            {
                OpenDocumentFromPath(asset.FullPath);
                return;
            }

            Process.Start(new ProcessStartInfo(asset.FullPath)
            {
                UseShellExecute = true
            });
            StatusMessage = $"Opened external asset: {asset.DisplayPath}";
            AddOutputEntry($"Opened asset via Windows association: {asset.FullPath}", OutputLogStatus.Info);
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            AddOutputEntry($"Open asset failed: {ex.Message}", OutputLogStatus.Error);
            MessageBox.Show(ex.Message, "Open Asset Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private static bool ShouldOpenAssetInEditor(string path)
    {
        var extension = Path.GetExtension(path);
        return string.Equals(extension, ".panel2d", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".cabinet3d", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".machine", StringComparison.OrdinalIgnoreCase);
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
            AddOutputEntry($"Selected MAME executable: {dialog.FileName}", OutputLogStatus.Info);
            ValidateMamePreferences();
        }
    }

    private async void ValidateMamePreferences()
    {
        await ValidateMamePreferencesAsync();
    }

    private async Task ValidateMamePreferencesAsync()
    {
        AutoResolveManagedMameExecutablePath();

        _mameSetupState = new MameSetupState(MameSetupPhase.Validating, "Validating setup...", MameSetupLatestKnownVersion, true, []);
        OnPropertyChanged(nameof(MameSetupPhaseDisplay));
        OnPropertyChanged(nameof(IsMameSetupInProgress));

        var state = await _mameSetupOrchestrator.ValidateAsync(
            new MameSetupValidationRequest(
                MameExecutablePath,
                MameInstallRootDirectory,
                MameLuaPluginPath,
                MameVersion,
                MameReleaseSource),
            CancellationToken.None);

        _mameSetupState = state;
        OnPropertyChanged(nameof(MameSetupPhaseDisplay));
        OnPropertyChanged(nameof(MameSetupLatestKnownVersion));
        OnPropertyChanged(nameof(IsMameSetupInProgress));
        MameValidationSummary = state.Summary;

        if (state.Phase == MameSetupPhase.Ready)
        {
            AddOutputEntry($"MAME preferences validation passed. Latest known version: {MameSetupLatestKnownVersion}.", OutputLogStatus.Info);
            await TryAutoProvisionMameAsync(state);
        }
        else
        {
            AddOutputEntry($"MAME preferences validation requires attention: {state.Summary}", OutputLogStatus.Warning);
            if (state.Issues is not null)
            {
                foreach (var issue in state.Issues)
                {
                    AddOutputEntry($"MAME setup issue: {issue}", OutputLogStatus.Warning);
                }
            }

            await TryAutoProvisionMameAsync(state);
        }
    }

    private void AutoResolveManagedMameExecutablePath()
    {
        if (!string.IsNullOrWhiteSpace(MameExecutablePath) && File.Exists(MameExecutablePath))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(MameInstallRootDirectory) || !Directory.Exists(MameInstallRootDirectory))
        {
            return;
        }

        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(MameVersion))
        {
            var versionInstallDirectory = _mameDownloadService.GetInstallDirectory(MameInstallRootDirectory, MameVersion);
            candidates.Add(Path.Combine(versionInstallDirectory, "mame.exe"));
            candidates.Add(Path.Combine(versionInstallDirectory, $"mame{MameVersion}", "mame.exe"));
        }

        foreach (var installDirectory in Directory.EnumerateDirectories(MameInstallRootDirectory, "mame*"))
        {
            candidates.Add(Path.Combine(installDirectory, "mame.exe"));
            candidates.Add(Path.Combine(installDirectory, Path.GetFileName(installDirectory), "mame.exe"));
        }

        var resolvedPath = candidates
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .FirstOrDefault(File.Exists);

        if (string.IsNullOrWhiteSpace(resolvedPath))
        {
            return;
        }

        MameExecutablePath = resolvedPath;
        AddOutputEntry($"Resolved installed MAME executable: {resolvedPath}", OutputLogStatus.Info);
    }

    private void SyncMameVersionToLatestInstalled()
    {
        var latestInstalledVersion = TryGetLatestInstalledMameVersion();
        if (string.IsNullOrWhiteSpace(latestInstalledVersion))
        {
            return;
        }

        if (!string.Equals(MameVersion, latestInstalledVersion, StringComparison.OrdinalIgnoreCase))
        {
            MameVersion = latestInstalledVersion;
            AddOutputEntry($"Auto-update enabled. Using latest installed MAME version: {MameVersion}.", OutputLogStatus.Info);
        }
    }

    private string? TryGetLatestInstalledMameVersion()
    {
        if (string.IsNullOrWhiteSpace(MameInstallRootDirectory) || !Directory.Exists(MameInstallRootDirectory))
        {
            return null;
        }

        var discoveredVersions = new List<string>();
        foreach (var installDirectory in Directory.EnumerateDirectories(MameInstallRootDirectory, "mame*"))
        {
            var executableCandidates = new[]
            {
                Path.Combine(installDirectory, "mame.exe"),
                Path.Combine(installDirectory, Path.GetFileName(installDirectory), "mame.exe")
            };

            if (!executableCandidates.Any(File.Exists))
            {
                continue;
            }

            var directoryName = Path.GetFileName(installDirectory);
            if (!string.IsNullOrWhiteSpace(directoryName) && directoryName.StartsWith("mame", StringComparison.OrdinalIgnoreCase))
            {
                var version = directoryName[4..];
                if (!string.IsNullOrWhiteSpace(version))
                {
                    discoveredVersions.Add(version);
                }
            }
        }

        var normalizedVersions = MameVersionParsing.NormalizeSortAndDedupe(discoveredVersions);
        return normalizedVersions.FirstOrDefault();
    }

    private bool IsLatestVersionInstallNeeded(string latestKnownVersion)
    {
        if (string.IsNullOrWhiteSpace(latestKnownVersion))
        {
            return false;
        }

        var selectedOrInstalledVersion = string.IsNullOrWhiteSpace(MameVersion)
            ? TryGetLatestInstalledMameVersion()
            : MameVersion;

        if (string.IsNullOrWhiteSpace(selectedOrInstalledVersion))
        {
            return true;
        }

        var ordered = MameVersionParsing.NormalizeSortAndDedupe([latestKnownVersion, selectedOrInstalledVersion]);
        var highestKnown = ordered.FirstOrDefault();
        return string.Equals(highestKnown, latestKnownVersion, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(latestKnownVersion, MameVersionParsing.NormalizeVersion(selectedOrInstalledVersion), StringComparison.OrdinalIgnoreCase);
    }

    private async Task TryAutoProvisionMameAsync(MameSetupState state)
    {
        if (_isAutoProvisioningMame)
        {
            return;
        }

        if (!KeepMameUpToDateAutomatically)
        {
            AddOutputEntry("Auto-provision skipped: Keep MAME up to date automatically is disabled.", OutputLogStatus.Info);
            return;
        }

        if (string.IsNullOrWhiteSpace(state.LatestKnownVersion))
        {
            AddOutputEntry("Auto-provision skipped: latest MAME version is unknown.", OutputLogStatus.Warning);
            return;
        }

        var hasMissingExecutableIssue = state.Issues.Any(issue => issue.Contains("executable", StringComparison.OrdinalIgnoreCase));
        var needsLatestVersionInstall = IsLatestVersionInstallNeeded(state.LatestKnownVersion);
        if (!hasMissingExecutableIssue && !needsLatestVersionInstall)
        {
            return;
        }

        try
        {
            _isAutoProvisioningMame = true;
            if (needsLatestVersionInstall)
            {
                AddOutputEntry($"Auto-update enabled. Latest known version {state.LatestKnownVersion} is newer than installed/selected version {MameVersion}.", OutputLogStatus.Info);
            }

            MameVersion = state.LatestKnownVersion;
            AddOutputEntry($"Auto-provisioning MAME {MameVersion} in background...", OutputLogStatus.Info);

            var executablePath = await _mameDownloadService.DownloadAndExtractAsync(
                MameReleaseSource,
                MameVersion,
                MameInstallRootDirectory,
                new Progress<string>(message => AddOutputEntry($"[Auto-setup] {message}", OutputLogStatus.Info)),
                CancellationToken.None);

            MameExecutablePath = executablePath;
            SyncMameVersionToLatestInstalled();
            AddOutputEntry($"Auto-provisioning completed. Executable: {executablePath}", OutputLogStatus.Info);

            ResyncMamePlugins();
            await ValidateMamePreferencesAsync();
        }
        catch (Exception ex)
        {
            AddOutputEntry($"Auto-provisioning failed: {ex.Message}", OutputLogStatus.Warning);
        }
        finally
        {
            _isAutoProvisioningMame = false;
        }
    }


    private async void RefreshMameVersions()
    {
        try
        {
            var catalog = await _mameVersionCatalogService.GetLatestVersionAsync(CancellationToken.None);
            var source = catalog.IsFromCache ? "cache" : "network/service";
            AddOutputEntry($"Known MAME versions ({source}): {string.Join(", ", catalog.KnownVersions)}", OutputLogStatus.Info);
            if (!string.IsNullOrWhiteSpace(catalog.LatestVersion))
            {
                AddOutputEntry($"Latest known MAME version: {catalog.LatestVersion}", OutputLogStatus.Info);
            }
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


    private void ResyncMamePlugins()
    {
        try
        {
            var copied = _mamePluginDeploymentService.SyncPluginFiles(MameLuaPluginPath, MameExecutablePath);
            AddOutputEntry($"Re-synced Oasis Lua plugins into active MAME install ({copied} files copied).", OutputLogStatus.Info);
            ValidateMamePreferences();
        }
        catch (Exception ex)
        {
            AddOutputEntry($"Failed to re-sync Oasis Lua plugins: {ex.Message}", OutputLogStatus.Warning);
        }
    }
    private void RemoveCachedMameVersion()
    {
        try
        {
            var removed = _mameDownloadService.RemoveCachedVersion(MameInstallRootDirectory, MameVersion);
            AddOutputEntry(removed
                ? $"Removed cached MAME version {MameVersion}."
                : $"No cached MAME version directory found for {MameVersion}.", removed ? OutputLogStatus.Info : OutputLogStatus.Warning);

            if (KeepMameUpToDateAutomatically)
            {
                SyncMameVersionToLatestInstalled();
            }
        }
        catch (Exception ex)
        {
            AddOutputEntry($"Failed to remove cached MAME version: {ex.Message}", OutputLogStatus.Warning);
        }
    }
    private void SavePreferences()
    {
        var existingPreferences = _preferencesStore.Load();
        _preferencesStore.Save(new EditorPreferences
        {
            ThemePreference = SelectedThemePreference,
            Mame = new MamePreferences
            {
                Version = MameVersion,
                ExecutablePath = MameExecutablePath,
                ReleaseSource = MameReleaseSource,
                CommandLineOverrides = MameCommandLineOverrides,
                KeepMameUpToDateAutomatically = KeepMameUpToDateAutomatically,
                DebugOutputLamps = DebugOutputLamps,
                DebugOutputStdIn = DebugOutputStdIn,
                DebugOutputStdOut = DebugOutputStdOut,
                RomDownloadBaseUrl = MameRomDownloadBaseUrl,
                RomArchiveExtension = MameRomArchiveExtension,
                LocalRomSourceDirectory = MameLocalRomSourceDirectory,
                LocalRomArchiveExtension = MameLocalRomArchiveExtension
            },
            OutputLog = new OutputLogPreferences
            {
                ShowInfoLogs = _outputLog.ShowInfoLogs,
                ShowWarningLogs = _outputLog.ShowWarningLogs,
                ShowErrorLogs = _outputLog.ShowErrorLogs,
                AutoScroll = _outputLog.AutoScroll
            },
            ProjectWindowStates = existingPreferences.ProjectWindowStates
        });
    }

    private void ProcessMameStdoutLine(string line, IMameStdoutParser parser)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        var normalized = line.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Replace("\\n", "\n", StringComparison.Ordinal);
        var segments = normalized.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0)
        {
            return;
        }

        foreach (var segment in segments)
        {
            if (DebugOutputStdOut)
            {
                AddOutputEntry($"[MAME-STDOUT] {segment}", OutputLogStatus.Info);
            }

            parser.ProcessLine(segment);
        }
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

        if (CanStopEmulation())
        {
            StopEmulation();
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

    private void ExitApplication()
    {
        if (CanStopEmulation())
        {
            StopEmulation();
        }

        Application.Current.Shutdown();
    }

    private bool CanStartEmulation()
    {
        return MameEmulationCommandStateEvaluator.Evaluate(HasLoadedProject, EmulationState).CanStart;
    }

    private async void StartEmulation()
    {
        if (!CanStartEmulation())
        {
            return;
        }

        EnsureOwnerWindowTaskbarVisibility();

        AddOutputEntry("Emulation start requested.", OutputLogStatus.Info);
        try
        {
            await _mameEmulationService.StartAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            AddOutputEntry($"Emulation failed to start: {ex.Message}", OutputLogStatus.Error);
        }
    }

    private bool CanStopEmulation()
    {
        return MameEmulationCommandStateEvaluator.Evaluate(HasLoadedProject, EmulationState).CanStop;
    }

    private async void StopEmulation()
    {
        if (!CanStopEmulation())
        {
            return;
        }

        AddOutputEntry("Emulation stop requested.", OutputLogStatus.Info);
        try
        {
            await _mameEmulationService.StopAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            AddOutputEntry($"Emulation failed to stop cleanly: {ex.Message}", OutputLogStatus.Error);
        }
    }

    private void EnsureOwnerWindowTaskbarVisibility()
    {
        if (!_ownerWindow.ShowInTaskbar)
        {
            _ownerWindow.ShowInTaskbar = true;
            AddOutputEntry("[UI] Re-enabled editor ShowInTaskbar before emulation start.", OutputLogStatus.Warning);
        }

        if (!_ownerWindow.IsVisible)
        {
            _ownerWindow.Show();
            AddOutputEntry("[UI] Re-shown editor window before emulation start.", OutputLogStatus.Warning);
        }
    }

    private bool CanPauseEmulation()
    {
        return MameEmulationCommandStateEvaluator.Evaluate(HasLoadedProject, EmulationState).CanPause;
    }

    private async void PauseEmulation()
    {
        if (!CanPauseEmulation())
        {
            return;
        }

        AddOutputEntry("Emulation pause requested.", OutputLogStatus.Info);
        await _mameEmulationService.PauseAsync(CancellationToken.None);
    }

    private bool CanResumeEmulation()
    {
        return MameEmulationCommandStateEvaluator.Evaluate(HasLoadedProject, EmulationState).CanResume;
    }

    private async void ResumeEmulation()
    {
        if (!CanResumeEmulation())
        {
            return;
        }

        AddOutputEntry("Emulation resume requested.", OutputLogStatus.Info);
        await _mameEmulationService.ResumeAsync(CancellationToken.None);
    }

    private MameProcessLaunchRequest? BuildMameLaunchRequest()
    {
        if (!HasLoadedProject || string.IsNullOrWhiteSpace(MameExecutablePath) || string.IsNullOrWhiteSpace(MameRomName))
        {
            return null;
        }

        return new MameProcessLaunchRequest(
            MameExecutablePath,
            MameRomName,
            MameRuntimePaths.EnsureManagedRomRootDirectory(),
            MameRuntimePaths.ResolveBundledLuaPluginSourcePath(),
            MameCommandLineOverrides);
    }

    private void LoadStartupProject(string startupProjectFilePath)
    {
        var project = LoadProjectFromFile(startupProjectFilePath);
        LoadedProject = project;
        SelectedFruitMachinePlatform = project.FruitMachinePlatform;
        MameRomName = project.MameRomName;
        AutomaticallyDownloadMissingRoms = project.AutomaticallyDownloadMissingRoms;
        RefreshMameRomStatus();
        PanelElementFactory.ProjectDirectoryPath = project.ProjectDirectory;
        ProjectFilePath = project.ProjectFilePath;
        UpdateRecentProjects(project.ProjectFilePath);
        _assetBrowser.RefreshAssetBrowser();
        StatusMessage = $"Project opened: {project.Name} ({project.ProjectFilePath})";
        AddOutputEntry($"Loaded startup project '{project.Name}' from {project.ProjectFilePath}", OutputLogStatus.Info);
    }

    private EditorProject LoadProjectFromFile(string projectFilePath)
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
        var fruitMachinePlatform = ResolveFruitMachinePlatform(projectDocument.RootElement);
        var mameRomName = ResolveMameRomName(projectDocument.RootElement);
        var automaticallyDownloadMissingRoms = ResolveAutomaticallyDownloadMissingRoms(projectDocument.RootElement);

        return new EditorProject
        {
            Name = openedProjectName,
            ProjectFilePath = projectFilePath,
            ProjectDirectory = projectDirectory,
            AssetsDirectory = assetsDirectory,
            MachinesDirectory = machinesDirectory,
            GeneratedDirectory = generatedDirectory,
            FruitMachinePlatform = fruitMachinePlatform,
            MameRomName = mameRomName,
            AutomaticallyDownloadMissingRoms = automaticallyDownloadMissingRoms
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


    private void SaveLoadedProjectMetadata()
    {
        if (LoadedProject is null)
        {
            return;
        }

        var projectFilePath = LoadedProject.ProjectFilePath;
        var projectJson = File.ReadAllText(projectFilePath);
        using var projectDocument = JsonDocument.Parse(projectJson);

        var tempPath = Path.GetTempFileName();
        try
        {
            using (var outputStream = File.Create(tempPath))
            using (var writer = new Utf8JsonWriter(outputStream, new JsonWriterOptions { Indented = true }))
            {
                writer.WriteStartObject();
                var wroteProjectSettings = false;

                foreach (var property in projectDocument.RootElement.EnumerateObject())
                {
                    if (property.NameEquals("project_settings"))
                    {
                        wroteProjectSettings = true;
                        writer.WritePropertyName("project_settings");
                        WriteProjectSettings(writer, property.Value, LoadedProject.FruitMachinePlatform, LoadedProject.MameRomName, LoadedProject.AutomaticallyDownloadMissingRoms);
                        continue;
                    }

                    property.WriteTo(writer);
                }

                if (!wroteProjectSettings)
                {
                    writer.WritePropertyName("project_settings");
                    writer.WriteStartObject();
                    writer.WriteString("FruitMachine_Platform", LoadedProject.FruitMachinePlatform.ToString());
                    writer.WriteString("MameRomName", LoadedProject.MameRomName);
                    writer.WriteBoolean("AutomaticallyDownloadMissingRoms", LoadedProject.AutomaticallyDownloadMissingRoms);
                    writer.WriteEndObject();
                }

                writer.WriteEndObject();
            }

            File.Copy(tempPath, projectFilePath, overwrite: true);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    private static void WriteProjectSettings(Utf8JsonWriter writer, JsonElement existingProjectSettings, FruitMachinePlatformType platform, string mameRomName, bool automaticallyDownloadMissingRoms)
    {
        writer.WriteStartObject();
        var wrotePlatform = false;
        var wroteMameRomName = false;
        var wroteAutomaticallyDownloadMissingRoms = false;

        foreach (var settingProperty in existingProjectSettings.EnumerateObject())
        {
            if (settingProperty.NameEquals("FruitMachine_Platform"))
            {
                writer.WriteString("FruitMachine_Platform", platform.ToString());
                wrotePlatform = true;
                continue;
            }
            if (settingProperty.NameEquals("MameRomName"))
            {
                writer.WriteString("MameRomName", mameRomName);
                wroteMameRomName = true;
                continue;
            }
            if (settingProperty.NameEquals("AutomaticallyDownloadMissingRoms"))
            {
                writer.WriteBoolean("AutomaticallyDownloadMissingRoms", automaticallyDownloadMissingRoms);
                wroteAutomaticallyDownloadMissingRoms = true;
                continue;
            }

            settingProperty.WriteTo(writer);
        }

        if (!wrotePlatform)
        {
            writer.WriteString("FruitMachine_Platform", platform.ToString());
        }
        if (!wroteMameRomName)
        {
            writer.WriteString("MameRomName", mameRomName);
        }
        if (!wroteAutomaticallyDownloadMissingRoms)
        {
            writer.WriteBoolean("AutomaticallyDownloadMissingRoms", automaticallyDownloadMissingRoms);
        }

        writer.WriteEndObject();
    }

    private FruitMachinePlatformType ResolveFruitMachinePlatform(JsonElement root)
    {
        if (!root.TryGetProperty("project_settings", out var projectSettingsElement)
            || !projectSettingsElement.TryGetProperty("FruitMachine_Platform", out var platformElement))
        {
            return FruitMachinePlatformType.None;
        }

        var rawPlatform = platformElement.GetString();
        if (string.IsNullOrWhiteSpace(rawPlatform))
        {
            return FruitMachinePlatformType.None;
        }

        if (Enum.TryParse<FruitMachinePlatformType>(rawPlatform, true, out var parsed))
        {
            return parsed;
        }

        AddOutputEntry($"Unknown FruitMachine_Platform '{rawPlatform}' in project settings; defaulting to None.", OutputLogStatus.Warning);
        return FruitMachinePlatformType.None;
    }

    private static string ResolveMameRomName(JsonElement root)
    {
        if (!root.TryGetProperty("project_settings", out var projectSettingsElement)
            || !projectSettingsElement.TryGetProperty("MameRomName", out var romNameElement))
        {
            return string.Empty;
        }

        return romNameElement.GetString() ?? string.Empty;
    }

    private static bool ResolveAutomaticallyDownloadMissingRoms(JsonElement root)
    {
        if (!root.TryGetProperty("project_settings", out var projectSettingsElement)
            || !projectSettingsElement.TryGetProperty("AutomaticallyDownloadMissingRoms", out var autoDownloadElement))
        {
            return true;
        }

        return autoDownloadElement.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => true
        };
    }

    private bool CanDownloadMameRom() => LoadedProject is not null && !string.IsNullOrWhiteSpace(MameRomName) && !_isMameRomDownloadInProgress;

    private void ResetMameRomSourceDefaults()
    {
        MameRomDownloadBaseUrl = MameRomDownloadService.DefaultDownloadRootUrl;
        MameRomArchiveExtension = MameRomDownloadService.DefaultArchiveExtension;
        MameLocalRomSourceDirectory = string.Empty;
        MameLocalRomArchiveExtension = MameRomDownloadService.DefaultArchiveExtension;
        AddOutputEntry("Reset ROM source preferences to defaults.", OutputLogStatus.Info);
    }

    private void DownloadMameRom()
    {
        if (_isMameRomDownloadInProgress)
        {
            return;
        }

        if (LoadedProject is null || string.IsNullOrWhiteSpace(MameRomName))
        {
            MameRomStatus = "Missing";
            AddOutputEntry("MAME ROM download skipped: no ROM name configured.", OutputLogStatus.Warning);
            return;
        }

        _ = DownloadMameRomAsync(MameRomName.Trim());
    }

    private async Task DownloadMameRomAsync(string romName)
    {
        _isMameRomDownloadInProgress = true;
        if (DownloadMameRomCommand is RelayCommand downloadMameRomCommand)
        {
            downloadMameRomCommand.RaiseCanExecuteChanged();
        }

        MameRomStatus = "Downloading";
        AddOutputEntry($"MAME ROM download requested for '{romName}'.", OutputLogStatus.Info);
        var downloadUrl = _mameRomDownloadService.BuildDownloadUrl(romName);
        try
        {
            var localSourcePath = _mameRomDownloadService.GetLocalRomArchivePath(romName);
            var archivePath = await _mameRomDownloadService.DownloadRomAsync(romName, CancellationToken.None);
            MameRomStatus = "Installed";
            if (!string.IsNullOrWhiteSpace(localSourcePath) && File.Exists(localSourcePath))
            {
                AddOutputEntry($"MAME ROM copied from local source '{localSourcePath}' to '{archivePath}'.", OutputLogStatus.Info);
            }
            else
            {
                AddOutputEntry($"MAME ROM downloaded to '{archivePath}'.", OutputLogStatus.Info);
            }
        }
        catch (Exception ex)
        {
            MameRomStatus = "Failed";
            AddOutputEntry($"MAME ROM download failed for '{romName}' from '{downloadUrl}': {ex.Message}", OutputLogStatus.Error);
        }
        finally
        {
            _isMameRomDownloadInProgress = false;
            if (DownloadMameRomCommand is RelayCommand updatedDownloadMameRomCommand)
            {
                updatedDownloadMameRomCommand.RaiseCanExecuteChanged();
            }
        }
    }

    private void RefreshMameRomStatus()
    {
        var romName = MameRomName.Trim();
        if (string.IsNullOrWhiteSpace(romName))
        {
            MameRomStatus = "Missing";
            return;
        }

        MameRomStatus = _mameRomDownloadService.IsRomInstalled(romName) ? "Installed" : "Missing";
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
        if (e.PropertyName is nameof(OutputLogViewModel.ShowInfoLogs)
            or nameof(OutputLogViewModel.ShowWarningLogs)
            or nameof(OutputLogViewModel.ShowErrorLogs)
            or nameof(OutputLogViewModel.AutoScroll))
        {
            if (!_isLoadingPreferences)
            {
                SavePreferences();
            }

            return;
        }

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

        NotifyEmulationCommands();
        NotifyUndoRedoStateChanged();
        _assetBrowser.NotifyRefreshCommand();
    }

    private void NotifyEmulationCommands()
    {
        if (StartEmulationCommand is RelayCommand startEmulationCommand)
        {
            startEmulationCommand.RaiseCanExecuteChanged();
        }

        if (StopEmulationCommand is RelayCommand stopEmulationCommand)
        {
            stopEmulationCommand.RaiseCanExecuteChanged();
        }

        if (PauseEmulationCommand is RelayCommand pauseEmulationCommand)
        {
            pauseEmulationCommand.RaiseCanExecuteChanged();
        }

        if (ResumeEmulationCommand is RelayCommand resumeEmulationCommand)
        {
            resumeEmulationCommand.RaiseCanExecuteChanged();
        }
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
