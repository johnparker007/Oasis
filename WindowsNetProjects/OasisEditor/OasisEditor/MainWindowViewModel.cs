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
using OasisEditor.Features.MameDebugger;
using OasisEditor.Features.MameDebugger.ViewModels;
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
    private const int NativeSystem6SevenSegmentCellStride = 16;
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
    private string _mameVersion = "0267";
    private string _mameExecutablePath = string.Empty;
    private string _mameInstallRootDirectory = string.Empty;
    private string _mameReleaseSource = string.Empty;
    private string _mameLuaPluginPath = string.Empty;
    private string _mameCommandLineOverrides = string.Empty;
    private string _system6NativeLibraryPath = string.Empty;
    private int _system6AudioBufferLengthMilliseconds = NativeEmulationPreferences.DefaultAudioBufferLengthMilliseconds;
    private string _mameRomDownloadBaseUrl = MameRomDownloadService.DefaultDownloadRootUrl;
    private string _mameRomArchiveExtension = MameRomDownloadService.DefaultArchiveExtension;
    private string _mameLocalRomSourceDirectory = string.Empty;
    private string _mameLocalRomArchiveExtension = MameRomDownloadService.DefaultArchiveExtension;
    private bool _keepMameUpToDateAutomatically = true;
    private bool _debugOutputLamps;
    private bool _debugOutputStdIn;
    private bool _debugOutputStdOut;
    private string _lastMfmeFmlImportDirectory = string.Empty;
    private FaceGenerationSettingsModel _defaultFaceGenerationSettings = FaceGenerationSettingsModel.Default;
    private bool _showFaceGenerationSettingsBeforeRegenerate = true;
    private string _mameValidationSummary = "Not validated.";
    private string _oasisPlayerExecutablePath = string.Empty;
    private bool _oasisPlayerFullscreen;
    private int _oasisPlayerPreviewWidth = OasisPlayerLaunchService.DefaultPreviewWidth;
    private int _oasisPlayerPreviewHeight = OasisPlayerLaunchService.DefaultPreviewHeight;
    private string _selectedPreferencesCategory = "Appearance";
    private string _selectedProjectSettingsCategory = "General";
    private string _selectedNativeProjectSettingsTab = "ROMS";
    private FruitMachinePlatformType _selectedFruitMachinePlatform = FruitMachinePlatformType.None;
    private string _mameRomName = string.Empty;
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
    private string _system6NativeRomStatus = "Program ROM 1 and 2 are required for native DLL launch.";
    private ObservableCollection<System6ReelOptoSettingsViewModel> _system6ReelOptos = [];
    private ObservableCollection<System6CoinSettingsViewModel> _system6Coins = [];
    private string _mameRomStatus = "Unknown";
    private bool _isMameRomDownloadInProgress;
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
    private readonly MameDownloadService _mameDownloadService = new();
    private readonly MameRomDownloadService _mameRomDownloadService = new();
    private readonly MamePluginAssetValidator _mamePluginAssetValidator = new();
    private readonly MamePluginDeploymentService _mamePluginDeploymentService = new();
    private readonly IMameSetupOrchestrator _mameSetupOrchestrator;
    private readonly IMameVersionCatalogService _mameVersionCatalogService;
    private bool _isLoadingPreferences;
    private readonly IMameEmulationService _mameEmulationService;
    private readonly IMameProcessRunner _mameProcessRunner;
    private readonly IEmulationBackendFactory _emulationBackendFactory;
    private readonly IMameLampRuntimeAdapter _lampRuntimeAdapter;
    private readonly IMameReelRuntimeAdapter _reelRuntimeAdapter;
    private readonly IMameSegmentRuntimeAdapter _segmentRuntimeAdapter;
    private IEmulationBackend? _activeEmulationBackend;
    private readonly IMameDebuggerService _mameDebuggerService;
    private readonly MameDebuggerShellViewModel _mameDebuggerShell;
    private MameSetupState _mameSetupState = MameSetupState.NotStarted;
    private bool _isAutoProvisioningMame;
    private bool _isMameUnthrottled;
    private MameEmulationState _mameEmulationState = MameEmulationState.Stopped;
    private readonly IInputMapDiagnosticsService _inputMapDiagnosticsService = new InputMapDiagnosticsService(new MameInputPortResolver());
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
        OpenDebuggerControlCommand = new RelayCommand(OpenDebuggerControl);
        OpenDebuggerDisassemblyCommand = new RelayCommand(OpenDebuggerDisassembly);
        OpenDebuggerRegistersCommand = new RelayCommand(OpenDebuggerRegisters);
        OpenDebuggerMemoryCommand = new RelayCommand(OpenDebuggerMemory);
        OpenDebuggerBreakpointsCommand = new RelayCommand(OpenDebuggerBreakpoints);
        OpenDebuggerWatchpointsCommand = new RelayCommand(OpenDebuggerWatchpoints);
        ClosePreferencesCommand = new RelayCommand(ClosePreferences);
        BrowseMameExecutableCommand = new RelayCommand(BrowseMameExecutable);
        BrowseOasisPlayerExecutableCommand = new RelayCommand(BrowseOasisPlayerExecutable);
        ValidateMamePreferencesCommand = new RelayCommand(ValidateMamePreferences);
        RefreshMameVersionsCommand = new RelayCommand(RefreshMameVersions);
        DownloadMameVersionCommand = new RelayCommand(DownloadMameVersion);
        OpenMameInstallRootCommand = new RelayCommand(OpenMameInstallRoot);
        ResyncMamePluginsCommand = new RelayCommand(ResyncMamePlugins);
        RemoveCachedMameVersionCommand = new RelayCommand(RemoveCachedMameVersion);
        DownloadMameRomCommand = new RelayCommand(DownloadMameRom, CanDownloadMameRom);
        BrowseSystem6ProgramRom1Command = new RelayCommand(() => BrowseSystem6RomPath(1, true));
        BrowseSystem6ProgramRom2Command = new RelayCommand(() => BrowseSystem6RomPath(2, true));
        BrowseSystem6ProgramRom3Command = new RelayCommand(() => BrowseSystem6RomPath(3, true));
        BrowseSystem6ProgramRom4Command = new RelayCommand(() => BrowseSystem6RomPath(4, true));
        BrowseSystem6SoundRom1Command = new RelayCommand(() => BrowseSystem6RomPath(1, false));
        BrowseSystem6SoundRom2Command = new RelayCommand(() => BrowseSystem6RomPath(2, false));
        BrowseSystem6SoundRom3Command = new RelayCommand(() => BrowseSystem6RomPath(3, false));
        BrowseSystem6SoundRom4Command = new RelayCommand(() => BrowseSystem6RomPath(4, false));
        ResetSystem6ReelOptosCommand = new RelayCommand(ResetSystem6ReelOptosToDefaults);
        ResetMameRomSourceDefaultsCommand = new RelayCommand(ResetMameRomSourceDefaults);
        CloseProjectSettingsCommand = new RelayCommand(CloseProjectSettings);
        CloseProjectCommand = new RelayCommand(CloseProject, CanCloseProject);
        ExitCommand = new RelayCommand(ExitApplication);
        StartAndLoadStateEmulationCommand = new RelayCommand(StartAndLoadStateEmulation, CanStartAndLoadStateEmulation);
        StartDebuggerAndLoadStateEmulationCommand = new RelayCommand(StartDebuggerAndLoadStateEmulation, CanStartAndLoadStateEmulation);
        SaveStateAndExitEmulationCommand = new RelayCommand(SaveStateAndExitEmulation, CanSaveStateAndExitEmulation);
        StartEmulationCommand = new RelayCommand(StartEmulation, CanStartEmulation);
        StartDebuggerEmulationCommand = new RelayCommand(StartDebuggerEmulation, CanStartEmulation);
        LoadStateEmulationCommand = new RelayCommand(LoadStateEmulation, CanLoadStateEmulation);
        SaveStateEmulationCommand = new RelayCommand(SaveStateEmulation, CanSaveStateEmulation);
        StopEmulationCommand = new RelayCommand(StopEmulation, CanStopEmulation);
        TogglePauseEmulationCommand = new RelayCommand(TogglePauseEmulation, CanTogglePauseEmulation);
        ToggleUnthrottleEmulationCommand = new RelayCommand(ToggleUnthrottleEmulation, CanSetThrottleEmulation);
        SoftResetEmulationCommand = new RelayCommand(SoftResetEmulation, CanResetEmulation);
        HardResetEmulationCommand = new RelayCommand(HardResetEmulation, CanResetEmulation);
        RefreshDebuggerStatusCommand = new RelayCommand(RefreshDebuggerStatus, CanQueryDebugger);
        ListDebuggerCpusCommand = new RelayCommand(ListDebuggerCpus, CanQueryDebugger);
        DebuggerRunCommand = new RelayCommand(DebuggerRun, CanControlDebugger);
        DebuggerBreakCommand = new RelayCommand(DebuggerBreak, CanControlDebugger);
        DebuggerStepCommand = new RelayCommand(DebuggerStep, CanControlDebugger);
        ListDebuggerBreakpointsCommand = new RelayCommand(ListDebuggerBreakpoints, CanControlDebugger);
        ListDebuggerWatchpointsCommand = new RelayCommand(ListDebuggerWatchpoints, CanControlDebugger);
        AddTestDebuggerBreakpointCommand = new RelayCommand(AddTestDebuggerBreakpoint, CanControlDebugger);
        DisassembleAroundCurrentPcCommand = new RelayCommand(DisassembleAroundCurrentPc, CanControlDebugger);
        DisassembleFixedAddressTestBlockCommand = new RelayCommand(DisassembleFixedAddressTestBlock, CanControlDebugger);

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
            _mameVersion = preferences.Mame.Version;
            _mameExecutablePath = preferences.Mame.ExecutablePath;
            _mameInstallRootDirectory = MameRuntimePaths.EnsureManagedRuntimeRootDirectory();
            _mameReleaseSource = preferences.Mame.ReleaseSource;
            _mameLuaPluginPath = MameRuntimePaths.ResolveBundledLuaPluginSourcePath();
            _mameCommandLineOverrides = preferences.Mame.CommandLineOverrides;
            _system6NativeLibraryPath = preferences.NativeEmulation.System6LibraryPath;
            _system6AudioBufferLengthMilliseconds = NormalizeSystem6AudioBufferLengthMilliseconds(preferences.NativeEmulation.AudioBufferLengthMilliseconds);
            _mameRomDownloadBaseUrl = preferences.Mame.RomDownloadBaseUrl;
            _mameRomArchiveExtension = preferences.Mame.RomArchiveExtension;
            _mameLocalRomSourceDirectory = preferences.Mame.LocalRomSourceDirectory;
            _mameLocalRomArchiveExtension = preferences.Mame.LocalRomArchiveExtension;
            _mameRomDownloadService.DownloadRootUrl = _mameRomDownloadBaseUrl;
            _mameRomDownloadService.ArchiveExtension = _mameRomArchiveExtension;
            _mameRomDownloadService.LocalRomSourceDirectory = _mameLocalRomSourceDirectory;
            _mameRomDownloadService.LocalRomArchiveExtension = _mameLocalRomArchiveExtension;
            _oasisPlayerExecutablePath = preferences.Player.ExecutablePath;
            _oasisPlayerFullscreen = preferences.Player.Fullscreen;
            _oasisPlayerPreviewWidth = preferences.Player.PreviewWidth;
            _oasisPlayerPreviewHeight = preferences.Player.PreviewHeight;
            _keepMameUpToDateAutomatically = preferences.Mame.KeepMameUpToDateAutomatically;
            _debugOutputLamps = preferences.Mame.DebugOutputLamps;
            _debugOutputStdIn = preferences.Mame.DebugOutputStdIn;
            _debugOutputStdOut = preferences.Mame.DebugOutputStdOut;
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
        _mameVersionCatalogService = new MameVersionCatalogService(_mameDownloadService);
        var setupValidationService = new MameSetupValidationService(_mamePluginAssetValidator, _mameVersionCatalogService);
        _mameSetupOrchestrator = new MameSetupOrchestrator(setupValidationService);
        _lampRuntimeAdapter = new MameLampRuntimeAdapter(
            () => OpenDocuments,
            () => DebugOutputLamps,
            message => AddOutputEntry(message, OutputLogStatus.Info),
            DispatchToUiThread);
        _reelRuntimeAdapter = new MameReelRuntimeAdapter(
            () => OpenDocuments,
            () => SelectedFruitMachinePlatform,
            () => _activeEmulationBackend?.BackendKind ?? EmulationBackendKind.Mame,
            () => DebugOutputStdOut,
            message => AddOutputEntry(message, OutputLogStatus.Info),
            DispatchToUiThread);
        _segmentRuntimeAdapter = new MameSegmentRuntimeAdapter(
            () => OpenDocuments,
            DispatchToUiThread,
            () => SelectedFruitMachinePlatform);
        var mameStdoutParser = new MameStdoutParser(
            new MameLampStateParser(),
            _lampRuntimeAdapter,
            new MameReelStateParser(),
            _reelRuntimeAdapter,
            new MameSegmentStateParser(),
            _segmentRuntimeAdapter,
            platformProvider: () => SelectedFruitMachinePlatform,
            vfdDotMatrixStateParser: new MameVfdDotMatrixStateParser(),
            vfdDotMatrixRuntimeAdapter: new MameVfdDotMatrixRuntimeAdapter(
                () => OpenDocuments,
                DispatchToUiThread));
        _mameProcessRunner = new MameProcessRunner(
            stdoutLogger: line => ProcessMameStdoutLine(line, mameStdoutParser),
            stdinLogger: line =>
            {
                if (DebugOutputStdIn)
                {
                    AddOutputEntry($"[MAME-STDIN] {line}", OutputLogStatus.Info);
                }
            },
            stderrLogger: line => AddOutputEntry($"[MAME-ERR] {line}", OutputLogStatus.Warning));
        _mameDebuggerService = new MameDebuggerService(
            _mameProcessRunner,
            message => AddOutputEntry(message, OutputLogStatus.Info));
        _mameDebuggerShell = new MameDebuggerShellViewModel(_mameDebuggerService, AddOutputEntry);
        _mameDebuggerService.DebuggerEventReceived += (_, debuggerEvent) =>
        {
            DispatchToUiThread(() =>
            {
                AddOutputEntry(
                    $"MAME debugger event: {debuggerEvent.Event} state={debuggerEvent.State ?? "unknown"} cpu={debuggerEvent.Cpu ?? "unknown"} pc={debuggerEvent.Pc?.ToString() ?? "unknown"}.",
                    OutputLogStatus.Info);
                NotifyEmulationCommands();
                _mameDebuggerShell.NotifyCommandStateChanged();
            });
        };
        _mameEmulationService = new MameEmulationService(
            new MameProcessStartInfoBuilder(),
            _mameProcessRunner,
            BuildMameLaunchRequest);
        _emulationBackendFactory = new EmulationBackendFactory(
            () => new MameEmulationBackend(
                _mameEmulationService,
                new MameInputCommandService(new MameInputPortResolver()),
                _mameProcessRunner,
                () => SelectedFruitMachinePlatform,
                input => LoadedProject?.InputDefinitions.FirstOrDefault(definition => string.Equals(definition.Id, input.Id, StringComparison.OrdinalIgnoreCase))),
            () => System6NativeLibraryPath,
            () => System6AudioBufferLengthMilliseconds);

        _mameEmulationService.StateChanged += (_, state) =>
        {
            DispatchToUiThread(() =>
            {
                if (state is MameEmulationState.Stopping or MameEmulationState.Stopped or MameEmulationState.Failed)
                {
                    _ = ReleaseAllPlayViewInputsAsync($"emulation state '{state}'", CancellationToken.None);
                }

                if (state is MameEmulationState.Starting or MameEmulationState.Stopped or MameEmulationState.Failed)
                {
                    IsUnthrottleEmulationChecked = false;
                }

                if (state is MameEmulationState.Stopped or MameEmulationState.Failed)
                {
                    _mameDebuggerService.SetDebuggerLaunchActive(false);
                }

                EmulationState = state;
                AddOutputEntry($"Emulation state changed to {state}.", OutputLogStatus.Info);
            });
        };

        if (_keepMameUpToDateAutomatically)
        {
            SyncMameVersionToLatestInstalled();
        }

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
        _ = ValidateMamePreferencesAsync();
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
    public ICommand OpenDebuggerControlCommand { get; }
    public ICommand OpenDebuggerDisassemblyCommand { get; }
    public ICommand OpenDebuggerRegistersCommand { get; }
    public ICommand OpenDebuggerMemoryCommand { get; }
    public ICommand OpenDebuggerBreakpointsCommand { get; }
    public ICommand OpenDebuggerWatchpointsCommand { get; }
    public ICommand ClosePreferencesCommand { get; }
    public ICommand BrowseMameExecutableCommand { get; }
    public ICommand BrowseOasisPlayerExecutableCommand { get; }
    public ICommand ValidateMamePreferencesCommand { get; }
    public ICommand RefreshMameVersionsCommand { get; }
    public ICommand DownloadMameVersionCommand { get; }
    public ICommand OpenMameInstallRootCommand { get; }
    public ICommand ResyncMamePluginsCommand { get; }
    public ICommand RemoveCachedMameVersionCommand { get; }
    public ICommand DownloadMameRomCommand { get; }
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
    public ICommand ResetMameRomSourceDefaultsCommand { get; }
    public ICommand ApplyInspectorSummaryCommand { get; }
    public ICommand CloseProjectCommand { get; }
    public ICommand ExitCommand { get; }
    public ICommand StartAndLoadStateEmulationCommand { get; }
    public ICommand StartDebuggerAndLoadStateEmulationCommand { get; }
    public ICommand SaveStateAndExitEmulationCommand { get; }
    public ICommand StartEmulationCommand { get; }
    public ICommand StartDebuggerEmulationCommand { get; }
    public ICommand LoadStateEmulationCommand { get; }
    public ICommand SaveStateEmulationCommand { get; }
    public ICommand StopEmulationCommand { get; }
    public ICommand TogglePauseEmulationCommand { get; }
    public ICommand ToggleUnthrottleEmulationCommand { get; }
    public ICommand SoftResetEmulationCommand { get; }
    public ICommand HardResetEmulationCommand { get; }
    public ICommand RefreshDebuggerStatusCommand { get; }
    public ICommand ListDebuggerCpusCommand { get; }
    public ICommand DebuggerRunCommand { get; }
    public ICommand DebuggerBreakCommand { get; }
    public ICommand DebuggerStepCommand { get; }
    public ICommand ListDebuggerBreakpointsCommand { get; }
    public ICommand ListDebuggerWatchpointsCommand { get; }
    public ICommand AddTestDebuggerBreakpointCommand { get; }
    public ICommand DisassembleAroundCurrentPcCommand { get; }
    public ICommand DisassembleFixedAddressTestBlockCommand { get; }
    public ObservableCollection<string> RecentProjects { get; }
    public ObservableCollection<DocumentTabViewModel> OpenDocuments { get; }
    public ObservableCollection<AssetBrowserItemViewModel> AssetBrowserItems { get; }
    public ObservableCollection<OutputLogEntry> OutputEntries { get; }
    public OutputLogViewModel OutputLog => _outputLog;
    public MameDebuggerShellViewModel DebuggerShell => _mameDebuggerShell;
    public IReadOnlyList<AssetDirectoryNodeViewModel> AssetDirectoryTree => _assetBrowser.AssetDirectoryTree;


    public IReadOnlyList<ThemePreference> ThemePreferences { get; } = Enum.GetValues<ThemePreference>();
    public IReadOnlyList<string> PreferencesCategories { get; } = ["Appearance", "Player", "MAME", "Native Emulation"];
    public IReadOnlyList<string> ProjectSettingsCategories { get; } = ["General", "MAME", "Native"];
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

    public string MameVersion { get => _mameVersion; set { if (SetProperty(ref _mameVersion, value)) SavePreferences(); } }
    public string OasisPlayerExecutablePath { get => _oasisPlayerExecutablePath; set { if (SetProperty(ref _oasisPlayerExecutablePath, value)) SavePreferences(); } }
    public bool OasisPlayerFullscreen { get => _oasisPlayerFullscreen; set { if (SetProperty(ref _oasisPlayerFullscreen, value)) SavePreferences(); } }
    public int OasisPlayerPreviewWidth { get => _oasisPlayerPreviewWidth; set { if (SetProperty(ref _oasisPlayerPreviewWidth, value)) SavePreferences(); } }
    public int OasisPlayerPreviewHeight { get => _oasisPlayerPreviewHeight; set { if (SetProperty(ref _oasisPlayerPreviewHeight, value)) SavePreferences(); } }
    public string MameExecutablePath { get => _mameExecutablePath; set { if (SetProperty(ref _mameExecutablePath, value)) SavePreferences(); } }
    public string MameInstallRootDirectory => _mameInstallRootDirectory;
    public string MameReleaseSource { get => _mameReleaseSource; set { if (SetProperty(ref _mameReleaseSource, value)) SavePreferences(); } }
    public string MameLuaPluginPath => _mameLuaPluginPath;
    public string MameCommandLineOverrides { get => _mameCommandLineOverrides; set { if (SetProperty(ref _mameCommandLineOverrides, value)) SavePreferences(); } }
    public string System6NativeLibraryPath { get => _system6NativeLibraryPath; set { if (SetProperty(ref _system6NativeLibraryPath, value)) SavePreferences(); } }
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
    public string System6NativeRomStatus { get => _system6NativeRomStatus; private set => SetProperty(ref _system6NativeRomStatus, value); }
    public ObservableCollection<System6ReelOptoSettingsViewModel> System6ReelOptos { get => _system6ReelOptos; private set => SetProperty(ref _system6ReelOptos, value); }
    public ObservableCollection<System6CoinSettingsViewModel> System6Coins { get => _system6Coins; private set => SetProperty(ref _system6Coins, value); }
    public string MameValidationSummary { get => _mameValidationSummary; private set => SetProperty(ref _mameValidationSummary, value); }
    public string MameSetupPhaseDisplay => _mameSetupState.Phase.ToString();
    public string MameSetupLatestKnownVersion => _mameSetupState.LatestKnownVersion;
    public bool IsMameSetupInProgress => _mameSetupState.IsInProgress;

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
    public MameEmulationState EmulationState
    {
        get => _mameEmulationState;
        private set
        {
            if (SetProperty(ref _mameEmulationState, value))
            {
                OnPropertyChanged(nameof(IsPauseEmulationChecked));
                NotifyEmulationCommands();
            }
        }
    }

    public bool IsPauseEmulationChecked => EmulationState == MameEmulationState.Paused;

    public bool IsUnthrottleEmulationChecked
    {
        get => _isMameUnthrottled;
        private set => SetProperty(ref _isMameUnthrottled, value);
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
        if (LoadedProject is null || EmulationState is not MameEmulationState.Running and not MameEmulationState.Paused)
        {
            return false;
        }

        _playViewInputRouter ??= _activeEmulationBackend is not null
            ? new PlayViewInputRouter(_activeEmulationBackend)
            : new PlayViewInputRouter(new MameInputCommandService(new MameInputPortResolver()), _mameProcessRunner);
        return true;
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

    private static int NormalizeSystem6AudioBufferLengthMilliseconds(int value)
        => Math.Clamp(value, 10, 1000);

    private void SavePreferences()
    {
        var existingPreferences = _preferencesStore.Load();
        _preferencesStore.Save(new EditorPreferences
        {
            ThemePreference = SelectedThemePreference,
            LastMfmeFmlImportDirectory = _lastMfmeFmlImportDirectory,
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
            NativeEmulation = new NativeEmulationPreferences
            {
                System6LibraryPath = System6NativeLibraryPath,
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
            if (TryClassifyMameStdoutSegment(segment, DebugOutputStdOut, out var status))
            {
                AddOutputEntry($"[MAME-STDOUT] {segment}", status);
            }

            _mameDebuggerService.ProcessStdoutLine(segment);
            parser.ProcessLine(segment);
        }
    }

    private static bool TryClassifyMameStdoutSegment(string segment, bool debugOutputStdOut, out OutputLogStatus status)
    {
        if (debugOutputStdOut)
        {
            status = OutputLogStatus.Info;
            return true;
        }

        if (segment.StartsWith("@ERROR", StringComparison.Ordinal))
        {
            status = OutputLogStatus.Warning;
            return true;
        }

        status = OutputLogStatus.Info;
        return false;
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

    private void OpenDebuggerControl()
    {
        ToolWindowOpenRequested?.Invoke(EditorToolWindowId.DebuggerControl);
        AddOutputEntry("Opened Debugger Control pane.", OutputLogStatus.Info);
    }

    private void OpenDebuggerDisassembly()
    {
        ToolWindowOpenRequested?.Invoke(EditorToolWindowId.DebuggerDisassembly);
        AddOutputEntry("Opened Disassembly pane.", OutputLogStatus.Info);
    }

    private void OpenDebuggerRegisters()
    {
        ToolWindowOpenRequested?.Invoke(EditorToolWindowId.DebuggerRegisters);
        AddOutputEntry("Opened Registers pane.", OutputLogStatus.Info);
    }

    private void OpenDebuggerMemory()
    {
        ToolWindowOpenRequested?.Invoke(EditorToolWindowId.DebuggerMemory);
        AddOutputEntry("Opened Memory pane.", OutputLogStatus.Info);
    }

    private void OpenDebuggerBreakpoints()
    {
        ToolWindowOpenRequested?.Invoke(EditorToolWindowId.DebuggerBreakpoints);
        AddOutputEntry("Opened Breakpoints pane.", OutputLogStatus.Info);
    }

    private void OpenDebuggerWatchpoints()
    {
        ToolWindowOpenRequested?.Invoke(EditorToolWindowId.DebuggerWatchpoints);
        AddOutputEntry("Opened Watchpoints pane.", OutputLogStatus.Info);
    }

    private void ClosePreferences()
    {
        ToolWindowCloseRequested?.Invoke(EditorToolWindowId.Preferences);
    }

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

    private MameEmulationCommandState CurrentEmulationCommandState =>
        MameEmulationCommandStateEvaluator.Evaluate(HasLoadedProject, EmulationState);

    private bool CanStartAndLoadStateEmulation()
    {
        return CurrentEmulationCommandState.CanStartAndLoadState;
    }

    private async void StartAndLoadStateEmulation()
    {
        if (!CanStartAndLoadStateEmulation())
        {
            return;
        }

        AddOutputEntry("Emulation start and load state requested.", OutputLogStatus.Info);
        try
        {
            await _mameEmulationService.StartAndLoadStateAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            AddOutputEntry($"Emulation failed to start and load state: {ex.Message}", OutputLogStatus.Error);
        }
    }

    private async void StartDebuggerAndLoadStateEmulation()
    {
        if (!CanStartAndLoadStateEmulation())
        {
            return;
        }

        AddOutputEntry("Debugger emulation start and load state requested.", OutputLogStatus.Info);
        try
        {
            if (!TrySyncMamePluginsForDebuggerLaunch())
            {
                return;
            }

            await _mameEmulationService.StartDebuggerAndLoadStateAsync(CancellationToken.None);
            _mameDebuggerService.SetDebuggerLaunchActive(true);
            await TryLogDebuggerStatusAsync("Debugger launch validation");
            NotifyEmulationCommands();
        }
        catch (Exception ex)
        {
            _mameDebuggerService.SetDebuggerLaunchActive(false);
            AddOutputEntry($"Debugger emulation failed to start and load state: {ex.Message}", OutputLogStatus.Error);
        }
    }

    private bool CanSaveStateAndExitEmulation()
    {
        return CurrentEmulationCommandState.CanSaveStateAndExit;
    }

    private async void SaveStateAndExitEmulation()
    {
        if (!CanSaveStateAndExitEmulation())
        {
            return;
        }

        AddOutputEntry("Emulation save state and exit requested.", OutputLogStatus.Info);
        try
        {
            await _mameEmulationService.SaveStateAndExitAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            AddOutputEntry($"Emulation failed to save state and exit cleanly: {ex.Message}", OutputLogStatus.Error);
        }
    }

    private bool CanStartEmulation()
    {
        return CurrentEmulationCommandState.CanStart;
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
            await backend.DisposeAsync().ConfigureAwait(false);
            _activeEmulationBackend = null;
            throw;
        }
    }

    private EmulationLaunchRequest BuildEmulationLaunchRequest()
    {
        var romRootPath = MameRuntimePaths.EnsureManagedRomRootDirectory();
        var romPaths = string.IsNullOrWhiteSpace(MameRomName)
            ? Array.Empty<string>()
            : new[] { Path.Combine(romRootPath, MameRomName + MameRomArchiveExtension) };

        return new EmulationLaunchRequest(
            SelectedFruitMachinePlatform,
            MameRomName,
            romRootPath,
            romPaths,
            MameCommandLineOverrides,
            BuildSystem6NativeRomSettingsForLaunch(),
            BuildConfiguredLampIdsForLaunch(),
            BuildConfiguredSevenSegmentDisplayIdsForLaunch());
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
                if (element.Kind == PanelElementKind.SevenSegment && element.DisplayNumber is int displayId && displayId is >= 0 and <= ushort.MaxValue / NativeSystem6SevenSegmentCellStride)
                {
                    displayIds.Add(displayId);
                }
            }

            foreach (var faceDisplay in document.GetFaceElements().OfType<FaceSevenSegmentDisplayElement>())
            {
                if (faceDisplay.LinkedMachineObjectReference is MachineObjectReference reference
                    && reference.Kind == MachineObjectKind.SevenSegmentDisplay
                    && int.TryParse(reference.Id, out var displayId)
                    && displayId is >= 0 and <= ushort.MaxValue / NativeSystem6SevenSegmentCellStride)
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
        System6ReelOptos = new ObservableCollection<System6ReelOptoSettingsViewModel>((settings.ReelOptos is { Count: > 0 } ? settings.ReelOptos : System6NativeRomSettings.CreateDefaultReelOptos()).Select(reel => new System6ReelOptoSettingsViewModel(reel, SaveSystem6NativeRomSettings)));
        System6Coins = new ObservableCollection<System6CoinSettingsViewModel>(NormalizeSystem6CoinSettings(settings.Coins).Select(coin => new System6CoinSettingsViewModel(coin, SaveSystem6NativeRomSettings)));
        OnPropertyChanged(nameof(System6ProgramRom1Path)); OnPropertyChanged(nameof(System6ProgramRom2Path));
        OnPropertyChanged(nameof(System6ProgramRom3Path)); OnPropertyChanged(nameof(System6ProgramRom4Path));
        OnPropertyChanged(nameof(System6SoundRom1Path)); OnPropertyChanged(nameof(System6SoundRom2Path));
        OnPropertyChanged(nameof(System6SoundRom3Path)); OnPropertyChanged(nameof(System6SoundRom4Path));
        OnPropertyChanged(nameof(System6FlashSwitch));
        OnPropertyChanged(nameof(System6PercentSwitchValue));
    }

    private void RefreshSystem6NativeRomStatus()
    {
        System6NativeRomStatus = string.IsNullOrWhiteSpace(System6ProgramRom1Path) || string.IsNullOrWhiteSpace(System6ProgramRom2Path)
            ? "Program ROM 1 and 2 are required for native DLL launch."
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
            ReelOptos = (settings.ReelOptos is { Count: > 0 } ? settings.ReelOptos : System6NativeRomSettings.CreateDefaultReelOptos())
                .Select(reel => new System6ReelOptoSettings { ReelIndex = reel.ReelIndex, Enabled = reel.Enabled, Steps = reel.Steps, OptoStart = reel.OptoStart, OptoEnd = reel.OptoEnd, OptoInvert = reel.OptoInvert })
                .ToList(),
            Coins = NormalizeSystem6CoinSettings(settings.Coins)
                .Select(coin => new System6CoinSettings { Name = coin.Name, Enabled = coin.Enabled, Num = coin.Num, Coin = coin.Coin, CoinValue = coin.CoinValue, CoinEnable = coin.CoinEnable, LockoutValue = coin.LockoutValue, LockoutInvert = coin.LockoutInvert, CounterIn = coin.CounterIn, CounterOut = coin.CounterOut, PortIndex = coin.PortIndex, Level = coin.Level, FullLevel = coin.FullLevel })
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

    private void OnActiveBackendLampChanged(object? sender, MachineLampChangedEventArgs e)
    {
        _lampRuntimeAdapter.ApplyLampState(e.LampId, e.Value);
    }

    private void OnActiveBackendReelChanged(object? sender, MachineReelChangedEventArgs e)
    {
        _reelRuntimeAdapter.ApplyReelState(e.ReelId, e.Position);
    }

    private void OnActiveBackendSegmentChanged(object? sender, MachineSegmentChangedEventArgs e)
    {
        _segmentRuntimeAdapter.ApplySegmentState(e.CellId, e.SegmentMask, e.OutputType);
    }

    private void OnActiveBackendVfdBrightnessChanged(object? sender, MachineVfdBrightnessChangedEventArgs e)
    {
        _segmentRuntimeAdapter.ApplyVfdBrightness(e.CellId, e.NormalizedBrightness);
    }

    private void OnActiveBackendStateChanged(object? sender, EmulationBackendState state)
    {
        DispatchToUiThread(() =>
        {
            EmulationState = state switch
            {
                EmulationBackendState.Starting => MameEmulationState.Starting,
                EmulationBackendState.Running => MameEmulationState.Running,
                EmulationBackendState.Paused => MameEmulationState.Paused,
                EmulationBackendState.Stopping => MameEmulationState.Stopping,
                EmulationBackendState.Stopped => MameEmulationState.Stopped,
                _ => MameEmulationState.Failed
            };
            AddOutputEntry($"Emulation backend state changed to {state}.", OutputLogStatus.Info);
        });
    }

    private async void StartDebuggerEmulation()
    {
        if (!CanStartEmulation())
        {
            return;
        }

        AddOutputEntry("Debugger emulation start requested.", OutputLogStatus.Info);
        try
        {
            if (!TrySyncMamePluginsForDebuggerLaunch())
            {
                return;
            }

            await _mameEmulationService.StartDebuggerAsync(CancellationToken.None);
            _mameDebuggerService.SetDebuggerLaunchActive(true);
            await TryLogDebuggerStatusAsync("Debugger launch validation");
            NotifyEmulationCommands();
        }
        catch (Exception ex)
        {
            _mameDebuggerService.SetDebuggerLaunchActive(false);
            AddOutputEntry($"Debugger emulation failed to start: {ex.Message}", OutputLogStatus.Error);
        }
    }

    private bool CanLoadStateEmulation()
    {
        return CurrentEmulationCommandState.CanLoadState;
    }

    private async void LoadStateEmulation()
    {
        await SendEmulationCommandAsync(
            CanLoadStateEmulation,
            "Emulation load state requested.",
            "Emulation failed to load state",
            cancellationToken => _mameEmulationService.LoadStateAsync(cancellationToken));
    }

    private bool CanSaveStateEmulation()
    {
        return CurrentEmulationCommandState.CanSaveState;
    }

    private async void SaveStateEmulation()
    {
        await SendEmulationCommandAsync(
            CanSaveStateEmulation,
            "Emulation save state requested.",
            "Emulation failed to save state",
            cancellationToken => _mameEmulationService.SaveStateAsync(cancellationToken));
    }

    private bool CanStopEmulation()
    {
        return CurrentEmulationCommandState.CanStop;
    }

    public void StopEmulationForWindowClose()
    {
        if (_activeEmulationBackend is null && _mameEmulationService.State == MameEmulationState.Stopped)
        {
            return;
        }

        AddOutputEntry("Emulation stop requested.", OutputLogStatus.Info);
        try
        {
            if (_activeEmulationBackend is not null)
            {
                _activeEmulationBackend.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
                _activeEmulationBackend.StateChanged -= OnActiveBackendStateChanged;
                _activeEmulationBackend.LampChanged -= OnActiveBackendLampChanged;
                _activeEmulationBackend.ReelChanged -= OnActiveBackendReelChanged;
                _activeEmulationBackend.SegmentChanged -= OnActiveBackendSegmentChanged;
                _activeEmulationBackend.VfdBrightnessChanged -= OnActiveBackendVfdBrightnessChanged;
                _activeEmulationBackend.DisposeAsync().AsTask().GetAwaiter().GetResult();
                _activeEmulationBackend = null;
                _playViewInputRouter = null;
                _playViewInputDispatcher = null;
                EmulationState = MameEmulationState.Stopped;
            }
            else
            {
                _mameEmulationService.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
            }
        }
        catch (Exception ex)
        {
            AddOutputEntry($"Emulation failed to stop cleanly: {ex.Message}", OutputLogStatus.Error);
        }
    }

    private async void StopEmulation()
    {
        await StopEmulationAsync().ConfigureAwait(false);
    }

    private async Task StopEmulationAsync()
    {
        if (!CanStopEmulation())
        {
            return;
        }

        AddOutputEntry("Emulation stop requested.", OutputLogStatus.Info);
        try
        {
            if (_activeEmulationBackend is not null)
            {
                await _activeEmulationBackend.StopAsync(CancellationToken.None);
                _activeEmulationBackend.StateChanged -= OnActiveBackendStateChanged;
                _activeEmulationBackend.LampChanged -= OnActiveBackendLampChanged;
                _activeEmulationBackend.ReelChanged -= OnActiveBackendReelChanged;
                _activeEmulationBackend.SegmentChanged -= OnActiveBackendSegmentChanged;
                _activeEmulationBackend.VfdBrightnessChanged -= OnActiveBackendVfdBrightnessChanged;
                await _activeEmulationBackend.DisposeAsync();
                _activeEmulationBackend = null;
                _playViewInputRouter = null;
                _playViewInputDispatcher = null;
                EmulationState = MameEmulationState.Stopped;
            }
            else
            {
                await _mameEmulationService.StopAsync(CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            AddOutputEntry($"Emulation failed to stop cleanly: {ex.Message}", OutputLogStatus.Error);
        }
    }

    private bool CanTogglePauseEmulation()
    {
        return CurrentEmulationCommandState.CanTogglePause;
    }

    private async void TogglePauseEmulation()
    {
        if (IsPauseEmulationChecked)
        {
            var commandSucceeded = await SendEmulationCommandAsync(
                CanTogglePauseEmulation,
                "Emulation resume requested.",
                "Emulation failed to resume",
                cancellationToken => _activeEmulationBackend?.ResumeAsync(cancellationToken) ?? _mameEmulationService.ResumeAsync(cancellationToken));

            if (!commandSucceeded)
            {
                OnPropertyChanged(nameof(IsPauseEmulationChecked));
            }

            return;
        }

        var pauseSucceeded = await SendEmulationCommandAsync(
            CanTogglePauseEmulation,
            "Emulation pause requested.",
            "Emulation failed to pause",
            cancellationToken => _activeEmulationBackend?.PauseAsync(cancellationToken) ?? _mameEmulationService.PauseAsync(cancellationToken));

        if (!pauseSucceeded)
        {
            OnPropertyChanged(nameof(IsPauseEmulationChecked));
        }
    }

    private bool CanSetThrottleEmulation()
    {
        return CurrentEmulationCommandState.CanSetThrottle;
    }

    private async void ToggleUnthrottleEmulation()
    {
        var shouldUnthrottle = !IsUnthrottleEmulationChecked;
        var commandSucceeded = await SendEmulationCommandAsync(
            CanSetThrottleEmulation,
            shouldUnthrottle ? "Emulation unthrottle requested." : "Emulation throttle requested.",
            shouldUnthrottle ? "Emulation failed to disable throttle" : "Emulation failed to enable throttle",
            cancellationToken => _mameEmulationService.SetThrottleAsync(!shouldUnthrottle, cancellationToken));

        if (commandSucceeded)
        {
            IsUnthrottleEmulationChecked = shouldUnthrottle;
        }
        else
        {
            OnPropertyChanged(nameof(IsUnthrottleEmulationChecked));
        }
    }

    private bool CanResetEmulation()
    {
        return CurrentEmulationCommandState.CanReset;
    }

    private async void SoftResetEmulation()
    {
        await SendEmulationCommandAsync(
            CanResetEmulation,
            "Emulation soft reset requested.",
            "Emulation failed to soft reset",
            cancellationToken => _activeEmulationBackend?.ResetAsync(EmulationResetKind.Soft, cancellationToken) ?? _mameEmulationService.SoftResetAsync(cancellationToken));
    }

    private async void HardResetEmulation()
    {
        await SendEmulationCommandAsync(
            CanResetEmulation,
            "Emulation hard reset requested.",
            "Emulation failed to hard reset",
            cancellationToken => _activeEmulationBackend?.ResetAsync(EmulationResetKind.Hard, cancellationToken) ?? _mameEmulationService.HardResetAsync(cancellationToken));
    }

    private bool TrySyncMamePluginsForDebuggerLaunch()
    {
        try
        {
            var copied = _mamePluginDeploymentService.SyncPluginFiles(MameLuaPluginPath, MameExecutablePath);
            AddOutputEntry($"Debugger launch synced Oasis Lua plugins into active MAME install ({copied} files copied).", OutputLogStatus.Info);
            return true;
        }
        catch (Exception ex)
        {
            AddOutputEntry($"Debugger launch blocked because Oasis Lua plugins could not be synced: {ex.Message}", OutputLogStatus.Error);
            return false;
        }
    }

    private bool CanQueryDebugger()
    {
        return _mameEmulationService.State is MameEmulationState.Running or MameEmulationState.Paused;
    }

    private bool CanControlDebugger()
    {
        return CanQueryDebugger() && _mameDebuggerService.State.IsDebuggerLaunchActive;
    }

    private async void RefreshDebuggerStatus()
    {
        await SendDebuggerCommandAsync(
            CanQueryDebugger,
            "Debugger status requested.",
            "Debugger status request failed",
            async cancellationToken =>
            {
                await LogDebuggerStatusAsync("Debugger status", cancellationToken);
            });
    }

    private async void ListDebuggerCpus()
    {
        await SendDebuggerCommandAsync(
            CanQueryDebugger,
            "Debugger CPU list requested.",
            "Debugger CPU list request failed",
            async cancellationToken =>
            {
                var cpus = await _mameDebuggerService.GetCpuListAsync(cancellationToken);
                if (cpus.Count == 0)
                {
                    AddOutputEntry("Debugger CPU list returned no CPU devices.", OutputLogStatus.Warning);
                    return;
                }

                AddOutputEntry($"Debugger CPUs: {string.Join(", ", cpus.Select(cpu => cpu.IsCurrent ? $"{cpu.Tag} ({cpu.Name}, current)" : $"{cpu.Tag} ({cpu.Name})"))}.", OutputLogStatus.Info);
            });
    }

    private async void DebuggerRun()
    {
        await SendDebuggerCommandAsync(
            CanControlDebugger,
            "Debugger run requested.",
            "Debugger run failed",
            cancellationToken => _mameDebuggerService.RunAsync(cancellationToken));
    }

    private async void DebuggerBreak()
    {
        await SendDebuggerCommandAsync(
            CanControlDebugger,
            "Debugger break requested.",
            "Debugger break failed",
            cancellationToken => _mameDebuggerService.BreakAsync(cancellationToken));
    }

    private async void DebuggerStep()
    {
        await SendDebuggerCommandAsync(
            CanControlDebugger,
            "Debugger step requested.",
            "Debugger step failed",
            cancellationToken => _mameDebuggerService.StepAsync(cancellationToken));
    }

    private async void ListDebuggerBreakpoints()
    {
        await SendDebuggerCommandAsync(
            CanControlDebugger,
            "Debugger breakpoint list requested.",
            "Debugger breakpoint list failed",
            async cancellationToken =>
            {
                var breakpoints = await _mameDebuggerService.GetBreakpointsAsync(null, cancellationToken);
                AddOutputEntry(breakpoints.Count == 0
                    ? "Debugger breakpoints: none."
                    : $"Debugger breakpoints: {string.Join(", ", breakpoints.Select(bp => $"#{bp.MameId} {bp.Cpu} 0x{bp.Address:X} enabled={bp.Enabled}"))}.",
                    OutputLogStatus.Info);
            });
    }

    private async void ListDebuggerWatchpoints()
    {
        await SendDebuggerCommandAsync(
            CanControlDebugger,
            "Debugger watchpoint list requested.",
            "Debugger watchpoint list failed",
            async cancellationToken =>
            {
                var watchpoints = await _mameDebuggerService.GetWatchpointsAsync(null, cancellationToken);
                AddOutputEntry(watchpoints.Count == 0
                    ? "Debugger watchpoints: none."
                    : $"Debugger watchpoints: {string.Join(", ", watchpoints.Select(wp => $"#{wp.MameId} {wp.Cpu} 0x{wp.Address:X}-0x{wp.Address + wp.Length - 1:X} {wp.Type} enabled={wp.Enabled}"))}.",
                    OutputLogStatus.Info);
            });
    }

    private async void AddTestDebuggerBreakpoint()
    {
        await SendDebuggerCommandAsync(
            CanControlDebugger,
            "Debugger test breakpoint requested at current PC.",
            "Debugger test breakpoint failed",
            async cancellationToken =>
            {
                var status = await _mameDebuggerService.GetStatusAsync(cancellationToken);
                if (!status.Pc.HasValue)
                {
                    AddOutputEntry("Debugger test breakpoint could not be created because the current PC is unknown.", OutputLogStatus.Warning);
                    return;
                }

                var breakpoint = await _mameDebuggerService.SetBreakpointAsync(
                    new MameDebuggerBreakpointRequest(status.Cpu, status.Pc.Value),
                    cancellationToken);
                AddOutputEntry($"Debugger test breakpoint #{breakpoint.MameId} set on {breakpoint.Cpu} at 0x{breakpoint.Address:X}.", OutputLogStatus.Info);
            });
    }

    private async void DisassembleAroundCurrentPc()
    {
        await SendDebuggerCommandAsync(
            CanControlDebugger,
            "Debugger disassembly requested around current PC.",
            "Debugger disassembly around current PC failed",
            async cancellationToken =>
            {
                var status = await _mameDebuggerService.GetStatusAsync(cancellationToken);
                var block = await _mameDebuggerService.DisassembleAsync(
                    new MameDebuggerDisassemblyRequest(status.Cpu, null, 16, CenterAroundPc: true),
                    cancellationToken);
                LogDisassemblyBlock("Debugger disassembly around PC", block);
            });
    }

    private async void DisassembleFixedAddressTestBlock()
    {
        await SendDebuggerCommandAsync(
            CanControlDebugger,
            "Debugger fixed-address disassembly test requested.",
            "Debugger fixed-address disassembly test failed",
            async cancellationToken =>
            {
                var status = await _mameDebuggerService.GetStatusAsync(cancellationToken);
                var block = await _mameDebuggerService.DisassembleAsync(
                    new MameDebuggerDisassemblyRequest(status.Cpu, 0, 16, CenterAroundPc: false),
                    cancellationToken);
                LogDisassemblyBlock("Debugger disassembly from 0x0", block);
            });
    }

    private void LogDisassemblyBlock(string prefix, MameDebuggerDisassemblyBlock block)
    {
        AddOutputEntry($"{prefix}: cpu={block.Cpu} start=0x{block.StartAddress:X} lines={block.Lines.Count}/{block.LineCount} pc={(block.CurrentPc.HasValue ? $"0x{block.CurrentPc.Value:X}" : "unknown")}", OutputLogStatus.Info);
        foreach (var line in block.Lines.Take(8))
        {
            var current = line.IsCurrentPc ? "=> " : "   ";
            var instruction = string.IsNullOrWhiteSpace(line.InstructionText) ? line.RawText : line.InstructionText;
            var opcodes = string.IsNullOrWhiteSpace(line.OpcodeBytes) ? string.Empty : $" [{line.OpcodeBytes}]";
            AddOutputEntry($"{current}{line.Cpu} 0x{line.Address:X}:{opcodes} {instruction}", OutputLogStatus.Info);
        }
        if (block.Lines.Count > 8)
        {
            AddOutputEntry($"{prefix}: {block.Lines.Count - 8} additional disassembly lines omitted from temporary Output preview.", OutputLogStatus.Info);
        }
    }

    private async Task LogDebuggerStatusAsync(string prefix, CancellationToken cancellationToken = default)
    {
        var status = await _mameDebuggerService.GetStatusAsync(cancellationToken);
        var statusLevel = status.Available ? OutputLogStatus.Info : OutputLogStatus.Warning;
        AddOutputEntry($"{prefix}: available={status.Available}, state={status.State}, cpu={status.Cpu ?? "unknown"}, pc={status.Pc?.ToString() ?? "unknown"}.", statusLevel);
    }

    private async Task TryLogDebuggerStatusAsync(string prefix, CancellationToken cancellationToken = default)
    {
        try
        {
            await LogDebuggerStatusAsync(prefix, cancellationToken);
        }
        catch (Exception ex)
        {
            AddOutputEntry($"{prefix} did not receive a debugger protocol response yet: {ex.Message}", OutputLogStatus.Warning);
        }
    }


    private async Task<bool> SendDebuggerCommandAsync(
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
            NotifyEmulationCommands();
            return true;
        }
        catch (Exception ex)
        {
            AddOutputEntry($"{failureMessage}: {ex.Message}", OutputLogStatus.Error);
            NotifyEmulationCommands();
            return false;
        }
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
        ApplySystem6NativeRomSettingsToViewModel(project.System6NativeRoms);
        RefreshMameRomStatus();
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
        var mameRomName = ResolveMameRomName(projectDocument.RootElement);
        var automaticallyDownloadMissingRoms = ResolveAutomaticallyDownloadMissingRoms(projectDocument.RootElement);
        var system6NativeRoms = ResolveSystem6NativeRomSettings(projectDocument.RootElement);
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
            MameRomName = mameRomName,
            AutomaticallyDownloadMissingRoms = automaticallyDownloadMissingRoms,
            System6NativeRoms = system6NativeRoms
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
        return _documentWorkspace.ExecuteDocumentCanvasCommand(documentId, command);
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
                        WriteProjectSettings(writer, property.Value, LoadedProject.FruitMachinePlatform, LoadedProject.MameRomName, LoadedProject.AutomaticallyDownloadMissingRoms, LoadedProject.System6NativeRoms);
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
                    writer.WriteString("MameRomName", LoadedProject.MameRomName);
                    writer.WriteBoolean("AutomaticallyDownloadMissingRoms", LoadedProject.AutomaticallyDownloadMissingRoms);
                    WriteSystem6NativeRomSettings(writer, LoadedProject.System6NativeRoms);
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

    private static void WriteProjectSettings(Utf8JsonWriter writer, JsonElement existingProjectSettings, FruitMachinePlatformType platform, string mameRomName, bool automaticallyDownloadMissingRoms, System6NativeRomSettings system6NativeRoms)
    {
        writer.WriteStartObject();
        var wrotePlatform = false;
        var wroteMameRomName = false;
        var wroteAutomaticallyDownloadMissingRoms = false;
        var wroteSystem6NativeRoms = false;

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
            if (settingProperty.NameEquals("System6NativeRoms"))
            {
                WriteSystem6NativeRomSettings(writer, system6NativeRoms);
                wroteSystem6NativeRoms = true;
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
        if (!wroteSystem6NativeRoms)
        {
            WriteSystem6NativeRomSettings(writer, system6NativeRoms);
        }

        writer.WriteEndObject();
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
            writer.WriteNumber("LockoutValue", coin.LockoutValue);
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
                LockoutValue = GetOptionalInt(coinElement, "LockoutValue", System6CoinSettings.DefaultLockoutValue),
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
            writer.WriteBoolean("Inverted", input.Inverted);
            writer.WriteString("RawMfmeShortcut", input.RawMfmeShortcut);
            writer.WriteString("KeyboardShortcut", input.KeyboardShortcut);
            if (input.LinkedVisualElementId.HasValue)
            {
                writer.WriteString("LinkedVisualElementId", input.LinkedVisualElementId.Value);
            }
            writer.WriteString("MamePortTag", input.MamePortTag);
            writer.WriteString("MameMask", input.MameMask);
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
                Inverted = inputElement.TryGetProperty("Inverted", out var invertedElement) && invertedElement.ValueKind == JsonValueKind.True,
                RawMfmeShortcut = inputElement.TryGetProperty("RawMfmeShortcut", out var rawShortcutElement) ? rawShortcutElement.GetString() ?? string.Empty : string.Empty,
                KeyboardShortcut = inputElement.TryGetProperty("KeyboardShortcut", out var keyboardShortcutElement) ? keyboardShortcutElement.GetString() ?? string.Empty : string.Empty,
                LinkedVisualElementId = linkedVisualId,
                MamePortTag = inputElement.TryGetProperty("MamePortTag", out var tagElement) ? tagElement.GetString() ?? string.Empty : string.Empty,
                MameMask = inputElement.TryGetProperty("MameMask", out var maskElement) ? maskElement.GetString() ?? string.Empty : string.Empty,
                Notes = inputElement.TryGetProperty("Notes", out var notesElement) ? notesElement.GetString() ?? string.Empty : string.Empty
            });
        }

        return definitions;
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
        RaiseEmulationCommandCanExecuteChanged(StartAndLoadStateEmulationCommand);
        RaiseEmulationCommandCanExecuteChanged(StartDebuggerAndLoadStateEmulationCommand);
        RaiseEmulationCommandCanExecuteChanged(SaveStateAndExitEmulationCommand);
        RaiseEmulationCommandCanExecuteChanged(StartEmulationCommand);
        RaiseEmulationCommandCanExecuteChanged(StartDebuggerEmulationCommand);
        RaiseEmulationCommandCanExecuteChanged(LoadStateEmulationCommand);
        RaiseEmulationCommandCanExecuteChanged(SaveStateEmulationCommand);
        RaiseEmulationCommandCanExecuteChanged(StopEmulationCommand);
        RaiseEmulationCommandCanExecuteChanged(TogglePauseEmulationCommand);
        RaiseEmulationCommandCanExecuteChanged(ToggleUnthrottleEmulationCommand);
        RaiseEmulationCommandCanExecuteChanged(SoftResetEmulationCommand);
        RaiseEmulationCommandCanExecuteChanged(HardResetEmulationCommand);
        RaiseEmulationCommandCanExecuteChanged(RefreshDebuggerStatusCommand);
        RaiseEmulationCommandCanExecuteChanged(ListDebuggerCpusCommand);
        RaiseEmulationCommandCanExecuteChanged(DebuggerRunCommand);
        RaiseEmulationCommandCanExecuteChanged(DebuggerBreakCommand);
        RaiseEmulationCommandCanExecuteChanged(DebuggerStepCommand);
        RaiseEmulationCommandCanExecuteChanged(ListDebuggerBreakpointsCommand);
        RaiseEmulationCommandCanExecuteChanged(ListDebuggerWatchpointsCommand);
        RaiseEmulationCommandCanExecuteChanged(AddTestDebuggerBreakpointCommand);
        RaiseEmulationCommandCanExecuteChanged(DisassembleAroundCurrentPcCommand);
        RaiseEmulationCommandCanExecuteChanged(DisassembleFixedAddressTestBlockCommand);
        _mameDebuggerShell.NotifyCommandStateChanged();
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
    private int _lockoutValue;
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
        _lockoutValue = model.LockoutValue;
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
    public int LockoutValue { get => _lockoutValue; set => SetAndSave(ref _lockoutValue, Math.Clamp(value, 0, byte.MaxValue), nameof(LockoutValue)); }
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
        LockoutValue = LockoutValue,
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
    public int ReelNumber => ReelIndex + 1;

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
