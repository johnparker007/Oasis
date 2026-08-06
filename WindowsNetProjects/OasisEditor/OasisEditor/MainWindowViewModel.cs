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
using OasisEditor.Features.LayoutImport;
using OasisEditor.Features.FmlImport;
using OasisEditor.Features.CabinetEditor.Models;
using EditorCommands = OasisEditor.Commands;
using OasisEditor.Views;
using OasisEditor.Rendering;
using OasisEditor.Progress;

namespace OasisEditor;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private static readonly bool kDebugSkiaPerformanceOutput = false;
    private readonly RecentProjectsStore _recentProjectsStore = new();
    private readonly IApplicationThemeService _applicationThemeService;
    private readonly EditorPreferencesStore _preferencesStore;
    private readonly Window _ownerWindow;
    private string _projectFilePath = string.Empty;
    private string _statusMessage = "Create a new project to get started.";
    private EditorProject? _loadedProject;
    private DocumentTabViewModel? _selectedDocument;
    private ThemePreference _selectedThemePreference;
    private string _fabricRuntimeLibraryPath = string.Empty;
    private string _productionAmberLibraryPath = string.Empty;
    private string _mpu5AmberLibraryPath = string.Empty;
    private int _system6AudioBufferLengthMilliseconds = NativeEmulationPreferences.DefaultAudioBufferLengthMilliseconds;
    private string _lastMfmeFmlImportDirectory = string.Empty;
    private FaceGenerationSettingsModel _defaultFaceGenerationSettings = FaceGenerationSettingsModel.Default;
    private bool _showFaceGenerationSettingsBeforeRegenerate = true;
    private string _oasisPlayerExecutablePath = string.Empty;
    private bool _oasisPlayerFullscreen;
    private int _oasisPlayerPreviewWidth = OasisPlayerLaunchService.DefaultPreviewWidth;
    private int _oasisPlayerPreviewHeight = OasisPlayerLaunchService.DefaultPreviewHeight;
    private string _selectedPreferencesCategory = "Appearance";
    private string _selectedProjectSettingsCategory = "General";
    private string _selectedNativeProjectSettingsTab = "ROMS";
    private FruitMachinePlatformType _selectedFruitMachinePlatform = FruitMachinePlatformType.None;
    private bool _automaticallyDownloadMissingRoms = true;
    private string _system6ProgramRom1Path = string.Empty;
    private string _system6ProgramRom2Path = string.Empty;
    private string _system6ProgramRom3Path = string.Empty;
    private string _system6ProgramRom4Path = string.Empty;
    private string _system6SoundRom1Path = string.Empty;
    private string _system6SoundRom2Path = string.Empty;
    private string _system6SoundRom3Path = string.Empty;
    private string _system6SoundRom4Path = string.Empty;
    private bool _system6FlashSwitch;
    private int _system6PercentSwitchValue = System6NativeRomSettings.DefaultPercentSwitchValue;
    private AmberCoinCommunicationStyle _system6CoinCommunicationStyle = AmberCoinCommunicationStyle.Parallel;
    private bool _system6CoinCommunicationInvert;
    private uint _system6CoinPulseCycles = 800_000;
    private bool _system6CoinEdcEnabled;
    private string _system6NativeRomStatus = "Program ROM 1 and 2 are required for Fabric Amber launch.";
    private ObservableCollection<System6ReelOptoSettingsViewModel> _system6ReelOptos = [];
    private ObservableCollection<System6CoinSettingsViewModel> _system6Coins = [];
    private bool _isFmlImportInProgress;
    private bool _isEditorProgressVisible;
    private bool _isEditorProgressIndeterminate;
    private double _editorProgressPercent;
    private string _editorProgressMessage = string.Empty;
    private readonly AssetBrowserViewModel _assetBrowser;
    private readonly OutputLogViewModel _outputLog;
    private readonly InspectorViewModel _inspector;
    private readonly HierarchyViewModel _hierarchy;
    private readonly DocumentWorkspaceViewModel _documentWorkspace;
    private readonly ActiveDocumentContextService _activeDocumentContext;
    private readonly MachineRuntimeStateStore _machineRuntimeStates;
    private readonly HierarchyPanelCommandService _hierarchyPanelCommands;
    private bool _isRefreshingHierarchy;
    private readonly IFmlImportService _fmlImportService = new FmlImportService();
    private readonly Automation.IDocumentSaveService _documentSaveService = new Automation.DocumentSaveService();
    private readonly IProgressDialogService _progressDialogService;
    private bool _isLoadingPreferences;
    private readonly IEmulationBackendFactory _emulationBackendFactory;
    private readonly IMachineLampRuntimeAdapter _lampRuntimeAdapter;
    private readonly IMachineReelRuntimeAdapter _reelRuntimeAdapter;
    private readonly IMachineSegmentRuntimeAdapter _segmentRuntimeAdapter;
    private IEmulationBackend? _activeEmulationBackend;
    private EmulationBackendState _emulationState = EmulationBackendState.Stopped;
    private readonly IInputMapDiagnosticsService _inputMapDiagnosticsService = new InputMapDiagnosticsService();
    private readonly OasisPlayerPreviewService _oasisPlayerPreviewService = new();
    private IReadOnlyList<InputMapDiagnostic> _inputMapDiagnostics = [];
    private PlayViewInputRouter? _playViewInputRouter;
    private PlayViewInputDispatcher? _playViewInputDispatcher;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action<EditorToolWindowId>? ToolWindowOpenRequested;
    public event Action<EditorToolWindowId>? ToolWindowCloseRequested;

    public bool IsEditorProgressOperationActive => _progressDialogService.IsOperationActive;

    public Task RunEditorProgressAsync(
        EditorProgressRequest request,
        Func<IEditorProgressReporter, CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        return _progressDialogService.RunAsync(request, operation, cancellationToken);
    }

    public void ReportEditorOperationError(string message, OutputLogStatus status)
    {
        StatusMessage = message;
        AddOutputEntry(message, status);
    }

    public MainWindowViewModel(
        IApplicationThemeService applicationThemeService,
        EditorPreferencesStore preferencesStore,
        Window ownerWindow,
        string startupProjectFilePath)
    {
        _applicationThemeService = applicationThemeService;
        _preferencesStore = preferencesStore;
        _ownerWindow = ownerWindow;
        _progressDialogService = new WpfProgressDialogService(() => _ownerWindow, _ownerWindow.Dispatcher);

        if (string.IsNullOrWhiteSpace(startupProjectFilePath))
        {
            throw new InvalidOperationException("Editor shell requires an active loaded project.");
        }

        OpenUntitledDocumentCommand = new RelayCommand(OpenUntitledDocument, CanOpenUntitledDocument);
        OpenPanel2DStubCommand = new RelayCommand(OpenPanel2DStubDocument, CanOpenUntitledDocument);
        OpenFaceStubCommand = new RelayCommand(OpenFaceStubDocument, CanOpenUntitledDocument);
        AddFaceSourceShapeCommand = new RelayCommand(AddFaceSourceShape, CanAddFaceSourceShape);
        GenerateFaceFromSourceShapeCommand = new RelayCommand(GenerateFaceFromSourceShape, CanGenerateFaceFromSourceShape);
        RegenerateFaceCommand = new RelayCommand(RegenerateFace, CanRegenerateFace);
        OpenFaceGenerationSettingsCommand = new RelayCommand(OpenFaceGenerationSettings, CanOpenFaceGenerationSettings);
        ValidateFaceCommand = new RelayCommand(ValidateFace, CanValidateFace);
        OpenSourcePanel2DCommand = new RelayCommand(OpenSourcePanel2D, CanOpenSourcePanel2D);
        OpenCabinet3DStubCommand = new RelayCommand(OpenCabinet3DStubDocument, CanOpenUntitledDocument);
        OpenMachineStubCommand = new RelayCommand(OpenMachineStubDocument, CanOpenUntitledDocument);
        ImportMfmeFmlCommand = new RelayCommand(ImportMfmeFml, CanImportMfmeFml);
        ImportGlbModelCommand = new RelayCommand(ImportGlbModel, CanImportGlbModel);
        BuildOasisPlayerMachineCommand = new RelayCommand(BuildOasisPlayerMachine, CanBuildOasisPlayerMachine);
        PreviewInOasisPlayerCommand = new RelayCommand(PreviewInOasisPlayer, CanBuildOasisPlayerMachine);
        SaveSelectedDocumentCommand = new RelayCommand(SaveSelectedDocument, CanSaveSelectedDocument);
        CloseSelectedDocumentCommand = new RelayCommand(CloseSelectedDocument, CanCloseSelectedDocument);
        OpenPreferencesCommand = new RelayCommand(OpenPreferences);
        OpenProjectSettingsCommand = new RelayCommand(OpenProjectSettings);
        OpenInputMapCommand = new RelayCommand(OpenInputMap);
        OpenPlayViewCommand = new RelayCommand(OpenPlayView);
        ClosePreferencesCommand = new RelayCommand(ClosePreferences);
        BrowseOasisPlayerExecutableCommand = new RelayCommand(BrowseOasisPlayerExecutable);
        BrowseSystem6ProgramRom1Command = new RelayCommand(() => BrowseSystem6RomPath(1, true));
        BrowseSystem6ProgramRom2Command = new RelayCommand(() => BrowseSystem6RomPath(2, true));
        BrowseSystem6ProgramRom3Command = new RelayCommand(() => BrowseSystem6RomPath(3, true));
        BrowseSystem6ProgramRom4Command = new RelayCommand(() => BrowseSystem6RomPath(4, true));
        BrowseSystem6SoundRom1Command = new RelayCommand(() => BrowseSystem6RomPath(1, false));
        BrowseSystem6SoundRom2Command = new RelayCommand(() => BrowseSystem6RomPath(2, false));
        BrowseSystem6SoundRom3Command = new RelayCommand(() => BrowseSystem6RomPath(3, false));
        BrowseSystem6SoundRom4Command = new RelayCommand(() => BrowseSystem6RomPath(4, false));
        ResetSystem6ReelOptosCommand = new RelayCommand(ResetSystem6ReelOptosToDefaults);
        CloseProjectSettingsCommand = new RelayCommand(CloseProjectSettings);
        CloseProjectCommand = new RelayCommand(CloseProject, CanCloseProject);
        ExitCommand = new RelayCommand(ExitApplication);
        StartEmulationCommand = new RelayCommand(StartEmulation, CanStartEmulation);
        StopEmulationCommand = new RelayCommand(StopEmulation, CanStopEmulation);
        TogglePauseEmulationCommand = new RelayCommand(TogglePauseEmulation, CanTogglePauseEmulation);
        SoftResetEmulationCommand = new RelayCommand(SoftResetEmulation, CanResetEmulation);
        HardResetEmulationCommand = new RelayCommand(HardResetEmulation, CanResetEmulation);

        _outputLog = new OutputLogViewModel();
        _outputLog.PropertyChanged += OnOutputLogPropertyChanged;
        SkiaRenderDiagnostics.IsEnabled = kDebugSkiaPerformanceOutput;
        if (kDebugSkiaPerformanceOutput)
        {
            SkiaRenderDiagnostics.ReportReady += message => AddOutputEntry(message, OutputLogStatus.Info);
        }
        _activeDocumentContext = new ActiveDocumentContextService();
        _machineRuntimeStates = new MachineRuntimeStateStore();
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
            () => _assetBrowser.SelectedDirectory,
            () => SelectedDocument,
            () => OpenDocuments,
            () => LoadedProject,
            _activeDocumentContext,
            ExecuteDocumentCanvasCommand,
            ApplyInspectorSummary,
            GenerateFaceFromSourceShapeCommand);
        _hierarchy = new HierarchyViewModel(
            () => SelectedDocument,
            [new Panel2DHierarchyProvider(), new FaceHierarchyProvider()]);
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
            _fabricRuntimeLibraryPath = preferences.NativeEmulation.FabricRuntimeLibraryPath;
            _productionAmberLibraryPath = preferences.NativeEmulation.ProductionAmberLibraryPath;
            _mpu5AmberLibraryPath = preferences.NativeEmulation.Mpu5AmberLibraryPath;
            _system6AudioBufferLengthMilliseconds = NormalizeSystem6AudioBufferLengthMilliseconds(preferences.NativeEmulation.AudioBufferLengthMilliseconds);
            _oasisPlayerExecutablePath = preferences.Player.ExecutablePath;
            _oasisPlayerFullscreen = preferences.Player.Fullscreen;
            _oasisPlayerPreviewWidth = preferences.Player.PreviewWidth;
            _oasisPlayerPreviewHeight = preferences.Player.PreviewHeight;
            _lastMfmeFmlImportDirectory = preferences.LastMfmeFmlImportDirectory;
            _defaultFaceGenerationSettings = preferences.FaceGeneration.ToSettings();
            _showFaceGenerationSettingsBeforeRegenerate = preferences.FaceGeneration.ShowFaceGenerationSettingsBeforeRegenerate;
            _outputLog.ShowInfoLogs = preferences.OutputLog.ShowInfoLogs;
            _outputLog.ShowWarningLogs = preferences.OutputLog.ShowWarningLogs;
            _outputLog.ShowErrorLogs = preferences.OutputLog.ShowErrorLogs;
            _outputLog.AutoScroll = preferences.OutputLog.AutoScroll;
            _outputLog.SearchText = preferences.OutputLog.SearchText;
        }
        finally
        {
            _isLoadingPreferences = false;
        }
        _lampRuntimeAdapter = new MachineLampRuntimeAdapter(
            () => OpenDocuments, () => false, _ => { }, DispatchToUiThread);
        _reelRuntimeAdapter = new MachineReelRuntimeAdapter(
            () => OpenDocuments, () => SelectedFruitMachinePlatform, () => false, _ => { }, DispatchToUiThread,
            reelId => System6ReelOptos.FirstOrDefault(reel => reel.ReelIndex == reelId)?.Steps
                ?? System6ReelOptoSettings.DefaultSteps);
        _segmentRuntimeAdapter = new MachineSegmentRuntimeAdapter(
            () => OpenDocuments,
            DispatchToUiThread,
            () => SelectedFruitMachinePlatform);
        _emulationBackendFactory = new EmulationBackendFactory(
            () => FabricRuntimeLibraryPath, () => ProductionAmberLibraryPath,
            () => System6AudioBufferLengthMilliseconds,
            errorLogger: message => AddOutputEntry(message, OutputLogStatus.Error),
            infoLogger: message => AddOutputEntry(message, OutputLogStatus.Info),
            mpu5AmberPathProvider: () => Mpu5AmberLibraryPath);

        RecentProjects = new ObservableCollection<string>(_recentProjectsStore.Load());
        OpenDocuments = new ObservableCollection<DocumentTabViewModel>();
        OpenDocuments.CollectionChanged += OnOpenDocumentsChanged;
        _documentWorkspace = new DocumentWorkspaceViewModel(
            () => _loadedProject,
            value => LoadedProject = value,
            OpenDocuments,
            () => _selectedDocument,
            value => SelectedDocument = value,
            NotifyUndoRedoStateChanged,
            value => StatusMessage = value,
            AddOutputEntry,
            _machineRuntimeStates,
            new Automation.Panel2DDocumentCreationService(),
            documentId =>
            {
                _activeDocumentContext.ClearDocumentState(documentId);
                _machineRuntimeStates.ClearDocumentState(documentId);
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
    public ICommand OpenFaceStubCommand { get; }
    public ICommand AddFaceSourceShapeCommand { get; }
    public ICommand GenerateFaceFromSourceShapeCommand { get; }
    public ICommand RegenerateFaceCommand { get; }
    public ICommand OpenFaceGenerationSettingsCommand { get; }
    public ICommand ValidateFaceCommand { get; }
    public ICommand OpenSourcePanel2DCommand { get; }
    public ICommand OpenCabinet3DStubCommand { get; }
    public ICommand OpenMachineStubCommand { get; }
    public ICommand ImportMfmeFmlCommand { get; }
    public ICommand ImportGlbModelCommand { get; }
    public ICommand BuildOasisPlayerMachineCommand { get; }
    public ICommand PreviewInOasisPlayerCommand { get; }
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
    public ICommand OpenInputMapCommand { get; }
    public ICommand OpenPlayViewCommand { get; }
    public ICommand ClosePreferencesCommand { get; }
    public ICommand BrowseOasisPlayerExecutableCommand { get; }
    public ICommand BrowseSystem6ProgramRom1Command { get; }
    public ICommand BrowseSystem6ProgramRom2Command { get; }
    public ICommand BrowseSystem6ProgramRom3Command { get; }
    public ICommand BrowseSystem6ProgramRom4Command { get; }
    public ICommand BrowseSystem6SoundRom1Command { get; }
    public ICommand BrowseSystem6SoundRom2Command { get; }
    public ICommand BrowseSystem6SoundRom3Command { get; }
    public ICommand BrowseSystem6SoundRom4Command { get; }
    public ICommand CloseProjectSettingsCommand { get; }
    public ICommand ResetSystem6ReelOptosCommand { get; }
    public ICommand ApplyInspectorSummaryCommand { get; }
    public ICommand CloseProjectCommand { get; }
    public ICommand ExitCommand { get; }
    public ICommand StartEmulationCommand { get; }
    public ICommand StopEmulationCommand { get; }
    public ICommand TogglePauseEmulationCommand { get; }
    public ICommand SoftResetEmulationCommand { get; }
    public ICommand HardResetEmulationCommand { get; }
    public ObservableCollection<string> RecentProjects { get; }
    public ObservableCollection<DocumentTabViewModel> OpenDocuments { get; }
    public ObservableCollection<AssetBrowserItemViewModel> AssetBrowserItems { get; }
    public ObservableCollection<OutputLogEntry> OutputEntries { get; }
    public OutputLogViewModel OutputLog => _outputLog;
    public IReadOnlyList<AssetDirectoryNodeViewModel> AssetDirectoryTree => _assetBrowser.AssetDirectoryTree;


    public IReadOnlyList<ThemePreference> ThemePreferences { get; } = Enum.GetValues<ThemePreference>();
    public IReadOnlyList<string> PreferencesCategories { get; } = ["Appearance", "Player", "Fabric Emulation"];
    public IReadOnlyList<string> ProjectSettingsCategories { get; } = ["General", "Impact / Fabric"];
    public IReadOnlyList<string> NativeProjectSettingsTabs { get; } = ["ROMS", "Stake/Prize", "Reels", "Coins"];
    public IReadOnlyList<FruitMachinePlatformType> FruitMachinePlatformTypes { get; } = Enum.GetValues<FruitMachinePlatformType>();
    public IReadOnlyList<InputDefinitionModel> InputDefinitions => LoadedProject?.InputDefinitions ?? [];
    public IReadOnlyList<InputMapDiagnostic> InputMapDiagnostics
    {
        get => _inputMapDiagnostics;
        private set => SetProperty(ref _inputMapDiagnostics, value);
    }

    public int InputMapWarningCount => InputMapDiagnostics.Count(d => d.Severity == InputMapDiagnosticSeverity.Warning);
    public bool HasInputMapDiagnostics => InputMapDiagnostics.Count > 0;

    public int DeleteInputDefinitions(IReadOnlyCollection<InputDefinitionModel> selectedInputs)
    {
        if (LoadedProject is null || selectedInputs.Count == 0)
        {
            return 0;
        }

        // TODO: Wrap this project-level mutation when the Editor exposes project-scoped undo history;
        // the existing Undo/Redo service is scoped exclusively to the active content document.
        var deletedCount = InputMapDeletionService.DeleteSelected(LoadedProject.InputDefinitions, selectedInputs);
        if (deletedCount == 0)
        {
            return 0;
        }

        SaveLoadedProjectMetadata();
        SelectedDocument?.MarkDirty();
        OnPropertyChanged(nameof(InputDefinitions));
        RefreshInputMapDiagnostics();
        AddOutputEntry($"Deleted {deletedCount} input definition(s).", OutputLogStatus.Info);
        return deletedCount;
    }


    public FruitMachinePlatformType SelectedFruitMachinePlatform
    {
        get => _selectedFruitMachinePlatform;
        set
        {
            if (!SetProperty(ref _selectedFruitMachinePlatform, value))
            {
                return;
            }

            foreach (var document in OpenDocuments)
            {
                document.RuntimeState.FruitMachinePlatform = value;
                var faceReelObjectIds = document.GetFaceElements()
                    .OfType<FaceReelDisplayElement>()
                    .Select(element => element.ObjectId)
                    .Where(objectId => !string.IsNullOrWhiteSpace(objectId))
                    .ToArray();
                document.NotifyFaceVisualPreviewChanged(faceReelObjectIds);
            }

            if (LoadedProject is not null)
            {
                LoadedProject.FruitMachinePlatform = value;
                SaveLoadedProjectMetadata();
                RefreshInputMapDiagnostics();
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

    public string OasisPlayerExecutablePath { get => _oasisPlayerExecutablePath; set { if (SetProperty(ref _oasisPlayerExecutablePath, value)) SavePreferences(); } }
    public bool OasisPlayerFullscreen { get => _oasisPlayerFullscreen; set { if (SetProperty(ref _oasisPlayerFullscreen, value)) SavePreferences(); } }
    public int OasisPlayerPreviewWidth { get => _oasisPlayerPreviewWidth; set { if (SetProperty(ref _oasisPlayerPreviewWidth, value)) SavePreferences(); } }
    public int OasisPlayerPreviewHeight { get => _oasisPlayerPreviewHeight; set { if (SetProperty(ref _oasisPlayerPreviewHeight, value)) SavePreferences(); } }
    public string FabricRuntimeLibraryPath { get => _fabricRuntimeLibraryPath; set { if (SetProperty(ref _fabricRuntimeLibraryPath, value)) SavePreferences(); } }
    public string ProductionAmberLibraryPath { get => _productionAmberLibraryPath; set { if (SetProperty(ref _productionAmberLibraryPath, value)) SavePreferences(); } }
    public string Mpu5AmberLibraryPath { get => _mpu5AmberLibraryPath; set { if (SetProperty(ref _mpu5AmberLibraryPath, value)) SavePreferences(); } }
    public int System6AudioBufferLengthMilliseconds
    {
        get => _system6AudioBufferLengthMilliseconds;
        set
        {
            var normalized = NormalizeSystem6AudioBufferLengthMilliseconds(value);
            if (SetProperty(ref _system6AudioBufferLengthMilliseconds, normalized))
            {
                SavePreferences();
            }
        }
    }
    public string System6ProgramRom1Path { get => _system6ProgramRom1Path; set => SetSystem6RomPath(ref _system6ProgramRom1Path, value, nameof(System6ProgramRom1Path)); }
    public string System6ProgramRom2Path { get => _system6ProgramRom2Path; set => SetSystem6RomPath(ref _system6ProgramRom2Path, value, nameof(System6ProgramRom2Path)); }
    public string System6ProgramRom3Path { get => _system6ProgramRom3Path; set => SetSystem6RomPath(ref _system6ProgramRom3Path, value, nameof(System6ProgramRom3Path)); }
    public string System6ProgramRom4Path { get => _system6ProgramRom4Path; set => SetSystem6RomPath(ref _system6ProgramRom4Path, value, nameof(System6ProgramRom4Path)); }
    public string System6SoundRom1Path { get => _system6SoundRom1Path; set => SetSystem6RomPath(ref _system6SoundRom1Path, value, nameof(System6SoundRom1Path)); }
    public string System6SoundRom2Path { get => _system6SoundRom2Path; set => SetSystem6RomPath(ref _system6SoundRom2Path, value, nameof(System6SoundRom2Path)); }
    public string System6SoundRom3Path { get => _system6SoundRom3Path; set => SetSystem6RomPath(ref _system6SoundRom3Path, value, nameof(System6SoundRom3Path)); }
    public string System6SoundRom4Path { get => _system6SoundRom4Path; set => SetSystem6RomPath(ref _system6SoundRom4Path, value, nameof(System6SoundRom4Path)); }
    public bool System6FlashSwitch
    {
        get => _system6FlashSwitch;
        set
        {
            if (SetProperty(ref _system6FlashSwitch, value))
            {
                SaveSystem6NativeRomSettings();
            }
        }
    }
    public int System6PercentSwitchValue
    {
        get => _system6PercentSwitchValue;
        set
        {
            var clamped = Math.Clamp(value, 0, 15);
            if (SetProperty(ref _system6PercentSwitchValue, clamped))
            {
                SaveSystem6NativeRomSettings();
            }
        }
    }
    public AmberCoinCommunicationStyle System6CoinCommunicationStyle { get => _system6CoinCommunicationStyle; set { if (SetProperty(ref _system6CoinCommunicationStyle, value)) SaveSystem6NativeRomSettings(); } }
    public bool System6CoinCommunicationInvert { get => _system6CoinCommunicationInvert; set { if (SetProperty(ref _system6CoinCommunicationInvert, value)) SaveSystem6NativeRomSettings(); } }
    public uint System6CoinPulseCycles { get => _system6CoinPulseCycles; set { if (SetProperty(ref _system6CoinPulseCycles, value)) SaveSystem6NativeRomSettings(); } }
    public bool System6CoinEdcEnabled { get => _system6CoinEdcEnabled; set { if (SetProperty(ref _system6CoinEdcEnabled, value)) SaveSystem6NativeRomSettings(); } }
    public string System6NativeRomStatus { get => _system6NativeRomStatus; private set => SetProperty(ref _system6NativeRomStatus, value); }
    public ObservableCollection<System6ReelOptoSettingsViewModel> System6ReelOptos { get => _system6ReelOptos; private set => SetProperty(ref _system6ReelOptos, value); }
    public ObservableCollection<System6CoinSettingsViewModel> System6Coins { get => _system6Coins; private set => SetProperty(ref _system6Coins, value); }

    public bool IsEditorProgressVisible
    {
        get => _isEditorProgressVisible;
        private set => SetProperty(ref _isEditorProgressVisible, value);
    }

    public bool IsEditorProgressIndeterminate
    {
        get => _isEditorProgressIndeterminate;
        private set => SetProperty(ref _isEditorProgressIndeterminate, value);
    }

    public double EditorProgressPercent
    {
        get => _editorProgressPercent;
        private set => SetProperty(ref _editorProgressPercent, value);
    }

    public string EditorProgressMessage
    {
        get => _editorProgressMessage;
        private set => SetProperty(ref _editorProgressMessage, value);
    }

    public string SelectedPreferencesCategory
    {
        get => _selectedPreferencesCategory;
        set => SetProperty(ref _selectedPreferencesCategory, value);
    }

    public string SelectedProjectSettingsCategory
    {
        get => _selectedProjectSettingsCategory;
        set => SetProperty(ref _selectedProjectSettingsCategory, value);
    }

    public string SelectedNativeProjectSettingsTab
    {
        get => _selectedNativeProjectSettingsTab;
        set => SetProperty(ref _selectedNativeProjectSettingsTab, value);
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
                OnPropertyChanged(nameof(InputDefinitions));
                OnPropertyChanged(nameof(WindowTitle));
                NotifyInspectorChanged();
                NotifyDocumentCommands();
            }
        }
    }

    public string WindowTitle => FormatWindowTitle(LoadedProject?.Name);

    public bool HasLoadedProject => LoadedProject is not null;
    public EmulationBackendState EmulationState
    {
        get => _emulationState;
        private set
        {
            if (SetProperty(ref _emulationState, value))
            {
                OnPropertyChanged(nameof(IsPauseEmulationChecked));
                NotifyEmulationCommands();
            }
        }
    }

    public bool IsPauseEmulationChecked => EmulationState == EmulationBackendState.Paused;

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


    public void ActivateSelectedAssetInspector()
    {
        _inspector.ActivateAssetInspection();
        NotifyInspectorChanged();
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

    public void SelectHierarchyItem(HierarchyItemViewModel? hierarchyItem, HierarchySelectionModifier modifier = HierarchySelectionModifier.None)
    {
        if (SelectedDocument is null || SelectedDocument.Document.DocumentType is not (EditorDocumentType.Panel2D or EditorDocumentType.Face))
        {
            return;
        }

        if (hierarchyItem is null || hierarchyItem.IsGroup || hierarchyItem.SelectionItem is null)
        {
            return;
        }

        HierarchyMouseSelectionService.ApplySelection(
            SelectedDocument.SelectionState,
            _hierarchy.GetVisibleRows(),
            hierarchyItem,
            modifier);
        _activeDocumentContext.SetPanelSelection(SelectedDocument.DocumentId, SelectedDocument.HierarchySelectedPanelSelection);
        _inspector.ActivateDocumentInspection();
        NotifyInspectorChanged();
        NotifyHierarchyCommands();
    }

    public void SelectHierarchyItemForContextMenu(HierarchyItemViewModel? hierarchyItem)
    {
        if (SelectedDocument is null || SelectedDocument.Document.DocumentType is not (EditorDocumentType.Panel2D or EditorDocumentType.Face))
        {
            return;
        }

        if (hierarchyItem is null || hierarchyItem.IsGroup || hierarchyItem.SelectionItem is not { } selectionItem)
        {
            NotifyHierarchyCommands();
            return;
        }

        if (!SelectedDocument.SelectionState.Items.Contains(selectionItem))
        {
            SelectedDocument.SelectionState.Replace(selectionItem);
            _activeDocumentContext.SetPanelSelection(SelectedDocument.DocumentId, SelectedDocument.HierarchySelectedPanelSelection);
            _inspector.ActivateDocumentInspection();
            NotifyInspectorChanged();
        }
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

    private bool CanDeleteHierarchyItem(HierarchyItemViewModel hierarchyItem) => _hierarchyPanelCommands.CanDeleteSelected() || _hierarchyPanelCommands.CanDeleteItem(hierarchyItem);

    private void DeleteHierarchyItem(HierarchyItemViewModel hierarchyItem) => _hierarchyPanelCommands.DeleteSelected();

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

    private void OpenFaceStubDocument()
    {
        _documentWorkspace.OpenFaceStubDocument();
    }

    private bool CanAddFaceSourceShape()
    {
        return _documentWorkspace.CanAddFaceSourceShapeToSelectedPanel2D();
    }

    private void AddFaceSourceShape()
    {
        _documentWorkspace.AddFaceSourceShapeToSelectedPanel2D();
    }

    private bool CanGenerateFaceFromSourceShape()
    {
        return _documentWorkspace.CanCreateFaceFromSelectedFaceSourceShape();
    }

    private async void GenerateFaceFromSourceShape()
    {
        if (!CanGenerateFaceFromSourceShape()) return;
        try
        {
            await _progressDialogService.RunAsync(
                new EditorProgressRequest("Creating Face", "Creating Face from Face Source Shape...", EditorProgressMode.Determinate),
                (progress, _) =>
                {
                    _documentWorkspace.GenerateFaceFromSelectedFaceSourceShape(null, _defaultFaceGenerationSettings, progress);
                    return Task.CompletedTask;
                });
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            AddOutputEntry($"Create Face from Face Source Shape failed: {ex.Message}", OutputLogStatus.Error);
            MessageBox.Show(ex.Message, "Create Face from Face Source Shape Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private bool CanRegenerateFace()
    {
        return _documentWorkspace.CanRegenerateSelectedFace();
    }

    private async void RegenerateFace()
    {
        if (!CanRegenerateFace())
        {
            return;
        }

        FaceGenerationSettingsModel? settings = null;
        if (_showFaceGenerationSettingsBeforeRegenerate)
        {
            var existingFace = SelectedDocument?.GetFaceDocument();
            if (existingFace is null)
            {
                return;
            }

            var dialog = new FaceGenerationSettingsDialog(existingFace.GenerationSettings, "Regenerate")
            {
                Owner = _ownerWindow
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            settings = dialog.Settings;
        }

        try
        {
            await _progressDialogService.RunAsync(
                new EditorProgressRequest("Regenerating Face", "Regenerating selected Face from Face Source Shape...", EditorProgressMode.Determinate),
                (progress, _) =>
                {
                    _documentWorkspace.RegenerateSelectedFace(settings, progress);
                    return Task.CompletedTask;
                });
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            AddOutputEntry($"Regenerate Face failed: {ex.Message}", OutputLogStatus.Error);
            MessageBox.Show(ex.Message, "Regenerate Face Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private bool CanOpenFaceGenerationSettings()
    {
        return CanGenerateFaceFromSourceShape() || SelectedDocument?.Document.DocumentType == EditorDocumentType.Face;
    }

    private async void OpenFaceGenerationSettings()
    {
        if (SelectedDocument?.Document.DocumentType == EditorDocumentType.Face)
        {
            var canRegenerate = CanRegenerateFace();
            var existingFace = SelectedDocument.GetFaceDocument();
            var dialog = new FaceGenerationSettingsDialog(existingFace.GenerationSettings, canRegenerate ? "Regenerate" : "Save")
            {
                Owner = _ownerWindow
            };

            if (dialog.ShowDialog() == true)
            {
                if (canRegenerate)
                {
                    try
                    {
                        await _progressDialogService.RunAsync(
                            new EditorProgressRequest("Regenerating Face", "Regenerating selected Face from Face Source Shape...", EditorProgressMode.Determinate),
                            (progress, _) =>
                            {
                                _documentWorkspace.RegenerateSelectedFace(dialog.Settings, progress);
                                return Task.CompletedTask;
                            });
                    }
                    catch (Exception ex)
                    {
                        StatusMessage = ex.Message;
                        AddOutputEntry($"Regenerate Face failed: {ex.Message}", OutputLogStatus.Error);
                        MessageBox.Show(ex.Message, "Regenerate Face Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                else
                {
                    SaveSelectedFaceGenerationSettings(dialog.Settings);
                }
            }

            return;
        }

        if (!CanGenerateFaceFromSourceShape())
        {
            return;
        }

        var generateDialog = new FaceGenerationSettingsDialog(_defaultFaceGenerationSettings, "Create Face from Face Source Shape")
        {
            Owner = _ownerWindow
        };

        if (generateDialog.ShowDialog() != true)
        {
            return;
        }

        var faceNameDialog = new HierarchyRenameDialog("New Face", "Create Face Asset", "Face asset name")
        {
            Owner = _ownerWindow
        };

        if (faceNameDialog.ShowDialog() != true)
        {
            return;
        }

        var faceAssetName = faceNameDialog.NameText;
        _defaultFaceGenerationSettings = generateDialog.Settings;
        SavePreferences();
        try
        {
            await _progressDialogService.RunAsync(
                new EditorProgressRequest("Creating Face", "Creating Face from Face Source Shape...", EditorProgressMode.Determinate),
                (progress, _) =>
                {
                    _documentWorkspace.GenerateFaceFromSelectedFaceSourceShape(faceAssetName, generateDialog.Settings, progress);
                    return Task.CompletedTask;
                });
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            AddOutputEntry($"Create Face from Face Source Shape failed: {ex.Message}", OutputLogStatus.Error);
            MessageBox.Show(ex.Message, "Create Face from Face Source Shape Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }


    private void SaveSelectedFaceGenerationSettings(FaceGenerationSettingsModel settings)
    {
        if (SelectedDocument?.Document.DocumentType != EditorDocumentType.Face)
        {
            return;
        }

        var faceDocument = SelectedDocument.GetFaceDocument();
        SelectedDocument.SetFaceDocument(new FaceDocumentModel
        {
            Id = faceDocument.Id,
            Title = faceDocument.Title,
            Summary = faceDocument.Summary,
            SourcePanel2DDocumentId = faceDocument.SourcePanel2DDocumentId,
            SourcePanel2DDocumentPath = faceDocument.SourcePanel2DDocumentPath,
            SourceFaceShapeId = faceDocument.SourceFaceShapeId,
            AssignedCabinetFaceTargetId = faceDocument.AssignedCabinetFaceTargetId,
            AssignedCabinetAssetPath = faceDocument.AssignedCabinetAssetPath,
            SourceRegion = faceDocument.SourceRegion,
            LastRegeneratedAtUtc = faceDocument.LastRegeneratedAtUtc,
            GenerationSettings = (settings ?? FaceGenerationSettingsModel.Default).Normalize(),
            RuntimeRenderAssets = faceDocument.RuntimeRenderAssets,
            MaskLayer = faceDocument.MaskLayer,
            Trays = faceDocument.Trays,
            LampEmitters = faceDocument.LampEmitters,
            Layers = faceDocument.Layers,
            Elements = faceDocument.Elements
        },
        new PanelChangeEvent(
            SelectedDocument.DocumentId,
            null,
            PanelChangeProperties.Metadata,
            AffectsCanvas: false,
            AffectsHierarchy: false,
            AffectsInspectorRows: true,
            AffectsPersistence: true));
        SelectedDocument.MarkDirty();
        AddOutputEntry($"Updated face generation settings for '{SelectedDocument.Title}'.", OutputLogStatus.Info);
    }

    private bool CanValidateFace()
    {
        return SelectedDocument?.Document.DocumentType == EditorDocumentType.Face;
    }

    private void ValidateFace()
    {
        if (!CanValidateFace())
        {
            return;
        }

        var diagnostics = _documentWorkspace.ValidateSelectedFace();
        if (diagnostics.Count == 0)
        {
            AddOutputEntry($"Face validation completed for '{SelectedDocument!.Title}' with no warnings.", OutputLogStatus.Info);
            return;
        }

        foreach (var diagnostic in diagnostics)
        {
            AddOutputEntry($"Face validation ({diagnostic.Code}): {diagnostic.Message}", diagnostic.Severity == FaceValidationSeverity.Error ? OutputLogStatus.Error : OutputLogStatus.Warning);
        }
    }

    private bool CanOpenSourcePanel2D()
    {
        return _documentWorkspace.CanOpenSourcePanel2DForSelectedFace();
    }

    private void OpenSourcePanel2D()
    {
        if (!CanOpenSourcePanel2D())
        {
            return;
        }

        _documentWorkspace.OpenSourcePanel2DForSelectedFace();
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

    private bool CanImportMfmeFml()
    {
        return LoadedProject is not null
               && !_isFmlImportInProgress
               && SelectedDocument?.Document.DocumentType == EditorDocumentType.Panel2D;
    }


    private void BuildOasisPlayerMachine()
    {
        if (LoadedProject is null)
        {
            ReportEditorOperationError("Open a project before building for Oasis Player.", OutputLogStatus.Warning);
            return;
        }

        var selectedDocument = SelectedDocument;
        if (selectedDocument?.Document.DocumentType != EditorDocumentType.Cabinet3D || selectedDocument.Document.IsUntitled)
        {
            ReportEditorOperationError("Select a saved Cabinet3D asset before building for Oasis Player.", OutputLogStatus.Warning);
            return;
        }

        var result = new MachineRuntimeBuildService().BuildFromCabinetDocument(LoadedProject, selectedDocument.Document.FilePath, selectedDocument.GetCabinetDocument());
        if (!result.Success)
        {
            ReportEditorOperationError(result.ErrorMessage ?? "Failed to build Oasis Player runtime output.", OutputLogStatus.Error);
            return;
        }

        StatusMessage = $"Oasis Player machine build written: {result.BuildRoot}";
        AddOutputEntry($"Oasis Player machine build written: {result.BuildRoot}", OutputLogStatus.Info);
    }


    private void PreviewInOasisPlayer()
    {
        if (LoadedProject is null)
        {
            ReportEditorOperationError("Open a project before previewing in Oasis Player.", OutputLogStatus.Warning);
            return;
        }

        var selectedDocument = SelectedDocument;
        if (selectedDocument?.Document.DocumentType != EditorDocumentType.Cabinet3D || selectedDocument.Document.IsUntitled)
        {
            ReportEditorOperationError("Select a saved Cabinet3D asset before previewing in Oasis Player.", OutputLogStatus.Warning);
            return;
        }

        var result = _oasisPlayerPreviewService.Preview(LoadedProject, selectedDocument.Document.FilePath, selectedDocument.GetCabinetDocument(), new OasisPlayerPreferences
        {
            ExecutablePath = OasisPlayerExecutablePath,
            Fullscreen = OasisPlayerFullscreen,
            PreviewWidth = OasisPlayerPreviewWidth,
            PreviewHeight = OasisPlayerPreviewHeight
        });

        if (!result.Success)
        {
            ReportEditorOperationError(result.ErrorMessage ?? "Failed to preview in Oasis Player.", OutputLogStatus.Error);
            return;
        }

        var arguments = string.Join(" ", result.Arguments);
        StatusMessage = $"Oasis Player preview launched: {result.BuildRoot}";
        AddOutputEntry($"Oasis Player machine build written: {result.BuildRoot}", OutputLogStatus.Info);
        AddOutputEntry($"Oasis Player preview launched: {result.ExecutablePath} {arguments}", OutputLogStatus.Info);
    }

    private bool CanBuildOasisPlayerMachine()
    {
        return LoadedProject is not null
               && SelectedDocument?.Document.DocumentType == EditorDocumentType.Cabinet3D
               && SelectedDocument.Document.IsUntitled == false;
    }

    private bool CanImportGlbModel()
    {
        return LoadedProject is not null
               && SelectedDocument?.Document.DocumentType == EditorDocumentType.Cabinet3D;
    }

    private void ImportGlbModel()
    {
        if (LoadedProject is null)
        {
            return;
        }

        var activeDocument = SelectedDocument;
        if (activeDocument?.Document.DocumentType != EditorDocumentType.Cabinet3D)
        {
            AddOutputEntry("GLB import is supported only when a Cabinet3D document is active.", OutputLogStatus.Warning);
            MessageBox.Show(
                "GLB import is currently supported only for Cabinet3D documents.",
                "Import GLB Model",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = "Import GLB Model",
            Filter = "GLB Models|*.glb|All Files|*.*",
            InitialDirectory = LoadedProject.ProjectDirectory,
            CheckFileExists = true
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        activeDocument.SetCabinetDocument(CabinetDocument.FromModelPath(dialog.FileName));
        activeDocument.MarkDirty();
        NotifyInspectorChanged();
        NotifyDocumentCommands();

        StatusMessage = $"Imported GLB model into Cabinet3D asset: {dialog.FileName}";
        AddOutputEntry($"Imported GLB model into Cabinet3D asset: {dialog.FileName}", OutputLogStatus.Info);
    }

    private async void ImportMfmeFml()
    {
        if (LoadedProject is null)
        {
            return;
        }

        if (SelectedDocument?.Document.DocumentType != EditorDocumentType.Panel2D)
        {
            AddOutputEntry("MFME FML import is supported only when a Panel2D document is active.", OutputLogStatus.Warning);
            MessageBox.Show(
                "MFME FML import is currently supported only for Panel2D documents.",
                "Import MFME FML",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = "Import MFME FML",
            Filter = "MFME FML Layout|*.fml|All Files|*.*",
            InitialDirectory = ResolveMfmeFmlImportInitialDirectory(
                _lastMfmeFmlImportDirectory,
                LoadedProject.ProjectDirectory),
            CheckFileExists = true
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var selectedDirectory = Path.GetDirectoryName(dialog.FileName);
        if (!string.IsNullOrWhiteSpace(selectedDirectory))
        {
            _lastMfmeFmlImportDirectory = selectedDirectory;
            SavePreferences();
        }

        var activeDocument = SelectedDocument;
        var loadedProject = LoadedProject;
        if (activeDocument is null || loadedProject is null)
        {
            return;
        }

        _isFmlImportInProgress = true;
        NotifyDocumentCommands();
        BeginEditorProgress("Importing MFME FML...", 0.05);

        try
        {
            await YieldForProgressRenderAsync();
            var fmlPath = dialog.FileName;
            var projectDirectory = loadedProject.ProjectDirectory;
            var assetsDirectory = loadedProject.AssetsDirectory;

            ReportEditorProgress("Decoding FML and copying assets...", 0.15);
            var result = await _progressDialogService.RunAsync(
                new EditorProgressRequest("Importing MFME FML", "Decoding FML and copying assets...", EditorProgressMode.Determinate),
                async (progress, _) =>
                {
                    progress.Report(0.1, "Decoding FML layout...");
                    var importResult = await Task.Run(() => _fmlImportService.ImportFromFml(
                        fmlPath,
                        projectDirectory,
                        assetsDirectory,
                        copyAssets: true));
                    progress.Report(0.6, "Processing import diagnostics...");
                    return importResult;
                });

            ReportEditorProgress("Processing import diagnostics...", 0.6);
            foreach (var diagnostic in result.DebugDiagnostics)
            {
                AddOutputEntry($"MFME FML import debug: {diagnostic}", OutputLogStatus.Info);
            }

            foreach (var warning in result.Warnings)
            {
                AddOutputEntry($"MFME FML import warning ({warning.Code}): {warning.Message}", OutputLogStatus.Warning);
            }

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    AddOutputEntry($"MFME FML import failed: {error}", OutputLogStatus.Error);
                }

                MessageBox.Show(
                    "MFME FML import failed. See Output for details.",
                    "Import MFME FML",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (!OpenDocuments.Contains(activeDocument))
            {
                AddOutputEntry("MFME FML import completed but the target document is no longer open.", OutputLogStatus.Warning);
                return;
            }

            ReportEditorProgress("Updating project input definitions...", 0.7);
            if (LoadedProject is not null && ReferenceEquals(LoadedProject, loadedProject) && result.InputDefinitions.Count > 0)
            {
                LoadedProject.InputDefinitions.Clear();
                foreach (var inputDefinition in result.InputDefinitions)
                {
                    LoadedProject.InputDefinitions.Add(inputDefinition);
                }

                SaveLoadedProjectMetadata();
                OnPropertyChanged(nameof(InputDefinitions));
                RefreshInputMapDiagnostics();
                AddOutputEntry($"MFME FML import created {result.InputDefinitions.Count} input definitions.", OutputLogStatus.Info);
            }

            ReportEditorProgress("Inserting imported elements...", 0.8);
            var importCommand = new ImportPanelElementsCommand(
                activeDocument.DocumentId,
                activeDocument,
                result.ImportedElements);
            var inserted = _documentWorkspace.ExecuteDocumentCanvasCommand(activeDocument.DocumentId, importCommand);
            if (!inserted)
            {
                AddOutputEntry("MFME FML import completed but no elements were inserted.", OutputLogStatus.Warning);
                return;
            }

            ReportEditorProgress("Refreshing assets and editor panels...", 0.9);
            _assetBrowser.RefreshAssetBrowser();
            RefreshHierarchy();
            NotifyInspectorChanged();

            var grouped = result.ImportedElements
                .GroupBy(element => element.Kind)
                .OrderBy(group => group.Key.ToString(), StringComparer.Ordinal)
                .Select(group => $"{group.Key}: {group.Count()}");

            ReportEditorProgress("MFME FML import complete.", 1.0);
            AddOutputEntry($"MFME FML import completed. Imported {result.ImportedElements.Count} elements.", OutputLogStatus.Info);
            AddOutputEntry($"MFME FML import kinds -> {string.Join(", ", grouped)}", OutputLogStatus.Info);
            AddOutputEntry($"MFME FML import skipped {result.UnsupportedComponentTypes.Count} unsupported components.", OutputLogStatus.Info);
            AddOutputEntry($"MFME FML import copied {result.CopiedAssetRelativePaths.Count} assets.", OutputLogStatus.Info);
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            AddOutputEntry($"MFME FML import failed: {ex.Message}", OutputLogStatus.Error);
            MessageBox.Show(ex.Message, "Import MFME FML Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            _isFmlImportInProgress = false;
            EndEditorProgress();
            NotifyDocumentCommands();
        }
    }

    public static string ResolveMfmeFmlImportInitialDirectory(
        string? lastMfmeFmlImportDirectory,
        string projectDirectory)
    {
        if (!string.IsNullOrWhiteSpace(lastMfmeFmlImportDirectory)
            && Directory.Exists(lastMfmeFmlImportDirectory))
        {
            return lastMfmeFmlImportDirectory;
        }

        return projectDirectory;
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
            || string.Equals(extension, ".face", StringComparison.OrdinalIgnoreCase)
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
            openData.PanelTitle,
            openData.FaceDocumentJson,
            openData.CabinetDocumentJson);
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


    private void OnOpenDocumentsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (DocumentTabViewModel document in e.OldItems)
            {
                document.PropertyChanged -= OnOpenDocumentPropertyChanged;
                document.FaceVisualStateChanged -= OnOpenDocumentFaceVisualStateChanged;
            }
        }

        if (e.NewItems is not null)
        {
            foreach (DocumentTabViewModel document in e.NewItems)
            {
                document.PropertyChanged += OnOpenDocumentPropertyChanged;
                document.FaceVisualStateChanged += OnOpenDocumentFaceVisualStateChanged;
            }
        }

        RefreshCabinetFacePreviews();
    }

    private void OnOpenDocumentPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is DocumentTabViewModel document
            && document.Document.DocumentType == EditorDocumentType.Face
            && string.Equals(e.PropertyName, nameof(DocumentTabViewModel.FaceDocumentJson), StringComparison.Ordinal))
        {
            RefreshCabinetFacePreviews();
        }
    }

    private void OnOpenDocumentFaceVisualStateChanged(FaceVisualStateChangedEvent visualStateChanged)
    {
        foreach (var cabinetViewer in OpenDocuments
            .Where(document => document.Document.DocumentType == EditorDocumentType.Cabinet3D)
            .Select(document => document.ExistingCabinetViewer)
            .Where(viewer => viewer is not null))
        {
            cabinetViewer!.QueueFaceRuntimePreviewRefresh(visualStateChanged.DocumentId);
        }
    }

    private void RefreshCabinetFacePreviews()
    {
        foreach (var cabinetViewer in OpenDocuments
            .Where(document => document.Document.DocumentType == EditorDocumentType.Cabinet3D)
            .Select(document => document.CabinetViewer)
            .Where(viewer => viewer is not null))
        {
            cabinetViewer!.RefreshFacePreviews();
        }
    }

    private void OnAssetBrowserItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasAssetBrowserItems));
    }

    private bool CanSaveSelectedDocument()
    {
        return _documentWorkspace.CanSaveSelectedDocument();
    }

    private async void SaveSelectedDocument()
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
            var documentTitle = current.Title;
            var updatedDocument = await _progressDialogService.RunAsync(
                new EditorProgressRequest($"Saving {documentTitle}", "Saving document...", EditorProgressMode.Determinate, ShowDelay: TimeSpan.Zero),
                (progress, _) => Task.Run(() => _documentSaveService.SaveDocument(current, savePath, LoadedProject, progress)));
            _documentWorkspace.ReplaceDocument(current, updatedDocument);
            _assetBrowser.ScheduleRefreshFromDisk();
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

        if (selectedDocument?.Document.DocumentType is EditorDocumentType.Panel2D or EditorDocumentType.Cabinet3D or EditorDocumentType.Face)
        {
            var nameDialog = new HierarchyRenameDialog(defaultName, "Save Asset", "Asset name")
            {
                Owner = _ownerWindow
            };

            if (nameDialog.ShowDialog() != true)
            {
                return null;
            }

            var pathService = new ProjectAssetPathService();
            var assetType = selectedDocument.Document.DocumentType switch
            {
                EditorDocumentType.Face => EditorAssetType.Face,
                EditorDocumentType.Cabinet3D => EditorAssetType.Cabinet3D,
                _ => EditorAssetType.Panel2D
            };
            var assetName = pathService.EnsureUniqueAssetName(LoadedProject, assetType, nameDialog.NameText);
            pathService.CreateAssetPackageDirectory(LoadedProject, assetType, assetName);
            return pathService.GetAssetManifestPath(LoadedProject, assetType, assetName);
        }

        var dialog = new SaveFileDialog
        {
            Title = "Save Document",
            InitialDirectory = LoadedProject.AssetsDirectory,
            FileName = $"{defaultName}.machine",
            DefaultExt = ".machine",
            Filter = "Machine|*.machine|All Files|*.*"
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    private void CloseSelectedDocument()
    {
        _ = ReleaseAllPlayViewInputsAsync("document close", CancellationToken.None);
        _documentWorkspace.CloseSelectedDocument();
    }

    public async Task<bool> TryHandlePlayViewKeyDownAsync(string keyboardShortcut, bool isFocused, bool isRepeat, CancellationToken cancellationToken)
    {
        var canRoute = EnsurePlayViewInputRouter();
        var dispatcher = EnsurePlayViewInputDispatcher();
        if (dispatcher is null)
        {
            return false;
        }

        var handled = await dispatcher.TryHandleKeyDownAsync(SelectedFruitMachinePlatform, keyboardShortcut, isFocused, isRepeat, cancellationToken).ConfigureAwait(false);
        if (!handled && canRoute && isFocused && !isRepeat && !dispatcher.CanResolveShortcut(keyboardShortcut))
        {
            AddOutputEntry($"Play View key input unresolved: '{keyboardShortcut}' on platform '{SelectedFruitMachinePlatform}'.", OutputLogStatus.Warning);
        }

        return handled;
    }

    public Task<bool> TryHandlePlayViewKeyUpAsync(string keyboardShortcut, bool isFocused, CancellationToken cancellationToken)
    {
        var dispatcher = EnsurePlayViewInputDispatcher();
        if (dispatcher is null)
        {
            return Task.FromResult(false);
        }

        return dispatcher.TryHandleKeyUpAsync(SelectedFruitMachinePlatform, keyboardShortcut, isFocused, cancellationToken);
    }

    public Task<bool> TryHandlePlayViewPointerDownAsync(Guid visualElementId, bool isFocused, CancellationToken cancellationToken)
    {
        return TryHandlePlayViewPointerDownAsync(
            PlayInputTarget.ForPanelVisualElement(visualElementId),
            isFocused,
            $"Play View pointer input unresolved for visual '{visualElementId}'",
            cancellationToken);
    }

    public Task<bool> TryHandlePlayViewPointerUpAsync(Guid visualElementId, bool isFocused, CancellationToken cancellationToken)
    {
        return TryHandlePlayViewPointerUpAsync(PlayInputTarget.ForPanelVisualElement(visualElementId), isFocused, cancellationToken);
    }

    public Task<bool> TryHandlePlayViewPointerDownAsync(PlayInputTarget inputTarget, bool isFocused, CancellationToken cancellationToken)
    {
        return TryHandlePlayViewPointerDownAsync(
            inputTarget,
            isFocused,
            $"Play View pointer input unresolved for {inputTarget}",
            cancellationToken);
    }

    private async Task<bool> TryHandlePlayViewPointerDownAsync(PlayInputTarget inputTarget, bool isFocused, string unresolvedMessage, CancellationToken cancellationToken)
    {
        var canRoute = EnsurePlayViewInputRouter();
        var dispatcher = EnsurePlayViewInputDispatcher();
        if (dispatcher is null)
        {
            return false;
        }

        var handled = await dispatcher.TryHandlePointerDownAsync(SelectedFruitMachinePlatform, inputTarget, isFocused, cancellationToken).ConfigureAwait(false);
        if (!handled && canRoute && isFocused)
        {
            AddOutputEntry($"{unresolvedMessage} on platform '{SelectedFruitMachinePlatform}'.", OutputLogStatus.Warning);
        }

        return handled;
    }

    public Task<bool> TryHandlePlayViewPointerUpAsync(PlayInputTarget inputTarget, bool isFocused, CancellationToken cancellationToken)
    {
        var dispatcher = EnsurePlayViewInputDispatcher();
        if (dispatcher is null)
        {
            return Task.FromResult(false);
        }

        return dispatcher.TryHandlePointerUpAsync(SelectedFruitMachinePlatform, inputTarget, isFocused, cancellationToken);
    }

    public Task<bool> TryHandleFacePlayViewPointerDownAsync(MachineInputReference inputReference, bool isFocused, CancellationToken cancellationToken)
    {
        return TryHandlePlayViewPointerDownAsync(
            PlayInputTarget.ForMachineInput(inputReference),
            isFocused,
            $"Face Play View pointer input unresolved for machine input '{inputReference}'",
            cancellationToken);
    }

    public Task<bool> TryHandleFacePlayViewPointerUpAsync(MachineInputReference inputReference, bool isFocused, CancellationToken cancellationToken)
    {
        return TryHandlePlayViewPointerUpAsync(PlayInputTarget.ForMachineInput(inputReference), isFocused, cancellationToken);
    }

    public Task<int> ReleaseAllPlayViewInputsAsync(string reason, CancellationToken cancellationToken)
    {
        return ReleaseAllPlayViewInputsCoreAsync(reason, cancellationToken);
    }

    private async Task<int> ReleaseAllPlayViewInputsCoreAsync(string reason, CancellationToken cancellationToken)
    {
        if (_playViewInputRouter is null)
        {
            return 0;
        }

        var dispatcher = EnsurePlayViewInputDispatcher();
        if (dispatcher is null)
        {
            return 0;
        }

        var released = await dispatcher.ReleaseAllActiveAsync(SelectedFruitMachinePlatform, cancellationToken).ConfigureAwait(false);
        if (released > 0)
        {
            AddOutputEntry($"Play View released {released} active input(s) due to {reason}.", OutputLogStatus.Info);
        }

        return released;
    }

    private PlayViewInputDispatcher? EnsurePlayViewInputDispatcher()
    {
        if (!EnsurePlayViewInputRouter())
        {
            return null;
        }

        _playViewInputDispatcher ??= new PlayViewInputDispatcher(_playViewInputRouter!, LoadedProject?.InputDefinitions ?? []);
        return _playViewInputDispatcher;
    }

    private bool EnsurePlayViewInputRouter()
    {
        if (LoadedProject is null
            || _activeEmulationBackend is null
            || EmulationState is not EmulationBackendState.Running and not EmulationBackendState.Paused)
        {
            return false;
        }

        _playViewInputRouter ??= new PlayViewInputRouter(_activeEmulationBackend);
        return true;
    }


    private void OpenPreferences()
    {
        ToolWindowOpenRequested?.Invoke(EditorToolWindowId.Preferences);
        AddOutputEntry("Opened Preferences pane.", OutputLogStatus.Info);
    }

    private void BrowseOasisPlayerExecutable()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select Oasis Player executable",
            Filter = "Oasis Player executable|*.exe|All files|*.*",
            CheckFileExists = true
        };

        if (!string.IsNullOrWhiteSpace(OasisPlayerExecutablePath))
        {
            dialog.InitialDirectory = Path.GetDirectoryName(OasisPlayerExecutablePath);
        }

        if (dialog.ShowDialog(_ownerWindow) == true)
        {
            OasisPlayerExecutablePath = dialog.FileName;
            AddOutputEntry($"Oasis Player executable path set to: {OasisPlayerExecutablePath}", OutputLogStatus.Info);
        }
    }

    private static int NormalizeSystem6AudioBufferLengthMilliseconds(int value)
        => Math.Clamp(value, 10, 1000);

    private void SavePreferences()
    {
        var existingPreferences = _preferencesStore.Load();
        _preferencesStore.Save(new EditorPreferences
        {
            ThemePreference = SelectedThemePreference,
            LastMfmeFmlImportDirectory = _lastMfmeFmlImportDirectory,
            NativeEmulation = new NativeEmulationPreferences
            {
                FabricRuntimeLibraryPath = FabricRuntimeLibraryPath,
                ProductionAmberLibraryPath = ProductionAmberLibraryPath,
                Mpu5AmberLibraryPath = Mpu5AmberLibraryPath,
                AudioBufferLengthMilliseconds = System6AudioBufferLengthMilliseconds
            },
            Player = new OasisPlayerPreferences
            {
                ExecutablePath = OasisPlayerExecutablePath,
                Fullscreen = OasisPlayerFullscreen,
                PreviewWidth = OasisPlayerPreviewWidth,
                PreviewHeight = OasisPlayerPreviewHeight
            },
            FaceGeneration = FaceGenerationPreferences.FromSettings(
                _defaultFaceGenerationSettings,
                _showFaceGenerationSettingsBeforeRegenerate),
            OutputLog = new OutputLogPreferences
            {
                ShowInfoLogs = _outputLog.ShowInfoLogs,
                ShowWarningLogs = _outputLog.ShowWarningLogs,
                ShowErrorLogs = _outputLog.ShowErrorLogs,
                AutoScroll = _outputLog.AutoScroll,
                SearchText = _outputLog.SearchText
            },
            ProjectWindowStates = existingPreferences.ProjectWindowStates
        });
    }

    private void OpenProjectSettings()
    {
        ToolWindowOpenRequested?.Invoke(EditorToolWindowId.ProjectSettings);
        AddOutputEntry("Opened Project Settings pane.", OutputLogStatus.Info);
    }

    private void OpenInputMap()
    {
        ToolWindowOpenRequested?.Invoke(EditorToolWindowId.InputMap);
        AddOutputEntry("Opened Input Map pane.", OutputLogStatus.Info);
    }

    private async void OpenPlayView()
    {
        BeginEditorProgress("Opening Play View...", indeterminate: true);
        try
        {
            await YieldForProgressRenderAsync();
            ToolWindowOpenRequested?.Invoke(EditorToolWindowId.PlayView);
            await YieldForProgressRenderAsync();
            AddOutputEntry("Opened Play View pane.", OutputLogStatus.Info);
        }
        finally
        {
            EndEditorProgress();
        }
    }

    private void ClosePreferences()
    {
        ToolWindowCloseRequested?.Invoke(EditorToolWindowId.Preferences);
    }

    private CoalescedMachineOutputDispatcher? _machineOutputDispatcher;

    private static void DispatchToUiThread(Action work)
    {
        ArgumentNullException.ThrowIfNull(work);

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
        {
            return;
        }

        if (dispatcher.CheckAccess())
        {
            work();
            return;
        }

        _ = dispatcher.BeginInvoke(work);
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

        StopEmulationForWindowClose();

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
        _machineRuntimeStates.ClearAll();
        ProjectAssetPathResolver.ProjectDirectoryPath = null;
        _assetBrowser.Dispose();

        AssetBrowserItems.Clear();
        SelectedAsset = null;
        ProjectFilePath = string.Empty;
    }

    private void ExitApplication()
    {
        StopEmulationForWindowClose();

        Application.Current.Shutdown();
    }


    private bool CanStartEmulation()
    {
        return HasLoadedProject && EmulationState is EmulationBackendState.Stopped or EmulationBackendState.Failed;
    }

    private async void StartEmulation()
    {
        if (!CanStartEmulation())
        {
            return;
        }

        AddOutputEntry("Emulation start requested.", OutputLogStatus.Info);
        try
        {
            await StartBackendEmulationAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            AddOutputEntry($"Emulation failed to start: {ex.Message}", OutputLogStatus.Error);
        }
    }

    private async Task StartBackendEmulationAsync(CancellationToken cancellationToken)
    {
        var backend = _emulationBackendFactory.CreateBackend(SelectedFruitMachinePlatform)
            ?? throw new InvalidOperationException($"No emulation backend is available for platform '{SelectedFruitMachinePlatform}'.");

        _activeEmulationBackend = backend;
        _machineOutputDispatcher = new CoalescedMachineOutputDispatcher(DispatchToUiThread, ApplyMachineOutputBatch);
        backend.StateChanged += OnActiveBackendStateChanged;
        backend.LampChanged += OnActiveBackendLampChanged;
        backend.ReelChanged += OnActiveBackendReelChanged;
        backend.SegmentChanged += OnActiveBackendSegmentChanged;
        backend.VfdBrightnessChanged += OnActiveBackendVfdBrightnessChanged;
        try
        {
            await backend.StartAsync(BuildEmulationLaunchRequest(), cancellationToken).ConfigureAwait(false);
            AddOutputEntry($"Started {backend.GetType().Name} for platform '{SelectedFruitMachinePlatform}'.", OutputLogStatus.Info);
        }
        catch
        {
            backend.StateChanged -= OnActiveBackendStateChanged;
            backend.LampChanged -= OnActiveBackendLampChanged;
            backend.ReelChanged -= OnActiveBackendReelChanged;
            backend.SegmentChanged -= OnActiveBackendSegmentChanged;
            backend.VfdBrightnessChanged -= OnActiveBackendVfdBrightnessChanged;
            _machineOutputDispatcher?.Detach();
            _machineOutputDispatcher = null;
            await backend.DisposeAsync().ConfigureAwait(false);
            _activeEmulationBackend = null;
            throw;
        }
    }

    private EmulationLaunchRequest BuildEmulationLaunchRequest()
    {
        return SelectedFruitMachinePlatform switch
        {
            FruitMachinePlatformType.Impact => new EmulationLaunchRequest(
                BuildSystem6NativeRomSettingsForLaunch(), BuildConfiguredLampIdsForLaunch(), BuildConfiguredSevenSegmentDisplayIdsForLaunch()),
            FruitMachinePlatformType.MPU5 => EmulationLaunchRequest.ForMpu5(
                LoadedProject?.Mpu5NativeRoms ?? new Mpu5NativeRomSettings(), BuildConfiguredLampIdsForLaunch(), BuildConfiguredSevenSegmentDisplayIdsForLaunch()),
            _ => throw new NotSupportedException($"Platform '{SelectedFruitMachinePlatform}' is not supported by Fabric Amber emulation.")
        };
    }

    private IReadOnlyList<int>? BuildConfiguredLampIdsForLaunch()
    {
        var lampIds = new SortedSet<int>();
        foreach (var document in OpenDocuments)
        {
            foreach (var element in document.GetPanelElements())
            {
                if (element.Kind == PanelElementKind.Lamp && element.DisplayNumber is int lampId && lampId is >= 0 and <= byte.MaxValue)
                {
                    lampIds.Add(lampId);
                }
            }

            foreach (var emitter in document.GetFaceDocument().LampEmitters)
            {
                if (emitter.LampId is int lampId && lampId is >= 0 and <= byte.MaxValue)
                {
                    lampIds.Add(lampId);
                }
            }
        }

        return lampIds.Count == 0 ? null : lampIds.ToArray();
    }


    private IReadOnlyList<int>? BuildConfiguredSevenSegmentDisplayIdsForLaunch()
    {
        var displayIds = new SortedSet<int>();
        foreach (var document in OpenDocuments)
        {
            foreach (var element in document.GetPanelElements())
            {
                if (element.Kind == PanelElementKind.SevenSegment && element.DisplayNumber is int displayId && displayId is >= 0 and <= ushort.MaxValue)
                {
                    displayIds.Add(displayId);
                }
            }

            foreach (var faceDisplay in document.GetFaceElements().OfType<FaceSevenSegmentDisplayElement>())
            {
                if (faceDisplay.LinkedMachineObjectReference is MachineObjectReference reference
                    && reference.Kind == MachineObjectKind.SevenSegmentDisplay
                    && int.TryParse(reference.Id, out var displayId)
                    && displayId is >= 0 and <= ushort.MaxValue)
                {
                    displayIds.Add(displayId);
                }
            }
        }

        return displayIds.Count == 0 ? null : displayIds.ToArray();
    }

    private bool SetSystem6RomPath(ref string field, string value, string propertyName)
    {
        if (!SetProperty(ref field, value, propertyName))
        {
            return false;
        }

        SaveSystem6NativeRomSettings();
        RefreshSystem6NativeRomStatus();
        return true;
    }

    private void SaveSystem6NativeRomSettings()
    {
        if (LoadedProject is null)
        {
            return;
        }

        LoadedProject.System6NativeRoms = new System6NativeRomSettings
        {
            ProgramRom1Path = System6ProgramRom1Path,
            ProgramRom2Path = System6ProgramRom2Path,
            ProgramRom3Path = System6ProgramRom3Path,
            ProgramRom4Path = System6ProgramRom4Path,
            SoundRom1Path = System6SoundRom1Path,
            SoundRom2Path = System6SoundRom2Path,
            SoundRom3Path = System6SoundRom3Path,
            SoundRom4Path = System6SoundRom4Path,
            FlashSwitch = System6FlashSwitch,
            PercentSwitchValue = System6PercentSwitchValue,
            CoinCommunicationStyle = System6CoinCommunicationStyle,
            CoinCommunicationInvert = System6CoinCommunicationInvert,
            CoinPulseCycles = System6CoinPulseCycles,
            CoinEdcEnabled = System6CoinEdcEnabled,
            ReelOptos = System6ReelOptos.Select(reel => reel.ToModel()).ToList(),
            Coins = System6Coins.Select(coin => coin.ToModel()).ToList()
        };
        SaveLoadedProjectMetadata();
    }

    private void ApplySystem6NativeRomSettingsToViewModel(System6NativeRomSettings settings)
    {
        _system6ProgramRom1Path = settings.ProgramRom1Path;
        _system6ProgramRom2Path = settings.ProgramRom2Path;
        _system6ProgramRom3Path = settings.ProgramRom3Path;
        _system6ProgramRom4Path = settings.ProgramRom4Path;
        _system6SoundRom1Path = settings.SoundRom1Path;
        _system6SoundRom2Path = settings.SoundRom2Path;
        _system6SoundRom3Path = settings.SoundRom3Path;
        _system6SoundRom4Path = settings.SoundRom4Path;
        _system6FlashSwitch = settings.FlashSwitch;
        _system6PercentSwitchValue = Math.Clamp(settings.PercentSwitchValue, 0, 15);
        _system6CoinCommunicationStyle = settings.CoinCommunicationStyle;
        _system6CoinCommunicationInvert = settings.CoinCommunicationInvert;
        _system6CoinPulseCycles = settings.CoinPulseCycles;
        _system6CoinEdcEnabled = settings.CoinEdcEnabled;
        System6ReelOptos = new ObservableCollection<System6ReelOptoSettingsViewModel>((settings.ReelOptos is { Count: > 0 } ? settings.ReelOptos : System6NativeRomSettings.CreateDefaultReelOptos()).Select(reel => new System6ReelOptoSettingsViewModel(reel, SaveSystem6NativeRomSettings)));
        System6Coins = new ObservableCollection<System6CoinSettingsViewModel>(NormalizeSystem6CoinSettings(settings.Coins).Select(coin => new System6CoinSettingsViewModel(coin, SaveSystem6NativeRomSettings)));
        OnPropertyChanged(nameof(System6ProgramRom1Path)); OnPropertyChanged(nameof(System6ProgramRom2Path));
        OnPropertyChanged(nameof(System6ProgramRom3Path)); OnPropertyChanged(nameof(System6ProgramRom4Path));
        OnPropertyChanged(nameof(System6SoundRom1Path)); OnPropertyChanged(nameof(System6SoundRom2Path));
        OnPropertyChanged(nameof(System6SoundRom3Path)); OnPropertyChanged(nameof(System6SoundRom4Path));
        OnPropertyChanged(nameof(System6FlashSwitch));
        OnPropertyChanged(nameof(System6PercentSwitchValue));
        OnPropertyChanged(nameof(System6CoinCommunicationStyle));
        OnPropertyChanged(nameof(System6CoinCommunicationInvert));
        OnPropertyChanged(nameof(System6CoinPulseCycles));
        OnPropertyChanged(nameof(System6CoinEdcEnabled));
    }

    private void RefreshSystem6NativeRomStatus()
    {
        System6NativeRomStatus = string.IsNullOrWhiteSpace(System6ProgramRom1Path) || string.IsNullOrWhiteSpace(System6ProgramRom2Path)
            ? "Program ROM 1 and 2 are required for Fabric Amber launch."
            : "Configured; paths are validated when native emulation starts.";
    }

    private void BrowseSystem6RomPath(int slot, bool isProgramRom)
    {
        if (LoadedProject is null) return;
        var dialog = new OpenFileDialog { Title = $"Select System6 {(isProgramRom ? "Program" : "Sound")} ROM {slot}", Filter = "ROM files|*.bin;*.rom;*.p1;*.p2;*.p3;*.p4;*.snd|All files|*.*", InitialDirectory = LoadedProject.ProjectDirectory, CheckFileExists = true };
        if (dialog.ShowDialog() != true) return;
        var value = MakeProjectRelativePath(dialog.FileName, LoadedProject.ProjectDirectory);
        if (isProgramRom)
        {
            if (slot == 1) System6ProgramRom1Path = value; else if (slot == 2) System6ProgramRom2Path = value; else if (slot == 3) System6ProgramRom3Path = value; else System6ProgramRom4Path = value;
        }
        else
        {
            if (slot == 1) System6SoundRom1Path = value; else if (slot == 2) System6SoundRom2Path = value; else if (slot == 3) System6SoundRom3Path = value; else System6SoundRom4Path = value;
        }
    }

    private void ResetSystem6ReelOptosToDefaults()
    {
        System6ReelOptos = new ObservableCollection<System6ReelOptoSettingsViewModel>(System6NativeRomSettings.CreateDefaultReelOptos().Select(reel => new System6ReelOptoSettingsViewModel(reel, SaveSystem6NativeRomSettings)));
        SaveSystem6NativeRomSettings();
    }

    private static List<System6CoinSettings> NormalizeSystem6CoinSettings(IReadOnlyList<System6CoinSettings>? coins)
    {
        var defaults = System6NativeRomSettings.CreateDefaultCoins();
        if (coins is null || coins.Count == 0)
        {
            return defaults;
        }

        var normalized = new List<System6CoinSettings>(System6NativeRomSettings.DefaultCoinSlotCount);
        for (var index = 0; index < System6NativeRomSettings.DefaultCoinSlotCount; index++)
        {
            normalized.Add(index < coins.Count ? coins[index] : defaults[index]);
        }

        return normalized;
    }

    private System6NativeRomSettings BuildSystem6NativeRomSettingsForLaunch()
    {
        var settings = LoadedProject?.System6NativeRoms ?? new System6NativeRomSettings();
        if (LoadedProject is null) return settings;
        return new System6NativeRomSettings
        {
            ProgramRom1Path = ResolveProjectRelativePath(settings.ProgramRom1Path, LoadedProject.ProjectDirectory),
            ProgramRom2Path = ResolveProjectRelativePath(settings.ProgramRom2Path, LoadedProject.ProjectDirectory),
            ProgramRom3Path = ResolveProjectRelativePath(settings.ProgramRom3Path, LoadedProject.ProjectDirectory),
            ProgramRom4Path = ResolveProjectRelativePath(settings.ProgramRom4Path, LoadedProject.ProjectDirectory),
            SoundRom1Path = ResolveProjectRelativePath(settings.SoundRom1Path, LoadedProject.ProjectDirectory),
            SoundRom2Path = ResolveProjectRelativePath(settings.SoundRom2Path, LoadedProject.ProjectDirectory),
            SoundRom3Path = ResolveProjectRelativePath(settings.SoundRom3Path, LoadedProject.ProjectDirectory),
            SoundRom4Path = ResolveProjectRelativePath(settings.SoundRom4Path, LoadedProject.ProjectDirectory),
            FlashSwitch = settings.FlashSwitch,
            PercentSwitchValue = Math.Clamp(settings.PercentSwitchValue, 0, 15),
            CoinCommunicationStyle = settings.CoinCommunicationStyle,
            CoinCommunicationInvert = settings.CoinCommunicationInvert,
            CoinPulseCycles = settings.CoinPulseCycles,
            CoinEdcEnabled = settings.CoinEdcEnabled,
            ReelOptos = (settings.ReelOptos is { Count: > 0 } ? settings.ReelOptos : System6NativeRomSettings.CreateDefaultReelOptos())
                .Select(reel => new System6ReelOptoSettings { ReelIndex = reel.ReelIndex, Enabled = reel.Enabled, Steps = reel.Steps, OptoStart = reel.OptoStart, OptoEnd = reel.OptoEnd, OptoInvert = reel.OptoInvert })
                .ToList(),
            Coins = NormalizeSystem6CoinSettings(settings.Coins)
                .Select(coin => new System6CoinSettings { Name = coin.Name, Enabled = coin.Enabled, Num = coin.Num, Coin = coin.Coin, CoinValue = coin.CoinValue, CoinEnable = coin.CoinEnable, LockoutInvert = coin.LockoutInvert, CounterIn = coin.CounterIn, CounterOut = coin.CounterOut, PortIndex = coin.PortIndex, Level = coin.Level, FullLevel = coin.FullLevel })
                .ToList()
        };
    }

    private static string ResolveProjectRelativePath(string path, string projectDirectory)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path)) return path;
        return Path.GetFullPath(Path.Combine(projectDirectory, path));
    }

    private static string MakeProjectRelativePath(string path, string projectDirectory)
    {
        var fullPath = Path.GetFullPath(path);
        var fullProjectDirectory = Path.GetFullPath(projectDirectory);
        var relativePath = Path.GetRelativePath(fullProjectDirectory, fullPath);
        return !relativePath.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
               && !relativePath.Equals("..", StringComparison.Ordinal)
               && !Path.IsPathRooted(relativePath)
            ? relativePath
            : fullPath;
    }

    private void OnActiveBackendLampChanged(object? sender, MachineLampChangedEventArgs e) =>
        _machineOutputDispatcher?.EnqueueLamp(e.LampId, e.Value);

    private void OnActiveBackendReelChanged(object? sender, MachineReelChangedEventArgs e) =>
        _machineOutputDispatcher?.EnqueueReel(e.ReelId, e.Position);

    private void OnActiveBackendSegmentChanged(object? sender, MachineSegmentChangedEventArgs e) =>
        _machineOutputDispatcher?.EnqueueSegment(e.CellId, e.SegmentMask, e.OutputType);

    private void OnActiveBackendVfdBrightnessChanged(object? sender, MachineVfdBrightnessChangedEventArgs e) =>
        _machineOutputDispatcher?.EnqueueVfdBrightness(e.CellId, e.NormalizedBrightness);

    private void ApplyMachineOutputBatch(MachineOutputBatch batch)
    {
        foreach (var lamp in batch.Lamps)
            _lampRuntimeAdapter.ApplyLampState(lamp.Id, lamp.Value);
        foreach (var reel in batch.Reels)
            _reelRuntimeAdapter.ApplyReelState(reel.Id, reel.Value);
        foreach (var segment in batch.Segments)
            _segmentRuntimeAdapter.ApplySegmentState(segment.Id, segment.Mask, segment.OutputType);
        foreach (var brightness in batch.VfdBrightness)
            _segmentRuntimeAdapter.ApplyVfdBrightness(brightness.Id, brightness.Value);
    }

    private void OnActiveBackendStateChanged(object? sender, EmulationBackendState state)
    {
        DispatchToUiThread(() =>
        {
            EmulationState = state;
            AddOutputEntry($"Emulation backend state changed to {state}.", OutputLogStatus.Info);
        });
    }

    private bool CanStopEmulation() => _activeEmulationBackend is not null;

    public void StopEmulationForWindowClose()
    {
        if (_activeEmulationBackend is null) return;
        try
        {
            _activeEmulationBackend.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
            DetachAndDisposeActiveBackendAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            AddOutputEntry($"Emulation failed to stop cleanly: {ex.Message}", OutputLogStatus.Error);
        }
    }

    private async void StopEmulation() => await StopEmulationAsync().ConfigureAwait(false);

    private async Task StopEmulationAsync()
    {
        if (_activeEmulationBackend is null) return;
        try
        {
            await _activeEmulationBackend.StopAsync(CancellationToken.None);
            await DetachAndDisposeActiveBackendAsync();
        }
        catch (Exception ex)
        {
            AddOutputEntry($"Emulation failed to stop cleanly: {ex.Message}", OutputLogStatus.Error);
        }
    }

    private async Task DetachAndDisposeActiveBackendAsync()
    {
        var backend = _activeEmulationBackend;
        if (backend is null) return;
        backend.StateChanged -= OnActiveBackendStateChanged;
        backend.LampChanged -= OnActiveBackendLampChanged;
        backend.ReelChanged -= OnActiveBackendReelChanged;
        backend.SegmentChanged -= OnActiveBackendSegmentChanged;
        backend.VfdBrightnessChanged -= OnActiveBackendVfdBrightnessChanged;
        _machineOutputDispatcher?.Detach();
        _machineOutputDispatcher = null;
        await backend.DisposeAsync();
        _activeEmulationBackend = null;
        _playViewInputRouter = null;
        _playViewInputDispatcher = null;
        EmulationState = EmulationBackendState.Stopped;
    }

    private bool CanTogglePauseEmulation() => _activeEmulationBackend is not null && EmulationState is EmulationBackendState.Running or EmulationBackendState.Paused;

    private async void TogglePauseEmulation()
    {
        if (_activeEmulationBackend is null) return;
        if (EmulationState == EmulationBackendState.Paused)
            await SendEmulationCommandAsync(CanTogglePauseEmulation, "Emulation resume requested.", "Emulation failed to resume", _activeEmulationBackend.ResumeAsync);
        else
            await SendEmulationCommandAsync(CanTogglePauseEmulation, "Emulation pause requested.", "Emulation failed to pause", _activeEmulationBackend.PauseAsync);
        OnPropertyChanged(nameof(IsPauseEmulationChecked));
    }

    private bool CanResetEmulation()
    {
        return _activeEmulationBackend is not null && EmulationState is EmulationBackendState.Running or EmulationBackendState.Paused;
    }

    private async void SoftResetEmulation()
    {
        await SendEmulationCommandAsync(CanResetEmulation, "Emulation soft reset requested.",
            "Emulation failed to soft reset", cancellationToken =>
                _activeEmulationBackend!.ResetAsync(EmulationResetKind.Soft, cancellationToken));
    }

    private async void HardResetEmulation()
    {
        await SendEmulationCommandAsync(CanResetEmulation, "Emulation hard reset requested.",
            "Emulation failed to hard reset", cancellationToken =>
                _activeEmulationBackend!.ResetAsync(EmulationResetKind.Hard, cancellationToken));
    }

    private async Task<bool> SendEmulationCommandAsync(
        Func<bool> canExecute,
        string requestedMessage,
        string failureMessage,
        Func<CancellationToken, Task> command)
    {
        if (!canExecute())
        {
            return false;
        }

        AddOutputEntry(requestedMessage, OutputLogStatus.Info);
        try
        {
            await command(CancellationToken.None);
            return true;
        }
        catch (Exception ex)
        {
            AddOutputEntry($"{failureMessage}: {ex.Message}", OutputLogStatus.Error);
            return false;
        }
    }

    private void LoadStartupProject(string startupProjectFilePath)
    {
        var project = LoadProjectFromFile(startupProjectFilePath);
        LoadedProject = project;
        SelectedFruitMachinePlatform = project.FruitMachinePlatform;
        ApplySystem6NativeRomSettingsToViewModel(project.System6NativeRoms);
        RefreshSystem6NativeRomStatus();
        ProjectAssetPathResolver.ProjectDirectoryPath = project.ProjectDirectory;
        ProjectFilePath = project.ProjectFilePath;
        UpdateRecentProjects(project.ProjectFilePath);
        _assetBrowser.RefreshAssetBrowser();
        StatusMessage = $"Project opened: {project.Name} ({project.ProjectFilePath})";
        AddOutputEntry($"Loaded startup project '{project.Name}' from {project.ProjectFilePath}", OutputLogStatus.Info);
        RefreshInputMapDiagnostics();
    }

    private void RefreshInputMapDiagnostics()
    {
        if (LoadedProject is null)
        {
            InputMapDiagnostics = [];
            OnPropertyChanged(nameof(InputMapWarningCount));
            OnPropertyChanged(nameof(HasInputMapDiagnostics));
            return;
        }

        InputMapDiagnostics = _inputMapDiagnosticsService.Analyze(SelectedFruitMachinePlatform, LoadedProject.InputDefinitions);
        OnPropertyChanged(nameof(InputMapWarningCount));
        OnPropertyChanged(nameof(HasInputMapDiagnostics));
        var warningCount = InputMapWarningCount;
        if (warningCount > 0)
        {
            AddOutputEntry($"Input Map diagnostics reported {warningCount} warning(s).", OutputLogStatus.Warning);
        }
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
        var system6NativeRoms = ResolveSystem6NativeRomSettings(projectDocument.RootElement);
        var mpu5NativeRoms = ResolveMpu5NativeRomSettings(projectDocument.RootElement);
        var inputDefinitions = ResolveInputDefinitions(projectDocument.RootElement);

        return new EditorProject
        {
            Name = openedProjectName,
            ProjectFilePath = projectFilePath,
            ProjectDirectory = projectDirectory,
            AssetsDirectory = assetsDirectory,
            MachinesDirectory = machinesDirectory,
            GeneratedDirectory = generatedDirectory,
            FruitMachinePlatform = fruitMachinePlatform,
            System6NativeRoms = system6NativeRoms,
            Mpu5NativeRoms = mpu5NativeRoms
        }.WithInputDefinitions(inputDefinitions);
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
        try
        {
            var executed = _documentWorkspace.ExecuteDocumentCanvasCommand(documentId, command);
            if (executed)
            {
                NotifyInspectorChanged();
            }
            else
            {
                AddOutputEntry($"Command '{command.Description}' was not executed for document '{documentId:N}'.", OutputLogStatus.Warning);
            }

            return executed;
        }
        catch (Exception exception)
        {
            AddOutputEntry($"Command '{command.Description}' failed for document '{documentId:N}': {exception.Message}", OutputLogStatus.Error);
            return false;
        }
    }

    public void UpdateDocumentPanelSelection(Guid documentId, PanelSelectionInfo? selection)
    {
        var document = OpenDocuments.FirstOrDefault(tab => tab.DocumentId == documentId);
        var selectionChanged = true;
        if (document is not null)
        {
            var currentSelection = document.HierarchySelectedPanelSelection;
            selectionChanged = !ArePanelSelectionsEqual(currentSelection, selection);
            if (selectionChanged)
            {
                document.HierarchySelectedPanelSelection = selection;
            }
        }

        _activeDocumentContext.SetPanelSelection(documentId, selection);
        _inspector.ActivateDocumentInspection();
        if (selectionChanged)
        {
            _hierarchy.SyncSelection(document?.SelectionState);
        }
        NotifyInspectorChanged();
        OnPropertyChanged(nameof(HierarchyItems));
    }

    private static bool ArePanelSelectionsEqual(PanelSelectionInfo? left, PanelSelectionInfo? right)
    {
        return left is null
            ? right is null
            : right is PanelSelectionInfo rightSelection
              && PanelSelectionContract.IsSameSelection(left.Value, rightSelection);
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
                var wroteInputDefinitions = false;

                foreach (var property in projectDocument.RootElement.EnumerateObject())
                {
                    if (property.NameEquals("project_settings"))
                    {
                        wroteProjectSettings = true;
                        writer.WritePropertyName("project_settings");
                        WriteProjectSettings(writer, property.Value, LoadedProject.FruitMachinePlatform, LoadedProject.System6NativeRoms, LoadedProject.Mpu5NativeRoms);
                        continue;
                    }

                    if (property.NameEquals("input_definitions"))
                    {
                        wroteInputDefinitions = true;
                        writer.WritePropertyName("input_definitions");
                        WriteInputDefinitions(writer, LoadedProject.InputDefinitions);
                        continue;
                    }

                    property.WriteTo(writer);
                }

                if (!wroteProjectSettings)
                {
                    writer.WritePropertyName("project_settings");
                    writer.WriteStartObject();
                    writer.WriteString("FruitMachine_Platform", LoadedProject.FruitMachinePlatform.ToString());
                    WriteSystem6NativeRomSettings(writer, LoadedProject.System6NativeRoms);
                    WriteMpu5NativeRomSettings(writer, LoadedProject.Mpu5NativeRoms);
                    writer.WriteEndObject();
                }

                if (!wroteInputDefinitions)
                {
                    writer.WritePropertyName("input_definitions");
                    WriteInputDefinitions(writer, LoadedProject.InputDefinitions);
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

    private static void WriteProjectSettings(Utf8JsonWriter writer, JsonElement existingProjectSettings, FruitMachinePlatformType platform, System6NativeRomSettings system6NativeRoms, Mpu5NativeRomSettings mpu5NativeRoms)
    {
        writer.WriteStartObject();
        var wrotePlatform = false;
        var wroteSystem6Settings = false;
        var wroteMpu5Settings = false;
        foreach (var property in existingProjectSettings.EnumerateObject())
        {
            if (property.NameEquals("FruitMachine_Platform"))
            {
                writer.WriteString("FruitMachine_Platform", platform.ToString());
                wrotePlatform = true;
            }
            else if (property.NameEquals("System6NativeRoms"))
            {
                WriteSystem6NativeRomSettings(writer, system6NativeRoms);
                wroteSystem6Settings = true;
            }
            else if (property.NameEquals("Mpu5NativeRoms"))
            {
                WriteMpu5NativeRomSettings(writer, mpu5NativeRoms);
                wroteMpu5Settings = true;
            }
            else if (!property.NameEquals("MameRomName") && !property.NameEquals("AutomaticallyDownloadMissingRoms"))
            {
                property.WriteTo(writer);
            }
        }
        if (!wrotePlatform) writer.WriteString("FruitMachine_Platform", platform.ToString());
        if (!wroteSystem6Settings) WriteSystem6NativeRomSettings(writer, system6NativeRoms);
        if (!wroteMpu5Settings) WriteMpu5NativeRomSettings(writer, mpu5NativeRoms);
        writer.WriteEndObject();
    }

    private static void WriteMpu5NativeRomSettings(Utf8JsonWriter writer, Mpu5NativeRomSettings settings)
    {
        writer.WritePropertyName("Mpu5NativeRoms");
        JsonSerializer.Serialize(writer, settings);
    }

    private static Mpu5NativeRomSettings ResolveMpu5NativeRomSettings(JsonElement root)
    {
        if (!root.TryGetProperty("project_settings", out var projectSettings)
            || !projectSettings.TryGetProperty("Mpu5NativeRoms", out var settings))
            return new Mpu5NativeRomSettings();
        return settings.Deserialize<Mpu5NativeRomSettings>()
            ?? throw new InvalidOperationException("MPU5 native ROM settings are invalid.");
    }

    private static void WriteSystem6NativeRomSettings(Utf8JsonWriter writer, System6NativeRomSettings settings)
    {
        writer.WritePropertyName("System6NativeRoms");
        writer.WriteStartObject();
        writer.WriteString("ProgramRom1Path", settings.ProgramRom1Path);
        writer.WriteString("ProgramRom2Path", settings.ProgramRom2Path);
        writer.WriteString("ProgramRom3Path", settings.ProgramRom3Path);
        writer.WriteString("ProgramRom4Path", settings.ProgramRom4Path);
        writer.WriteString("SoundRom1Path", settings.SoundRom1Path);
        writer.WriteString("SoundRom2Path", settings.SoundRom2Path);
        writer.WriteString("SoundRom3Path", settings.SoundRom3Path);
        writer.WriteString("SoundRom4Path", settings.SoundRom4Path);
        writer.WriteBoolean("FlashSwitch", settings.FlashSwitch);
        writer.WriteNumber("PercentSwitchValue", Math.Clamp(settings.PercentSwitchValue, 0, 15));
        writer.WriteNumber("CoinCommunicationStyle", (uint)settings.CoinCommunicationStyle);
        writer.WriteBoolean("CoinCommunicationInvert", settings.CoinCommunicationInvert);
        writer.WriteNumber("CoinPulseCycles", settings.CoinPulseCycles);
        writer.WriteBoolean("CoinEdcEnabled", settings.CoinEdcEnabled);
        writer.WritePropertyName("ReelOptos");
        writer.WriteStartArray();
        foreach (var reel in settings.ReelOptos)
        {
            writer.WriteStartObject();
            writer.WriteNumber("ReelIndex", reel.ReelIndex);
            writer.WriteBoolean("Enabled", reel.Enabled);
            writer.WriteNumber("Steps", reel.Steps);
            writer.WriteNumber("OptoStart", reel.OptoStart);
            writer.WriteNumber("OptoEnd", reel.OptoEnd);
            writer.WriteBoolean("OptoInvert", reel.OptoInvert);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.WritePropertyName("Coins");
        writer.WriteStartArray();
        foreach (var coin in settings.Coins)
        {
            writer.WriteStartObject();
            writer.WriteString("Name", coin.Name);
            writer.WriteBoolean("Enabled", coin.Enabled);
            writer.WriteNumber("Num", coin.Num);
            writer.WriteNumber("Coin", coin.Coin);
            writer.WriteNumber("CoinValue", coin.CoinValue);
            writer.WriteNumber("CoinEnable", coin.CoinEnable);
            writer.WriteNumber("LockoutInvert", coin.LockoutInvert);
            writer.WriteNumber("CounterIn", coin.CounterIn);
            writer.WriteNumber("CounterOut", coin.CounterOut);
            writer.WriteNumber("PortIndex", coin.PortIndex);
            writer.WriteNumber("Level", coin.Level);
            writer.WriteNumber("FullLevel", coin.FullLevel);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static System6NativeRomSettings ResolveSystem6NativeRomSettings(JsonElement root)
    {
        if (!root.TryGetProperty("project_settings", out var projectSettingsElement)
            || !projectSettingsElement.TryGetProperty("System6NativeRoms", out var romsElement))
        {
            return new System6NativeRomSettings();
        }

        return new System6NativeRomSettings
        {
            ProgramRom1Path = GetOptionalString(romsElement, "ProgramRom1Path"),
            ProgramRom2Path = GetOptionalString(romsElement, "ProgramRom2Path"),
            ProgramRom3Path = GetOptionalString(romsElement, "ProgramRom3Path"),
            ProgramRom4Path = GetOptionalString(romsElement, "ProgramRom4Path"),
            SoundRom1Path = GetOptionalString(romsElement, "SoundRom1Path"),
            SoundRom2Path = GetOptionalString(romsElement, "SoundRom2Path"),
            SoundRom3Path = GetOptionalString(romsElement, "SoundRom3Path"),
            SoundRom4Path = GetOptionalString(romsElement, "SoundRom4Path"),
            FlashSwitch = romsElement.TryGetProperty("FlashSwitch", out var flashElement) && flashElement.ValueKind == JsonValueKind.True,
            PercentSwitchValue = Math.Clamp(GetOptionalInt(romsElement, "PercentSwitchValue", System6NativeRomSettings.DefaultPercentSwitchValue), 0, 15),
            CoinCommunicationStyle = (AmberCoinCommunicationStyle)GetOptionalInt(romsElement, "CoinCommunicationStyle", 0),
            CoinCommunicationInvert = romsElement.TryGetProperty("CoinCommunicationInvert", out var communicationInvert) && communicationInvert.ValueKind == JsonValueKind.True,
            CoinPulseCycles = checked((uint)GetOptionalInt(romsElement, "CoinPulseCycles", 800_000)),
            CoinEdcEnabled = romsElement.TryGetProperty("CoinEdcEnabled", out var edcEnabled) && edcEnabled.ValueKind == JsonValueKind.True,
            ReelOptos = ResolveSystem6ReelOptoSettings(romsElement),
            Coins = ResolveSystem6CoinSettings(romsElement)
        };
    }


    private static List<System6ReelOptoSettings> ResolveSystem6ReelOptoSettings(JsonElement romsElement)
    {
        if (!romsElement.TryGetProperty("ReelOptos", out var reelOptosElement) || reelOptosElement.ValueKind != JsonValueKind.Array)
        {
            return System6NativeRomSettings.CreateDefaultReelOptos();
        }

        var reelOptos = new List<System6ReelOptoSettings>();
        foreach (var reelElement in reelOptosElement.EnumerateArray())
        {
            reelOptos.Add(new System6ReelOptoSettings
            {
                ReelIndex = GetOptionalInt(reelElement, "ReelIndex"),
                Enabled = !reelElement.TryGetProperty("Enabled", out var enabledElement) || enabledElement.ValueKind != JsonValueKind.False,
                Steps = GetOptionalInt(reelElement, "Steps", System6ReelOptoSettings.DefaultSteps),
                OptoStart = GetOptionalInt(reelElement, "OptoStart", System6ReelOptoSettings.DefaultOptoStart),
                OptoEnd = GetOptionalInt(reelElement, "OptoEnd", System6ReelOptoSettings.DefaultOptoEnd),
                OptoInvert = reelElement.TryGetProperty("OptoInvert", out var invertElement) && invertElement.ValueKind == JsonValueKind.True
            });
        }

        return reelOptos.Count > 0 ? reelOptos : System6NativeRomSettings.CreateDefaultReelOptos();
    }

    private static List<System6CoinSettings> ResolveSystem6CoinSettings(JsonElement romsElement)
    {
        if (!romsElement.TryGetProperty("Coins", out var coinsElement) || coinsElement.ValueKind != JsonValueKind.Array)
        {
            return System6NativeRomSettings.CreateDefaultCoins();
        }

        var coins = new List<System6CoinSettings>();
        foreach (var coinElement in coinsElement.EnumerateArray().Take(System6NativeRomSettings.DefaultCoinSlotCount))
        {
            var coinIndex = coins.Count;
            coins.Add(new System6CoinSettings
            {
                Name = GetOptionalString(coinElement, "Name", $"Coin {coinIndex + 1}"),
                Enabled = coinElement.TryGetProperty("Enabled", out var enabledElement) && enabledElement.ValueKind == JsonValueKind.True,
                Num = GetOptionalInt(coinElement, "Num", coinIndex),
                Coin = GetOptionalInt(coinElement, "Coin", System6CoinSettings.DefaultCoin),
                CoinValue = GetOptionalInt(coinElement, "CoinValue", System6CoinSettings.DefaultCoinValue),
                CoinEnable = GetOptionalInt(coinElement, "CoinEnable", System6CoinSettings.DefaultCoinEnable),
                LockoutInvert = GetOptionalInt(coinElement, "LockoutInvert", System6CoinSettings.DefaultLockoutInvert),
                CounterIn = GetOptionalInt(coinElement, "CounterIn"),
                CounterOut = GetOptionalInt(coinElement, "CounterOut"),
                PortIndex = GetOptionalInt(coinElement, "PortIndex"),
                Level = GetOptionalInt(coinElement, "Level"),
                FullLevel = GetOptionalInt(coinElement, "FullLevel")
            });
        }

        return NormalizeSystem6CoinSettings(coins);
    }

    private static int GetOptionalInt(JsonElement element, string propertyName, int defaultValue = 0)
    {
        return element.TryGetProperty(propertyName, out var value) && value.TryGetInt32(out var parsed) ? parsed : defaultValue;
    }

    private static string GetOptionalString(JsonElement element, string propertyName, string defaultValue = "")
    {
        return element.TryGetProperty(propertyName, out var value) ? value.GetString() ?? defaultValue : defaultValue;
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

    private static void WriteInputDefinitions(Utf8JsonWriter writer, IReadOnlyList<InputDefinitionModel> inputDefinitions)
    {
        writer.WriteStartArray();

        foreach (var input in inputDefinitions)
        {
            writer.WriteStartObject();
            writer.WriteString("Id", input.Id);
            writer.WriteString("Name", input.Name);
            writer.WriteString("Kind", input.Kind.ToString());
            writer.WriteString("ButtonNumber", input.ButtonNumber);
            writer.WriteBoolean("CoinInput", input.CoinInput);
            if (input.CoinChannel.HasValue) writer.WriteNumber("CoinChannel", input.CoinChannel.Value);
            if (input.CoinValue.HasValue) writer.WriteNumber("CoinValue", input.CoinValue.Value);
            writer.WriteBoolean("Inverted", input.Inverted);
            writer.WriteString("RawMfmeShortcut", input.RawMfmeShortcut);
            writer.WriteString("KeyboardShortcut", input.KeyboardShortcut);
            if (input.LinkedVisualElementId.HasValue)
            {
                writer.WriteString("LinkedVisualElementId", input.LinkedVisualElementId.Value);
            }
            writer.WriteString("Notes", input.Notes);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static List<InputDefinitionModel> ResolveInputDefinitions(JsonElement root)
    {
        if (!root.TryGetProperty("input_definitions", out var inputDefinitionsElement)
            || inputDefinitionsElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var definitions = new List<InputDefinitionModel>();
        foreach (var inputElement in inputDefinitionsElement.EnumerateArray())
        {
            if (inputElement.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var id = inputElement.TryGetProperty("Id", out var idElement) ? idElement.GetString() : null;
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            var kindRaw = inputElement.TryGetProperty("Kind", out var kindElement) ? kindElement.GetString() : null;
            _ = Enum.TryParse<InputDefinitionKind>(kindRaw, true, out var kind);

            Guid? linkedVisualId = null;
            if (inputElement.TryGetProperty("LinkedVisualElementId", out var linkedElement)
                && linkedElement.ValueKind == JsonValueKind.String
                && Guid.TryParse(linkedElement.GetString(), out var parsedLinkedVisualId))
            {
                linkedVisualId = parsedLinkedVisualId;
            }

            definitions.Add(new InputDefinitionModel
            {
                Id = id,
                Name = inputElement.TryGetProperty("Name", out var nameElement) ? nameElement.GetString() ?? string.Empty : string.Empty,
                Kind = kind,
                ButtonNumber = inputElement.TryGetProperty("ButtonNumber", out var buttonNumberElement) ? buttonNumberElement.GetString() ?? string.Empty : string.Empty,
                CoinInput = inputElement.TryGetProperty("CoinInput", out var coinInputElement) && coinInputElement.ValueKind == JsonValueKind.True,
                CoinChannel = inputElement.TryGetProperty("CoinChannel", out var coinChannelElement) && coinChannelElement.TryGetInt32(out var coinChannel) ? coinChannel : null,
                CoinValue = inputElement.TryGetProperty("CoinValue", out var inputCoinValueElement) && inputCoinValueElement.TryGetInt32(out var inputCoinValue) ? inputCoinValue : null,
                Inverted = inputElement.TryGetProperty("Inverted", out var invertedElement) && invertedElement.ValueKind == JsonValueKind.True,
                RawMfmeShortcut = inputElement.TryGetProperty("RawMfmeShortcut", out var rawShortcutElement) ? rawShortcutElement.GetString() ?? string.Empty : string.Empty,
                KeyboardShortcut = inputElement.TryGetProperty("KeyboardShortcut", out var keyboardShortcutElement) ? keyboardShortcutElement.GetString() ?? string.Empty : string.Empty,
                LinkedVisualElementId = linkedVisualId,
                Notes = inputElement.TryGetProperty("Notes", out var notesElement) ? notesElement.GetString() ?? string.Empty : string.Empty
            });
        }

        return definitions;
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
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is not null
            && !dispatcher.HasShutdownStarted
            && !dispatcher.HasShutdownFinished
            && !dispatcher.CheckAccess())
        {
            _ = dispatcher.BeginInvoke(() => _outputLog.AddOutputEntry(message, status));
            return;
        }

        _outputLog.AddOutputEntry(message, status);
    }

    private void OnOutputLogPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(OutputLogViewModel.ShowInfoLogs)
            or nameof(OutputLogViewModel.ShowWarningLogs)
            or nameof(OutputLogViewModel.ShowErrorLogs)
            or nameof(OutputLogViewModel.AutoScroll)
            or nameof(OutputLogViewModel.SearchText))
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

        if (OpenFaceStubCommand is RelayCommand openFaceRelayCommand)
        {
            openFaceRelayCommand.RaiseCanExecuteChanged();
        }

        if (AddFaceSourceShapeCommand is RelayCommand addFaceSourceShapeRelayCommand)
        {
            addFaceSourceShapeRelayCommand.RaiseCanExecuteChanged();
        }

        if (GenerateFaceFromSourceShapeCommand is RelayCommand generateFaceFromSourceShapeRelayCommand)
        {
            generateFaceFromSourceShapeRelayCommand.RaiseCanExecuteChanged();
        }

        if (RegenerateFaceCommand is RelayCommand regenerateFaceRelayCommand)
        {
            regenerateFaceRelayCommand.RaiseCanExecuteChanged();
        }

        if (OpenFaceGenerationSettingsCommand is RelayCommand faceGenerationSettingsRelayCommand)
        {
            faceGenerationSettingsRelayCommand.RaiseCanExecuteChanged();
        }

        if (ValidateFaceCommand is RelayCommand validateFaceRelayCommand)
        {
            validateFaceRelayCommand.RaiseCanExecuteChanged();
        }

        if (OpenSourcePanel2DCommand is RelayCommand openSourcePanelRelayCommand)
        {
            openSourcePanelRelayCommand.RaiseCanExecuteChanged();
        }

        if (OpenCabinet3DStubCommand is RelayCommand openCabinetRelayCommand)
        {
            openCabinetRelayCommand.RaiseCanExecuteChanged();
        }

        if (OpenMachineStubCommand is RelayCommand openMachineRelayCommand)
        {
            openMachineRelayCommand.RaiseCanExecuteChanged();
        }

        if (ImportMfmeFmlCommand is RelayCommand importMfmeFmlRelayCommand)
        {
            importMfmeFmlRelayCommand.RaiseCanExecuteChanged();
        }

        if (ImportGlbModelCommand is RelayCommand importGlbRelayCommand)
        {
            importGlbRelayCommand.RaiseCanExecuteChanged();
        }

        if (BuildOasisPlayerMachineCommand is RelayCommand buildOasisPlayerRelayCommand)
        {
            buildOasisPlayerRelayCommand.RaiseCanExecuteChanged();
        }

        if (PreviewInOasisPlayerCommand is RelayCommand previewInOasisPlayerRelayCommand)
        {
            previewInOasisPlayerRelayCommand.RaiseCanExecuteChanged();
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
        RaiseEmulationCommandCanExecuteChanged(StartEmulationCommand);
        RaiseEmulationCommandCanExecuteChanged(StopEmulationCommand);
        RaiseEmulationCommandCanExecuteChanged(TogglePauseEmulationCommand);
        RaiseEmulationCommandCanExecuteChanged(SoftResetEmulationCommand);
        RaiseEmulationCommandCanExecuteChanged(HardResetEmulationCommand);
    }

    private static void RaiseEmulationCommandCanExecuteChanged(ICommand command)
    {
        if (command is RelayCommand relayCommand)
        {
            relayCommand.RaiseCanExecuteChanged();
        }
    }

    private void NotifyUndoRedoStateChanged()
    {
        OnPropertyChanged(nameof(UndoMenuHeader));
        OnPropertyChanged(nameof(RedoMenuHeader));
        NotifyInspectorChanged();
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
            if (SelectedDocument is not null)
            {
                _activeDocumentContext.SetPanelSelection(SelectedDocument.DocumentId, SelectedDocument.HierarchySelectedPanelSelection);
            }

            _hierarchy.SyncSelection(SelectedDocument?.SelectionState);
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

    private void BeginEditorProgress(string message, double progress = 0d, bool indeterminate = false)
    {
        EditorProgressMessage = message;
        EditorProgressPercent = Math.Clamp(progress, 0d, 1d) * 100d;
        IsEditorProgressIndeterminate = indeterminate;
        IsEditorProgressVisible = true;
    }

    private void ReportEditorProgress(string message, double progress)
    {
        EditorProgressMessage = message;
        EditorProgressPercent = Math.Clamp(progress, 0d, 1d) * 100d;
        IsEditorProgressIndeterminate = false;
        IsEditorProgressVisible = true;
    }

    private void EndEditorProgress()
    {
        IsEditorProgressVisible = false;
        IsEditorProgressIndeterminate = false;
        EditorProgressPercent = 0d;
        EditorProgressMessage = string.Empty;
    }

    private static async Task YieldForProgressRenderAsync()
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            await Task.Yield();
            return;
        }

        await dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Render);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    internal static string FormatWindowTitle(string? projectName)
    {
        var trimmedProjectName = projectName?.Trim();
        return string.IsNullOrWhiteSpace(trimmedProjectName)
            ? "Oasis Editor"
            : $"{trimmedProjectName} - Oasis Editor";
    }

}

internal readonly record struct OpenDocumentData(string Summary, string? PanelLayoutJson, string? PanelTitle = null, string? FaceDocumentJson = null, string? CabinetDocumentJson = null);


internal static class EditorProjectInputDefinitionExtensions
{
    public static EditorProject WithInputDefinitions(this EditorProject project, IReadOnlyList<InputDefinitionModel> definitions)
    {
        project.InputDefinitions.Clear();
        foreach (var definition in definitions)
        {
            project.InputDefinitions.Add(definition);
        }

        return project;
    }
}


public sealed class System6CoinSettingsViewModel : INotifyPropertyChanged
{
    private readonly Action _changed;
    private string _name;
    private bool _enabled;
    private int _num;
    private int _coin;
    private int _coinValue;
    private int _coinEnable;
    private int _lockoutInvert;
    private int _counterIn;
    private int _counterOut;
    private int _portIndex;
    private int _level;
    private int _fullLevel;

    public System6CoinSettingsViewModel(System6CoinSettings model, Action changed)
    {
        _name = model.Name;
        _enabled = model.Enabled;
        _num = model.Num;
        _coin = model.Coin;
        _coinValue = model.CoinValue;
        _coinEnable = model.CoinEnable;
        _lockoutInvert = model.LockoutInvert;
        _counterIn = model.CounterIn;
        _counterOut = model.CounterOut;
        _portIndex = model.PortIndex;
        _level = model.Level;
        _fullLevel = model.FullLevel;
        _changed = changed;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Name { get => _name; set => SetAndSave(ref _name, value, nameof(Name)); }
    public bool Enabled { get => _enabled; set => SetAndSave(ref _enabled, value, nameof(Enabled)); }
    public int Num { get => _num; set => SetAndSave(ref _num, Math.Clamp(value, 0, byte.MaxValue), nameof(Num)); }
    public int Coin { get => _coin; set => SetAndSave(ref _coin, Math.Clamp(value, 0, byte.MaxValue), nameof(Coin)); }
    public int CoinValue { get => _coinValue; set => SetAndSave(ref _coinValue, Math.Clamp(value, 0, byte.MaxValue), nameof(CoinValue)); }
    public int CoinEnable { get => _coinEnable; set => SetAndSave(ref _coinEnable, Math.Clamp(value, 0, byte.MaxValue), nameof(CoinEnable)); }
    public int LockoutInvert { get => _lockoutInvert; set => SetAndSave(ref _lockoutInvert, Math.Clamp(value, 0, byte.MaxValue), nameof(LockoutInvert)); }
    public int CounterIn { get => _counterIn; set => SetAndSave(ref _counterIn, Math.Clamp(value, 0, byte.MaxValue), nameof(CounterIn)); }
    public int CounterOut { get => _counterOut; set => SetAndSave(ref _counterOut, Math.Clamp(value, 0, byte.MaxValue), nameof(CounterOut)); }
    public int PortIndex { get => _portIndex; set => SetAndSave(ref _portIndex, Math.Clamp(value, 0, byte.MaxValue), nameof(PortIndex)); }
    public int Level { get => _level; set => SetAndSave(ref _level, Math.Clamp(value, 0, byte.MaxValue), nameof(Level)); }
    public int FullLevel { get => _fullLevel; set => SetAndSave(ref _fullLevel, Math.Clamp(value, 0, byte.MaxValue), nameof(FullLevel)); }

    public System6CoinSettings ToModel() => new()
    {
        Name = Name,
        Enabled = Enabled,
        Num = Num,
        Coin = Coin,
        CoinValue = CoinValue,
        CoinEnable = CoinEnable,
        LockoutInvert = LockoutInvert,
        CounterIn = CounterIn,
        CounterOut = CounterOut,
        PortIndex = PortIndex,
        Level = Level,
        FullLevel = FullLevel
    };

    private void SetAndSave<T>(ref T field, T value, string propertyName)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        _changed();
    }
}

public sealed class System6ReelOptoSettingsViewModel : INotifyPropertyChanged
{
    private readonly Action _changed;
    private bool _enabled;
    private int _steps;
    private int _optoStart;
    private int _optoEnd;
    private bool _optoInvert;

    public System6ReelOptoSettingsViewModel(System6ReelOptoSettings model, Action changed)
    {
        ReelIndex = model.ReelIndex;
        _enabled = model.Enabled;
        _steps = model.Steps;
        _optoStart = model.OptoStart;
        _optoEnd = model.OptoEnd;
        _optoInvert = model.OptoInvert;
        _changed = changed;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public int ReelIndex { get; }
    public int ReelNumber => ReelIndex;

    public bool Enabled { get => _enabled; set => SetAndSave(ref _enabled, value, nameof(Enabled)); }
    public int Steps { get => _steps; set => SetAndSave(ref _steps, value, nameof(Steps)); }
    public int OptoStart { get => _optoStart; set => SetAndSave(ref _optoStart, value, nameof(OptoStart)); }
    public int OptoEnd { get => _optoEnd; set => SetAndSave(ref _optoEnd, value, nameof(OptoEnd)); }
    public bool OptoInvert { get => _optoInvert; set => SetAndSave(ref _optoInvert, value, nameof(OptoInvert)); }

    public System6ReelOptoSettings ToModel() => new()
    {
        ReelIndex = ReelIndex,
        Enabled = Enabled,
        Steps = Steps,
        OptoStart = OptoStart,
        OptoEnd = OptoEnd,
        OptoInvert = OptoInvert
    };

    private void SetAndSave<T>(ref T field, T value, string propertyName)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        _changed();
    }
}
