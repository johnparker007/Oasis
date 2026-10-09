using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using OasisEditor.Commands;
using OasisEditor.Features.CabinetEditor.Models;
using OasisEditor.Features.CabinetEditor.Services;
using OasisEditor.Features.CabinetEditor.ViewModels;
using OasisEditor.Features.MachineComposition.ViewModels;
using OasisEditor.Progress;
using SkiaSharp;
using Oasis.Scripting;

namespace OasisEditor;

public sealed class DocumentTabViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly CommandService _commandService;
    private EditorDocument _document;
    private string? _panelLayoutJson;
    private string? _faceDocumentJson;
    private bool _faceDocumentJsonIsCurrent = true;
    private string? _cabinetDocumentJson;
    private CabinetDocument _cabinetDocumentModel;
    private MachineDocument _machineDocumentModel;
    private string _machineBehaviorSource = string.Empty;
    private bool _machineBehaviorSourceMissing;
    private OasisScriptMachineReferenceIndexBuildResult? _machineBehaviorReferenceIndex;
    private ReelDocument _reelDocumentModel;
    private Object3DDocument _object3DDocumentModel;
    private string? _pendingObject3DModelSourcePath;
    private Panel2DDocumentModel _panelDocumentModel;
    private FaceDocumentModel _faceDocumentModel;
    private Dictionary<string, PanelElementModel> _lampElementsByObjectId = new(StringComparer.Ordinal);
    private Dictionary<string, PanelElementModel> _reelElementsByObjectId = new(StringComparer.Ordinal);
    private Dictionary<string, PanelElementModel> _alphaElementsByObjectId = new(StringComparer.Ordinal);
    private Dictionary<string, PanelElementModel> _sevenSegmentElementsByObjectId = new(StringComparer.Ordinal);
    private Dictionary<string, PanelElementModel> _vfdDotMatrixElementsByObjectId = new(StringComparer.Ordinal);
    private HashSet<string> _visualStateObjectIds = new(StringComparer.Ordinal);
    private double _panelZoom = 1.0;
    private double _faceZoom = 1.0;
    private double _facePanX;
    private double _facePanY;
    private double _panelPanX;
    private double _panelPanY;
    private CalibrationPlacementState? _calibrationPlacement;
    private Dictionary<string, object>? _lastVisualStateByObjectId;
    private readonly MachineRuntimeState _runtimeState;
    private CabinetModelDocumentViewModel? _cabinetViewer;
    private DocumentTabViewModel? _machineCompositionContext;
    private bool _isRefreshingMachineCompositionChoices;
    private string? _machineCompositionCatalogSignature;
    private Func<IReadOnlyList<DocumentTabViewModel>>? _openDocumentsAccessor;
    private Func<EditorProject?>? _projectAccessor;
    private Func<string>? _libraryRootAccessor;
    private Action<string>? _openAssetDocument;
    private readonly FaceWorkspaceViewModel? _faceWorkspace;
    private readonly MachineCompositionGraphViewModel? _machineCompositionGraph;
    private MachineComposition3DViewModel? _machineComposition3D;
    internal Func<ICabinetModelLoader> MachineCompositionLoaderFactory { get; set; } = static () => new SharpGltfWpfModelLoader();
    private readonly MachineRuntimeSettingsViewModel? _machineRuntimeSettings;
    private readonly FaceRuntimeAssetsConfigurationService _runtimeAssetsConfiguration = new();
    private SKBitmap? _correctionInputBitmap;
    private string? _correctionInputCacheKey;
    private readonly Dictionary<string, CalibrationOperationInputCacheEntry> _calibrationOperationInputs = new(StringComparer.Ordinal);
    private IProgressDialogService _progressDialogService = NoOpProgressDialogService.Instance;
    private bool _isDetachedFaceBuildWorker;
    private CancellationToken _faceBuildCancellationToken;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action<PanelChangeEvent>? PanelChanged;
    public event Action<PanelVisualStateChangedEvent>? PanelVisualStateChanged;
    public event Action<FaceVisualStateChangedEvent>? FaceVisualStateChanged;
    public event Action<FacePreviewChangedEvent>? FacePreviewChanged;
    public event EventHandler<DocumentSelectionChangedEventArgs>? SelectionChanged;

    public DocumentTabViewModel(
        EditorDocument document,
        string? panelLayoutJson = null,
        Guid? documentId = null,
        CommandService? commandService = null,
        MachineRuntimeState? runtimeState = null,
        string? faceDocumentJson = null,
        string? cabinetDocumentJson = null,
        string? machineDocumentJson = null,
        string? reelDocumentJson = null,
        string? object3DDocumentJson = null)
    {
        _document = document;
        DocumentId = documentId ?? Guid.NewGuid();
        _commandService = commandService ?? new CommandService(new CommandHistory(), DocumentId);
        _panelLayoutJson = panelLayoutJson;
        _faceDocumentJson = faceDocumentJson;
        _cabinetDocumentJson = cabinetDocumentJson;
        _runtimeState = runtimeState ?? new MachineRuntimeState();
        _panelDocumentModel = Panel2DDocumentStorage.DeserializeModel(panelLayoutJson);
        SelectionState.SelectionChanged += OnSelectionStateChanged;
        _faceDocumentModel = FaceDocumentStorage.TryRead(faceDocumentJson, out var faceDocumentFile)
            ? FaceDocumentStorage.ToModel(faceDocumentFile)
            : new FaceDocumentModel();
        if (document.DocumentType == EditorDocumentType.Face)
        {
            _faceDocumentModel = new FaceDocumentModel
            {
                Id = _faceDocumentModel.Id,
                Title = document.Title,
                Summary = _faceDocumentModel.Summary,
                SourcePanel2DDocumentId = _faceDocumentModel.SourcePanel2DDocumentId,
                SourcePanel2DDocumentPath = _faceDocumentModel.SourcePanel2DDocumentPath,
                SourceFaceShapeId = _faceDocumentModel.SourceFaceShapeId,
                SourceRegion = _faceDocumentModel.SourceRegion,
                LastRegeneratedAtUtc = _faceDocumentModel.LastRegeneratedAtUtc,
                GenerationSettings = _faceDocumentModel.GenerationSettings,
            Provenance = _faceDocumentModel.Provenance, BuildState = _faceDocumentModel.BuildState,
                Artwork = _faceDocumentModel.Artwork,
                RuntimeRenderAssets = _faceDocumentModel.RuntimeRenderAssets,
                MaskLayer = _faceDocumentModel.MaskLayer,
                Trays = _faceDocumentModel.Trays,
                LampEmitters = _faceDocumentModel.LampEmitters,
                Layers = _faceDocumentModel.Layers,
                Elements = _faceDocumentModel.Elements
            };
        }
        _cabinetDocumentModel = CabinetDocumentStorage.TryRead(cabinetDocumentJson, out var cabinetDocument)
            ? cabinetDocument
            : CabinetDocument.Empty;
        _machineDocumentModel = MachineDocumentStorage.TryRead(machineDocumentJson, out var machineDocument, out _)
            ? machineDocument
            : MachineDocument.Create(document.Title);
        if (_machineDocumentModel.Behavior is not null && !document.IsUntitled && !string.IsNullOrWhiteSpace(document.FilePath))
        {
            var sourcePath = Path.Combine(Path.GetDirectoryName(document.FilePath)!, MachineBehaviorDefinition.CanonicalSourcePath);
            if (File.Exists(sourcePath)) _machineBehaviorSource = File.ReadAllText(sourcePath);
            else _machineBehaviorSourceMissing = true;
        }
        _reelDocumentModel = ReelDocumentStorage.TryRead(reelDocumentJson, out var reelDocument, out _)
            ? reelDocument
            : ReelDocument.Create(document.Title);
        _object3DDocumentModel = Object3DDocumentStorage.TryRead(object3DDocumentJson, out var object3DDocument, out _)
            ? object3DDocument
            : Object3DDocument.Create(document.Title);
        OpenSelectedCabinetAssetCommand = new RelayCommand(OpenSelectedCabinetAsset, () => CanOpenSelectedCabinetAsset);
        AddMachineObjectInstanceCommand = new RelayCommand(AddMachineObjectInstance);
        AddMachineAnchorCommand = new RelayCommand(AddMachineAnchor);
        AddMachineBehaviorCommand = new RelayCommand(AddMachineBehavior, () => _machineDocumentModel.Behavior is null);
        RemoveMachineBehaviorCommand = new RelayCommand(RemoveMachineBehavior, () => _machineDocumentModel.Behavior is not null);
        RefreshMachineObjectInstanceRows();
        RefreshMachineAnchorRows();
        RebuildLampCaches();
        _faceWorkspace = document.DocumentType == EditorDocumentType.Face ? new FaceWorkspaceViewModel(this) : null;
        _machineCompositionGraph = document.DocumentType == EditorDocumentType.Machine ? new MachineCompositionGraphViewModel(this) : null;
        _machineRuntimeSettings = document.DocumentType == EditorDocumentType.Machine ? new MachineRuntimeSettingsViewModel(this) : null;
        ValidateMachineBehaviorSource();
    }

    public EditorDocument Document => _document;
    public Guid DocumentId { get; }
    public CommandService CommandService => _commandService;
    public MachineRuntimeState RuntimeState => _runtimeState;
    public DocumentSelectionState SelectionState { get; } = new();
    public FaceWorkspaceViewModel? FaceWorkspace => _faceWorkspace;
    public MachineCompositionGraphViewModel? MachineCompositionGraph => _machineCompositionGraph;
    public MachineComposition3DViewModel? ExistingMachineComposition3D => _machineComposition3D;
    public MachineComposition3DViewModel? MachineComposition3D => Document.DocumentType == EditorDocumentType.Machine ? GetOrCreateMachineComposition3D() : null;
    public MachineRuntimeSettingsViewModel? MachineRuntimeSettings => _machineRuntimeSettings;
    internal IProgressDialogService ProgressDialogService => _progressDialogService;
    internal string? ProjectDirectory => _projectAccessor?.Invoke()?.ProjectDirectory;
    internal EditorProject? CurrentProject => _projectAccessor?.Invoke();
    internal string CurrentLibraryRoot => LibraryRoot();
    public string Title => Document.IsDirty ? $"{Document.Title}*" : Document.Title;
    public string TypeLabel => Document.DocumentType switch
    {
        EditorDocumentType.ProjectOverview => "Project",
        EditorDocumentType.Panel2D => "Panel 2D",
        EditorDocumentType.Cabinet3D => "Cabinet 3D",
        EditorDocumentType.Machine => "Machine",
        EditorDocumentType.Face => "Face",
        EditorDocumentType.Reel => "Reel",
        EditorDocumentType.Object3D => "Object3D",
        _ => "Document Type"
    };
    public string FilePath => Document.FilePath;
    public string ContentSummary => Document.ContentSummary;
    public bool IsDirty => Document.IsDirty;
    public CalibrationPlacementState? CalibrationPlacement
    {
        get => _calibrationPlacement;
        set { if (_calibrationPlacement == value) return; _calibrationPlacement = value; PropertyChanged?.Invoke(this, new(nameof(CalibrationPlacement))); }
    }
    public void BeginCalibrationPlacement(CalibrationPlacementState placement)
    {
        FaceWorkspace?.NavigateTo(FaceWorkspaceDestination.ArtworkCalibration);
        CalibrationPlacement = placement;
    }

    public void CancelCalibrationPlacement() => CalibrationPlacement = null;
    public bool HasCabinetViewer => Document.DocumentType == EditorDocumentType.Cabinet3D && !string.IsNullOrWhiteSpace(_cabinetDocumentModel.Model.Path);
    public CabinetModelDocumentViewModel? ExistingCabinetViewer => _cabinetViewer;
    public CabinetModelDocumentViewModel? CabinetViewer => HasCabinetViewer ? GetOrCreateCabinetViewer() : null;

    private CabinetModelDocumentViewModel GetOrCreateCabinetViewer()
    {
        if (_cabinetViewer is not null) return _cabinetViewer;
        var viewer = new CabinetModelDocumentViewModel(new SharpGltfWpfModelLoader(), this, _openDocumentsAccessor, _projectAccessor, () => LibraryRoot());
        _cabinetViewer = viewer;
        viewer.SetMachineCompositionContext(_machineCompositionContext);
        viewer.Initialize();
        return viewer;
    }

    private MachineComposition3DViewModel GetOrCreateMachineComposition3D()
    {
        if (_machineComposition3D is not null) return _machineComposition3D;
        var composition = new MachineComposition3DViewModel(this, MachineCompositionLoaderFactory());
        _machineComposition3D = composition;
        composition.Refresh(GetMachineDocument(), cabinetChanged: false, objectInstancesChanged: true);
        composition.RefreshDefinitions();
        return composition;
    }

    public void SetOpenDocumentsAccessor(Func<IReadOnlyList<DocumentTabViewModel>> openDocumentsAccessor)
    {
        _openDocumentsAccessor = openDocumentsAccessor;
        ReconcileRuntimeAssetsConfiguration();
        RefreshMachineCompositionGraph();
        _machineComposition3D?.RefreshDefinitions();
    }

    public void SetProjectAccessor(Func<EditorProject?> projectAccessor)
    {
        _projectAccessor = projectAccessor;
        InvalidateMachineBehaviorReferenceIndex();
        ReconcileRuntimeAssetsConfiguration();
        _cabinetViewer?.ReflectionEditor.RefreshProjectContext();
        RefreshMachineCompositionChoices();
        RefreshMachineCompositionGraph();
        _machineComposition3D?.RefreshDefinitions();
    }
    public void SetLibraryRootAccessor(Func<string> libraryRootAccessor)
    {
        _libraryRootAccessor = libraryRootAccessor;
        InvalidateMachineBehaviorReferenceIndex();
        RefreshMachineCompositionChoices();
        _cabinetViewer?.RefreshFacePreviews();
        RefreshMachineCompositionGraph();
        _machineComposition3D?.RefreshDefinitions();
    }

    public void SetAssetDocumentOpener(Action<string> openAssetDocument)
    {
        _openAssetDocument = openAssetDocument;
        NotifyMachineAssetNavigationChanged();
    }

    internal void SetMachineCompositionContext(DocumentTabViewModel? machineDocument)
    {
        _machineCompositionContext = machineDocument;
        _cabinetViewer?.SetMachineCompositionContext(machineDocument);
    }

    internal void SetProgressDialogService(IProgressDialogService progressDialogService)
    {
        _progressDialogService = progressDialogService ?? throw new ArgumentNullException(nameof(progressDialogService));
    }

    public void MarkDirty()
    {
        if (_document.IsDirty)
        {
            return;
        }

        _document = _document.MarkDirty();
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Document)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsDirty)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Title)));
    }

    internal void ApplySavedDocumentState(string savePath)
    {
        _document = _document.SaveAs(savePath, _document.ContentSummary).MarkClean();
        NotifyDocumentMetadataChanged();
    }

    internal void ApplyContentSummary(string summary)
    {
        _document = _document.WithContentSummary(summary).MarkDirty();
        NotifyDocumentMetadataChanged();
    }

    private void NotifyDocumentMetadataChanged()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Document)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FilePath)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ContentSummary)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsDirty)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Title)));
    }


    public string? CabinetDocumentJson
    {
        get => _cabinetDocumentJson;
        set
        {
            if (string.Equals(_cabinetDocumentJson, value, StringComparison.Ordinal))
            {
                return;
            }

            _cabinetDocumentJson = value;
            _cabinetDocumentModel = CabinetDocumentStorage.TryRead(value, out var cabinetDocument)
                ? cabinetDocument
                : CabinetDocument.Empty;
            DisposeCabinetViewer();
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CabinetDocumentJson)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasCabinetViewer)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CabinetViewer)));
        }
    }

    public CabinetDocument GetCabinetDocument()
    {
        return _cabinetDocumentModel;
    }

    public MachineDocument GetMachineDocument() => _machineDocumentModel;
    public const string DefaultMachineBehaviorSource = "on machine.started()\n{\n}\n";
    public bool HasMachineBehavior => _machineDocumentModel.Behavior is not null;
    public string MachineBehaviorSource
    {
        get => _machineBehaviorSource;
        set
        {
            value ??= string.Empty;
            if (value == _machineBehaviorSource) return;
            _machineBehaviorSource = value;
            _machineBehaviorSourceMissing = false;
            MarkDirty();
            PropertyChanged?.Invoke(this, new(nameof(MachineBehaviorSource)));
            _machineBehaviorReferenceIndex = null;
            ValidateMachineBehaviorSource();
            PropertyChanged?.Invoke(this, new(nameof(MachineTriggerInventory)));
        }
    }
    public ObservableCollection<OasisScriptEditorDiagnostic> MachineBehaviorDiagnostics { get; } = [];
    public System.Windows.Input.ICommand AddMachineBehaviorCommand { get; }
    public System.Windows.Input.ICommand RemoveMachineBehaviorCommand { get; }
    private void AddMachineBehavior()
    {
        if (_machineDocumentModel.Behavior is not null) return;
        if (string.IsNullOrEmpty(_machineBehaviorSource)) _machineBehaviorSource = DefaultMachineBehaviorSource;
        _machineBehaviorSourceMissing = false;
        ExecuteMachineMutation(machine => machine with { Runtime = new OasisRuntimeDefinition(), Behavior = MachineBehaviorDefinition.OasisScript() }, "Add Oasis Script behaviour");
    }
    private void RemoveMachineBehavior() => MachineRuntimeKind = EmulationRuntimeDefinition.RuntimeKind;
    private void ValidateMachineBehaviorSource()
    {
        MachineBehaviorDiagnostics.Clear();
        if (_machineDocumentModel.Behavior is null) return;
        if (_machineBehaviorSourceMissing)
        {
            MachineBehaviorDiagnostics.Add(new("Error", "OSM3100", $"{MachineBehaviorDefinition.CanonicalSourcePath} is missing from the Machine package.", 1, 1, 0));
            return;
        }
        var compilation = OasisScriptCompiler.Compile(_machineBehaviorSource, MachineBehaviorDefinition.CanonicalSourcePath);
        foreach (var diagnostic in compilation.Diagnostics)
            MachineBehaviorDiagnostics.Add(new(diagnostic.Severity.ToString(), diagnostic.Code, diagnostic.Message, diagnostic.Line, diagnostic.Column, diagnostic.Span.Start));
        if (!compilation.Success) return;
        var index = GetMachineBehaviorReferenceIndex();
        foreach (var diagnostic in index.Diagnostics.Concat(OasisScriptMachineValidator.Validate(compilation.Program!, index.References)))
            MachineBehaviorDiagnostics.Add(new(diagnostic.Severity.ToString(), diagnostic.Code, diagnostic.Message, diagnostic.Line, diagnostic.Column, diagnostic.Span.Start));
    }
    internal bool IsMachineBehaviorSourceMissing => _machineBehaviorSourceMissing;
    internal OasisScriptMachineReferenceIndexBuildResult GetMachineBehaviorReferenceIndex() =>
        _machineBehaviorReferenceIndex ??= new OasisScriptMachineReferenceIndexBuilder().Build(_projectAccessor?.Invoke(), LibraryRoot(), _machineDocumentModel);
    internal void RefreshMachineReferencesForCabinet(string manifestPath)
    {
        if (_projectAccessor?.Invoke() is { } project && _machineDocumentModel.CabinetAsset is { } reference
            && CabinetModelDocumentViewModel.IsSelectedCabinet(project, LibraryRoot(), reference, manifestPath))
            RefreshMachineTriggerInventory();
    }
    public void RefreshMachineTriggerInventory() => InvalidateMachineBehaviorReferenceIndex();
    public System.Windows.Input.ICommand RefreshMachineTriggersCommand => new RelayCommand(RefreshMachineTriggerInventory);
    public CabinetTriggerInventory MachineTriggerInventory => GetMachineBehaviorReferenceIndex().TriggerInventory
        ?? CabinetTriggerInventory.Unavailable("No Cabinet reference selected.");
    private void InvalidateMachineBehaviorReferenceIndex()
    {
        _machineBehaviorReferenceIndex = null;
        ValidateMachineBehaviorSource();
        PropertyChanged?.Invoke(this, new(nameof(MachineTriggerInventory)));
    }
    public ReelDocument GetReelDocument() => _reelDocumentModel;
    public Object3DDocument GetObject3DDocument() => _object3DDocumentModel;
    public string ReelDisplayName { get => _reelDocumentModel.DisplayName; set => SetReelValue(_reelDocumentModel with { DisplayName = value?.Trim() ?? string.Empty }, "Rename Reel"); }
    public double ReelDiameterMm { get => _reelDocumentModel.DiameterMm; set => SetReelValue(_reelDocumentModel with { DiameterMm = value }, "Set Reel diameter"); }
    public double ReelWidthMm { get => _reelDocumentModel.WidthMm; set => SetReelValue(_reelDocumentModel with { WidthMm = value }, "Set Reel width"); }
    private void SetReelValue(ReelDocument next, string description)
    {
        if (next == _reelDocumentModel || string.IsNullOrWhiteSpace(next.DisplayName) || next.DiameterMm <= 0 || next.WidthMm <= 0 || !PanelElementValidation.IsFinite(next.DiameterMm) || !PanelElementValidation.IsFinite(next.WidthMm)) return;
        _commandService.Execute(new SetReelDocumentCommand(this, next, description));
    }
    public string Object3DDisplayName { get => _object3DDocumentModel.DisplayName; set => SetObject3DValue(_object3DDocumentModel with { DisplayName = value?.Trim() ?? string.Empty }, "Rename Object3D"); }
    public string Object3DModelPath { get => _object3DDocumentModel.Model.Path; set => SetObject3DValue(_object3DDocumentModel with { Model = _object3DDocumentModel.Model with { Path = value?.Trim() ?? string.Empty } }, "Set Object3D model path"); }
    public double Object3DModelScale { get => _object3DDocumentModel.Model.Scale; set => SetObject3DValue(_object3DDocumentModel with { Model = _object3DDocumentModel.Model with { Scale = value } }, "Set Object3D model scale"); }
    public string Object3DUpAxis { get => _object3DDocumentModel.Model.UpAxis; set => SetObject3DValue(_object3DDocumentModel with { Model = _object3DDocumentModel.Model with { UpAxis = value } }, "Set Object3D up-axis"); }
    public IReadOnlyList<string> Object3DUpAxes { get; } = ["X", "Y", "Z"];
    public IReadOnlyList<Object3DColliderKind> Object3DColliderKinds { get; } = Enum.GetValues<Object3DColliderKind>();
    public IReadOnlyList<Object3DCapsuleAxis> Object3DCapsuleAxes { get; } = Enum.GetValues<Object3DCapsuleAxis>();
    public Object3DColliderKind Object3DColliderKind { get => _object3DDocumentModel.Physics.Collider.Kind; set { var c = value switch { Object3DColliderKind.Sphere => new Object3DColliderDefinition(value, [0,0,0], Radius: .5), Object3DColliderKind.Box => new Object3DColliderDefinition(value, [0,0,0], Size: [1,1,1]), Object3DColliderKind.Capsule => new Object3DColliderDefinition(value, [0,0,0], Radius: .5, Height: 2, Axis: Object3DCapsuleAxis.Y), _ => new Object3DColliderDefinition(value) }; SetObject3DValue(_object3DDocumentModel with { Physics = _object3DDocumentModel.Physics with { Collider = c } }, "Set Object3D collider kind"); } }
    public double Object3DColliderCenterX { get => ColliderVector(_object3DDocumentModel.Physics.Collider.Center, 0); set => SetColliderCenter(0, value); }
    public double Object3DColliderCenterY { get => ColliderVector(_object3DDocumentModel.Physics.Collider.Center, 1); set => SetColliderCenter(1, value); }
    public double Object3DColliderCenterZ { get => ColliderVector(_object3DDocumentModel.Physics.Collider.Center, 2); set => SetColliderCenter(2, value); }
    public double Object3DColliderRadius { get => _object3DDocumentModel.Physics.Collider.Radius ?? .5; set => SetCollider(_object3DDocumentModel.Physics.Collider with { Radius = value }, "Set collider radius"); }
    public double Object3DColliderSizeX { get => ColliderVector(_object3DDocumentModel.Physics.Collider.Size, 0, 1); set => SetColliderSize(0, value); }
    public double Object3DColliderSizeY { get => ColliderVector(_object3DDocumentModel.Physics.Collider.Size, 1, 1); set => SetColliderSize(1, value); }
    public double Object3DColliderSizeZ { get => ColliderVector(_object3DDocumentModel.Physics.Collider.Size, 2, 1); set => SetColliderSize(2, value); }
    public double Object3DColliderHeight { get => _object3DDocumentModel.Physics.Collider.Height ?? 2; set => SetCollider(_object3DDocumentModel.Physics.Collider with { Height = value }, "Set collider height"); }
    public Object3DCapsuleAxis Object3DColliderAxis { get => _object3DDocumentModel.Physics.Collider.Axis ?? Object3DCapsuleAxis.Y; set => SetCollider(_object3DDocumentModel.Physics.Collider with { Axis = value }, "Set collider axis"); }
    public bool Object3DRigidbodyEnabled { get => _object3DDocumentModel.Physics.Rigidbody.Enabled; set { var body = _object3DDocumentModel.Physics.Rigidbody with { Enabled = value, Mass = value ? _object3DDocumentModel.Physics.Rigidbody.Mass ?? 1 : null, UseGravity = value ? _object3DDocumentModel.Physics.Rigidbody.UseGravity ?? true : null }; SetObject3DValue(_object3DDocumentModel with { Physics = _object3DDocumentModel.Physics with { Rigidbody = body } }, "Toggle Object3D Rigidbody"); } }
    public double Object3DRigidbodyMass { get => _object3DDocumentModel.Physics.Rigidbody.Mass ?? 1; set { var body = _object3DDocumentModel.Physics.Rigidbody with { Mass = value }; SetObject3DValue(_object3DDocumentModel with { Physics = _object3DDocumentModel.Physics with { Rigidbody = body } }, "Set Rigidbody mass"); } }
    public bool Object3DUseGravity { get => _object3DDocumentModel.Physics.Rigidbody.UseGravity ?? true; set { var body = _object3DDocumentModel.Physics.Rigidbody with { UseGravity = value }; SetObject3DValue(_object3DDocumentModel with { Physics = _object3DDocumentModel.Physics with { Rigidbody = body } }, "Set Rigidbody gravity"); } }
    private static double ColliderVector(double[]? values, int index, double fallback = 0) => values is { Length: 3 } ? values[index] : fallback;
    private void SetColliderCenter(int index, double value) { var v = (_object3DDocumentModel.Physics.Collider.Center ?? [0,0,0]).ToArray(); v[index] = value; SetCollider(_object3DDocumentModel.Physics.Collider with { Center = v }, "Set collider center"); }
    private void SetColliderSize(int index, double value) { var v = (_object3DDocumentModel.Physics.Collider.Size ?? [1,1,1]).ToArray(); v[index] = value; SetCollider(_object3DDocumentModel.Physics.Collider with { Size = v }, "Set collider size"); }
    private void SetCollider(Object3DColliderDefinition collider, string description) => SetObject3DValue(_object3DDocumentModel with { Physics = _object3DDocumentModel.Physics with { Collider = collider } }, description);
    private void SetObject3DValue(Object3DDocument next, string description) { if (next == _object3DDocumentModel) return; try { Object3DValidationService.Validate(next); } catch (InvalidOperationException) { return; } _commandService.Execute(new SetObject3DDocumentCommand(this, next, description)); }
    internal string? PendingObject3DModelSourcePath => _pendingObject3DModelSourcePath;
    internal void SetObject3DModelSource(string sourcePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        var packageRelativePath = Path.GetFileName(sourcePath);
        var next = _object3DDocumentModel with { Model = _object3DDocumentModel.Model with { Path = packageRelativePath } };
        Object3DValidationService.Validate(next);
        _commandService.Execute(new SetObject3DModelSourceCommand(this, next, Path.GetFullPath(sourcePath)));
    }
    internal void ApplySavedObject3DDocument(Object3DDocument document)
    {
        _object3DDocumentModel = document;
        _pendingObject3DModelSourcePath = null;
        PropertyChanged?.Invoke(this, new(nameof(Object3DModelPath)));
    }
    public string GetMachineDocumentJson() => MachineDocumentStorage.Serialize(_machineDocumentModel);
    public string MachineDisplayName { get => _machineDocumentModel.DisplayName; set { if (string.IsNullOrWhiteSpace(value) || value == _machineDocumentModel.DisplayName) return; ExecuteMachineMutation(_machineDocumentModel with { DisplayName = value.Trim() }, "Rename Machine"); } }
    public object? MachineCabinetAssetPath { get => _machineDocumentModel.CabinetAsset; set { var reference = value switch { null => null, AssetReference typed => typed, string path when !string.IsNullOrWhiteSpace(path) => AssetReference.Project(path), _ => null }; if (_isRefreshingMachineCompositionChoices || reference == _machineDocumentModel.CabinetAsset) return; ExecuteMachineMutation(_machineDocumentModel with { CabinetAsset = reference, SurfaceAssignments = [], ReelAssignments = [] }, "Select Machine Cabinet"); } }
    public System.Windows.Input.ICommand OpenSelectedCabinetAssetCommand { get; }
    public System.Windows.Input.ICommand AddMachineObjectInstanceCommand { get; }
    public System.Windows.Input.ICommand AddMachineAnchorCommand { get; }
    public bool CanOpenSelectedCabinetAsset => CanOpenMachineAsset(_machineDocumentModel.CabinetAsset);

    private bool CanOpenMachineAsset(AssetReference? reference)
    {
        if (reference is null || _openAssetDocument is null || _projectAccessor?.Invoke() is not { } project) return false;
        return TryResolve(project, reference, out var path) && File.Exists(path);
    }

    private void OpenSelectedCabinetAsset() => OpenMachineAsset(_machineDocumentModel.CabinetAsset);

    internal void OpenMachineAsset(AssetReference? reference)
    {
        if (reference is null || _openAssetDocument is null || _projectAccessor?.Invoke() is not { } project) return;
        if (!TryResolve(project, reference, out var path) || !File.Exists(path))
        {
            NotifyMachineAssetNavigationChanged();
            return;
        }
        _openAssetDocument(path);
    }

    internal void OpenMachineGraphAsset(string path)
    {
        if (_openAssetDocument is not null && File.Exists(path)) _openAssetDocument(path);
    }

    internal bool CanOpenMachineAssetReference(AssetReference? reference) => CanOpenMachineAsset(reference);

    private void NotifyMachineAssetNavigationChanged()
    {
        PropertyChanged?.Invoke(this, new(nameof(CanOpenSelectedCabinetAsset)));
        if (OpenSelectedCabinetAssetCommand is RelayCommand command) command.RaiseCanExecuteChanged();
        foreach (var row in MachineReelAssignmentRows) row.NotifyAssetNavigationChanged();
        foreach (var row in MachineObjectInstanceRows) row.NotifyAssetNavigationChanged();
    }
    private MachineAssetChoice? _selectedMachineCabinetChoice;
    public MachineAssetChoice? SelectedMachineCabinetChoice
    {
        get => _selectedMachineCabinetChoice;
        set
        {
            if (ReferenceEquals(_selectedMachineCabinetChoice, value)) return;
            _selectedMachineCabinetChoice = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedMachineCabinetChoice)));
            MachineCabinetAssetPath = value?.AssetPath;
        }
    }
    public FruitMachinePlatformType MachinePlatform
    {
        get => (_machineDocumentModel.Runtime as EmulationRuntimeDefinition)?.Platform ?? FruitMachinePlatformType.None;
        set
        {
            if (!IsMachineEmulationRuntime || value == MachinePlatform) return;
            ExecuteMachineMutation(_machineDocumentModel with { Runtime = EmulationRuntimeDefinition.Create(value) }, "Change Machine runtime platform");
        }
    }
    public bool IsMachineEmulationRuntime => _machineDocumentModel.Runtime is EmulationRuntimeDefinition;
    public IReadOnlyList<string> MachineRuntimeKinds { get; } = ["Emulation", "Oasis"];
    public Func<bool> ConfirmRemoveMachineBehavior { get; set; } = () => System.Windows.MessageBox.Show(
        "Switch to Emulation and remove Oasis Script behaviour? Saving will delete behavior.oasis. You can undo this change before closing the Machine.",
        "Remove Oasis Script behaviour", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning) == System.Windows.MessageBoxResult.Yes;
    public string MachineRuntimeKind
    {
        get => _machineDocumentModel.Runtime.Kind;
        set
        {
            if (string.IsNullOrEmpty(value) || value == MachineRuntimeKind) return;
            if (value == OasisRuntimeDefinition.RuntimeKind) AddMachineBehavior();
            else if (value == EmulationRuntimeDefinition.RuntimeKind)
            {
                if (HasMachineBehavior && !ConfirmRemoveMachineBehavior())
                { PropertyChanged?.Invoke(this, new(nameof(MachineRuntimeKind))); return; }
                ExecuteMachineMutation(machine => machine with { Runtime = EmulationRuntimeDefinition.Create(FruitMachinePlatformType.None), Behavior = null }, "Switch Machine to Emulation and remove behaviour");
            }
            else throw new ArgumentException($"Unsupported Machine runtime '{value}'.");
        }
    }
    public IReadOnlyList<FruitMachinePlatformType> MachinePlatforms => EmulationRuntimePlatforms.Supported;
    public IReadOnlyList<MachineSurfaceAssignment> MachineSurfaceAssignments => _machineDocumentModel.SurfaceAssignments;
    public IReadOnlyList<MachineReelAssignment> MachineReelAssignments => _machineDocumentModel.ReelAssignments;
    public IReadOnlyList<MachineObject3DInstance> MachineObjectInstances => _machineDocumentModel.ObjectInstances;
    public IReadOnlyList<MachineAnchor> MachineAnchors => _machineDocumentModel.Anchors;
    public IReadOnlyList<InputDefinitionModel> MachineInputs => _machineDocumentModel.InputDefinitions;
    public ObservableCollection<MachineAssetChoice> MachineCabinetChoices { get; } = [];
    public ObservableCollection<MachineAssetChoice> MachineFaceChoices { get; } = [];
    public ObservableCollection<MachineSurfaceAssignmentRow> MachineSurfaceAssignmentRows { get; } = [];
    public ObservableCollection<MachineReelAssignmentRow> MachineReelAssignmentRows { get; } = [];
    public ObservableCollection<MachineObjectInstanceRow> MachineObjectInstanceRows { get; } = [];
    public ObservableCollection<MachineAnchorRow> MachineAnchorRows { get; } = [];

    internal void SetMachineDocument(MachineDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var cabinetChanged = _machineDocumentModel.CabinetAsset != document.CabinetAsset;
        var surfaceAssignmentsChanged = !_machineDocumentModel.SurfaceAssignments.SequenceEqual(document.SurfaceAssignments);
        var reelAssignmentsChanged = !_machineDocumentModel.ReelAssignments.SequenceEqual(document.ReelAssignments);
        var objectInstancesChanged = !_machineDocumentModel.ObjectInstances.SequenceEqual(document.ObjectInstances);
        var anchorsChanged = !_machineDocumentModel.Anchors.SequenceEqual(document.Anchors);
        _machineDocumentModel = document;
        _machineBehaviorReferenceIndex = null;
        ValidateMachineBehaviorSource();
        _machineRuntimeSettings?.Refresh();
        MarkDirty();
        foreach (var property in new[] { "MachineDocument", nameof(MachineDisplayName), nameof(MachineCabinetAssetPath), nameof(MachineTriggerInventory), nameof(MachineRuntimeKind), nameof(IsMachineEmulationRuntime), nameof(MachinePlatform), nameof(MachineSurfaceAssignments), nameof(MachineReelAssignments), nameof(MachineObjectInstances), nameof(MachineAnchors), nameof(MachineInputs), nameof(HasMachineBehavior), nameof(MachineBehaviorSource) })
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
        if (AddMachineBehaviorCommand is RelayCommand addBehavior) addBehavior.RaiseCanExecuteChanged();
        if (RemoveMachineBehaviorCommand is RelayCommand removeBehavior) removeBehavior.RaiseCanExecuteChanged();
        if (cabinetChanged)
        {
            _machineCompositionCatalogSignature = null;
            RefreshMachineCabinetDependentRows(forceRebuild: true);
        }
        else
        {
            SynchronizeMachineAssignmentRows();
            if ((surfaceAssignmentsChanged || reelAssignmentsChanged) && _projectAccessor?.Invoke() is { } project)
                RefreshMachineReelRowsFromAssignedFaces(project);
        }
        if (objectInstancesChanged) RefreshMachineObjectInstanceRows();
        if (anchorsChanged) RefreshMachineAnchorRows();
        _machineComposition3D?.Refresh(document, cabinetChanged, objectInstancesChanged || anchorsChanged);
        NotifyMachineAssetNavigationChanged();
        RefreshMachineCompositionGraph();
    }

    internal void RefreshMachineCompositionChoices()
    {
        if (Document.DocumentType != EditorDocumentType.Machine || _projectAccessor?.Invoke() is not { } project) return;
        InvalidateMachineBehaviorReferenceIndex();
        var cabinetChoices = DiscoverAssetChoices(project, EditorAssetType.Cabinet3D);
        var faceChoices = DiscoverProjectAssetChoices(project, EditorAssetType.Face);
        var reelChoices = DiscoverAssetChoices(project, EditorAssetType.Reel);
        var objectChoices = DiscoverAssetChoices(project, EditorAssetType.Object3D);
        var signature = BuildMachineCompositionCatalogSignature(project, cabinetChoices, faceChoices.Concat(reelChoices).Concat(objectChoices).ToArray());
        if (string.Equals(signature, _machineCompositionCatalogSignature, StringComparison.Ordinal)) { NotifyMachineAssetNavigationChanged(); RefreshMachineCompositionGraph(); return; }
        _isRefreshingMachineCompositionChoices = true;
        try
        {
            RefreshMachineCompositionChoicesCore(project, cabinetChoices, faceChoices);
            RefreshMachineObjectInstanceRows(objectChoices);
            _machineCompositionCatalogSignature = signature;
        }
        finally { _isRefreshingMachineCompositionChoices = false; NotifyMachineAssetNavigationChanged(); RefreshMachineCompositionGraph(); }
    }

    internal void RefreshMachineCompositionGraph() => _machineCompositionGraph?.Refresh(
        _projectAccessor?.Invoke(), LibraryRoot(), _openDocumentsAccessor?.Invoke());

    private void RefreshMachineCompositionChoicesCore(EditorProject project, IReadOnlyList<MachineAssetChoice> cabinetAssets, IReadOnlyList<MachineAssetChoice> faceAssets)
    {
        var selectedCabinetPath = _machineDocumentModel.CabinetAsset;
        var cabinetChoices = new List<MachineAssetChoice> { new("(None)", null) };
        cabinetChoices.AddRange(cabinetAssets);
        if (selectedCabinetPath is not null && cabinetChoices.All(choice => !Equals(choice.AssetPath, selectedCabinetPath)))
            cabinetChoices.Add(new MachineAssetChoice($"Missing: {selectedCabinetPath.Path} [{selectedCabinetPath.Scope}]", selectedCabinetPath));
        ReconcileMachineAssetChoices(MachineCabinetChoices, cabinetChoices);
        SynchronizeSelectedMachineCabinetChoice();
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MachineCabinetAssetPath)));

        var faceChoices = new List<MachineAssetChoice> { new("(None)", null) };
        faceChoices.AddRange(faceAssets);
        ReconcileMachineAssetChoices(MachineFaceChoices, faceChoices);

        RefreshMachineCabinetDependentRows(project, forceRebuild: false);
    }

    private void RefreshMachineCabinetDependentRows(bool forceRebuild)
    {
        if (_projectAccessor?.Invoke() is { } project)
            RefreshMachineCabinetDependentRows(project, forceRebuild);
        else
        {
            MachineSurfaceAssignmentRows.Clear();
            MachineReelAssignmentRows.Clear();
        }
    }

    private void RefreshMachineCabinetDependentRows(EditorProject project, bool forceRebuild)
    {
        if (_machineDocumentModel.CabinetAsset is null)
        {
            MachineSurfaceAssignmentRows.Clear();
            MachineReelAssignmentRows.Clear();
            return;
        }
        if (!TryResolve(project, _machineDocumentModel.CabinetAsset, out var cabinetPath))
        {
            MachineSurfaceAssignmentRows.Clear();
            RefreshMachineReelRowsFromAssignedFaces(project);
            return;
        }
        if (!File.Exists(cabinetPath) || !CabinetDocumentStorage.TryRead(File.ReadAllText(cabinetPath), out var cabinet))
        {
            MachineSurfaceAssignmentRows.Clear();
            RefreshMachineReelRowsFromAssignedFaces(project);
            return;
        }
        var faceChoices = MachineFaceChoices.ToList();
        var targets = CabinetFaceTargetDiscovery.Discover(cabinetPath, cabinet, new GlbCabinetFaceTargetDetector()).Targets
            .Select(target => (target.Id, target.DisplayName)).ToList();
        foreach (var assignment in _machineDocumentModel.SurfaceAssignments.Where(assignment => targets.All(target => !string.Equals(target.Id, assignment.TargetId, StringComparison.Ordinal))))
            targets.Add((assignment.TargetId, $"Missing target: {assignment.TargetId}"));
        var existingTargets = MachineSurfaceAssignmentRows.ToDictionary(row => row.TargetId, StringComparer.Ordinal);
        var rebuildSurfaceRows = forceRebuild || !MachineSurfaceAssignmentRows.Select(row => row.TargetId).SequenceEqual(targets.Select(target => target.Id), StringComparer.Ordinal);
        if (rebuildSurfaceRows)
            MachineSurfaceAssignmentRows.Clear();
        foreach (var target in targets)
        {
            var assignedPath = _machineDocumentModel.SurfaceAssignments.FirstOrDefault(item => item.TargetId == target.Id)?.FaceAssetPath;
            var rowChoices = faceChoices.ToList();
            if (!string.IsNullOrWhiteSpace(assignedPath) && rowChoices.All(choice => !string.Equals(choice.AssetPath as string, assignedPath, StringComparison.OrdinalIgnoreCase))) rowChoices.Add(new MachineAssetChoice($"Missing: {Path.GetFileName(Path.GetDirectoryName(assignedPath))}", assignedPath));
            if (rebuildSurfaceRows || !existingTargets.TryGetValue(target.Id, out var row))
            {
                MachineSurfaceAssignmentRows.Add(new MachineSurfaceAssignmentRow(this, target.Id, target.DisplayName, rowChoices, assignedPath));
                continue;
            }
            row.RefreshChoices(rowChoices);
            row.SynchronizeSelectedAssetPath(assignedPath, forceNotification: true);
        }
        RefreshMachineReelRowsFromAssignedFaces(project);
    }

    private void RefreshMachineReelRowsFromAssignedFaces(EditorProject project)
    {
        var reelChoices = new List<MachineAssetChoice> { new("(None)", null) };
        reelChoices.AddRange(DiscoverAssetChoices(project, EditorAssetType.Reel));
        var references = DiscoverRequiredMachineReelReferences(project, _machineDocumentModel.SurfaceAssignments);
        for (var index = 0; index < references.Length; index++)
        {
            var reference = references[index];
            var existingIndex = -1;
            for (var candidate = index; candidate < MachineReelAssignmentRows.Count; candidate++)
            {
                if (MachineReelAssignmentRows[candidate].Reference == reference) { existingIndex = candidate; break; }
            }
            var assignedPath = _machineDocumentModel.ReelAssignments.FirstOrDefault(item => item.MachineReelReference == reference)?.ReelAsset;
            var rowChoices = reelChoices.ToList();
            if (assignedPath is not null && rowChoices.All(choice => !Equals(choice.AssetPath, assignedPath)))
                rowChoices.Add(new MachineAssetChoice($"Missing: {assignedPath.Path} [{assignedPath.Scope}]", assignedPath));
            if (existingIndex < 0)
                MachineReelAssignmentRows.Insert(index, new MachineReelAssignmentRow(this, reference, rowChoices, assignedPath));
            else if (existingIndex != index)
                MachineReelAssignmentRows.Move(existingIndex, index);
            var row = MachineReelAssignmentRows[index];
            row.RefreshChoices(rowChoices);
            row.SynchronizeSelectedReelAssetPath(assignedPath, forceNotification: true);
        }
        while (MachineReelAssignmentRows.Count > references.Length)
            MachineReelAssignmentRows.RemoveAt(MachineReelAssignmentRows.Count - 1);
    }

    private string BuildMachineCompositionCatalogSignature(EditorProject project, IReadOnlyList<MachineAssetChoice> cabinets, IReadOnlyList<MachineAssetChoice> faces)
    {
        var parts = cabinets.Concat(faces).Select(choice => $"{choice.AssetPath}|{choice.DisplayName}").ToList();
        parts.Add($"selected:{_machineDocumentModel.CabinetAsset}");
        if (_machineDocumentModel.CabinetAsset is not null)
        {
            if (!TryResolve(project, _machineDocumentModel.CabinetAsset, out var cabinetPath))
            { parts.Add($"unresolved:{_machineDocumentModel.CabinetAsset}"); return string.Join("\n", parts); }
            AddFileStamp(parts, cabinetPath);
            if (File.Exists(cabinetPath) && CabinetDocumentStorage.TryRead(File.ReadAllText(cabinetPath), out var cabinet))
            {
                var modelPath = Path.IsPathFullyQualified(cabinet.Model.Path) ? cabinet.Model.Path : Path.GetFullPath(Path.Combine(Path.GetDirectoryName(cabinetPath)!, cabinet.Model.Path));
                AddFileStamp(parts, modelPath);
            }
        }
        foreach (var assignment in _machineDocumentModel.SurfaceAssignments.OrderBy(item => item.TargetId, StringComparer.Ordinal))
        {
            var facePath = new ProjectAssetPathService().ResolveProjectRelativePath(project, assignment.FaceAssetPath);
            AddFileStamp(parts, facePath);
        }
        return string.Join("\n", parts);
    }

    private static void AddFileStamp(List<string> parts, string path)
    {
        if (!File.Exists(path)) { parts.Add($"missing:{path}"); return; }
        var info = new FileInfo(path);
        parts.Add($"file:{path}|{info.Length}|{info.LastWriteTimeUtc.Ticks}");
    }

    internal static void ReconcileMachineAssetChoices(ObservableCollection<MachineAssetChoice> current, IReadOnlyList<MachineAssetChoice> desired)
    {
        for (var index = 0; index < desired.Count; index++)
        {
            var desiredChoice = desired[index];
            var existingIndex = -1;
            for (var candidate = index; candidate < current.Count; candidate++)
                if (Equals(current[candidate].AssetPath, desiredChoice.AssetPath)) { existingIndex = candidate; break; }
            if (existingIndex < 0) current.Insert(index, desiredChoice);
            else
            {
                if (existingIndex != index) current.Move(existingIndex, index);
                if (!string.Equals(current[index].DisplayName, desiredChoice.DisplayName, StringComparison.Ordinal)) current[index] = desiredChoice;
            }
        }
        while (current.Count > desired.Count) current.RemoveAt(current.Count - 1);
    }

    private void SynchronizeSelectedMachineCabinetChoice()
    {
        var selected = MachineCabinetChoices.FirstOrDefault(choice => Equals(choice.AssetPath, _machineDocumentModel.CabinetAsset));
        if (ReferenceEquals(_selectedMachineCabinetChoice, selected)) return;
        _selectedMachineCabinetChoice = selected;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedMachineCabinetChoice)));
    }

    private void SynchronizeMachineAssignmentRows()
    {
        foreach (var row in MachineSurfaceAssignmentRows)
            row.SynchronizeSelectedAssetPath(_machineDocumentModel.SurfaceAssignments.FirstOrDefault(item => item.TargetId == row.TargetId)?.FaceAssetPath);
        foreach (var row in MachineReelAssignmentRows)
            row.SynchronizeSelectedReelAssetPath(_machineDocumentModel.ReelAssignments.FirstOrDefault(item => item.MachineReelReference == row.Reference)?.ReelAsset);
    }

    private void RefreshMachineObjectInstanceRows(IReadOnlyList<MachineAssetChoice>? discoveredChoices = null)
    {
        var choices = new List<MachineAssetChoice> { new("(None)", null) };
        if (discoveredChoices is not null) choices.AddRange(discoveredChoices);
        else if (_projectAccessor?.Invoke() is { } project) choices.AddRange(DiscoverAssetChoices(project, EditorAssetType.Object3D));
        foreach (var instance in _machineDocumentModel.ObjectInstances)
            if (instance.ObjectAsset is not null && choices.All(choice => !Equals(choice.AssetPath, instance.ObjectAsset)))
                choices.Add(new MachineAssetChoice($"Missing: {instance.ObjectAsset.Path} [{instance.ObjectAsset.Scope}]", instance.ObjectAsset));

        MachineObjectInstanceRows.Clear();
        foreach (var instance in _machineDocumentModel.ObjectInstances)
            MachineObjectInstanceRows.Add(new MachineObjectInstanceRow(this, instance, choices));
    }

    private void RefreshMachineAnchorRows()
    {
        MachineAnchorRows.Clear();
        foreach (var anchor in _machineDocumentModel.Anchors) MachineAnchorRows.Add(new MachineAnchorRow(this, anchor));
    }

    private static IEnumerable<string> EnumerateManifests(string root, string manifest) => Directory.Exists(root) ? Directory.EnumerateFiles(root, manifest, SearchOption.AllDirectories).OrderBy(path => path, StringComparer.OrdinalIgnoreCase) : [];
    private static IEnumerable<MachineObjectReference> DiscoverFaceReelReferences(EditorProject project, string faceAssetPath)
    {
        var path = new ProjectAssetPathService().ResolveProjectRelativePath(project, faceAssetPath);
        if (!File.Exists(path) || !FaceDocumentStorage.TryReadValidated(File.ReadAllText(path), out var file, out _)) return [];
        return FaceDocumentStorage.ToModel(file).Elements.OfType<FaceReelMount>()
            .Select(element => element.LinkedMachineObjectReference)
            .Where(reference => reference is { Kind: MachineObjectKind.Reel })
            .Select(reference => reference!.Value).ToArray();
    }
    private static MachineObjectReference[] DiscoverRequiredMachineReelReferences(EditorProject project, IReadOnlyList<MachineSurfaceAssignment> assignments) =>
        assignments.SelectMany(assignment => DiscoverFaceReelReferences(project, assignment.FaceAssetPath))
            .Distinct()
            .OrderBy(reference => reference.Id)
            .ToArray();
    internal static IReadOnlyList<MachineAssetChoice> DiscoverProjectAssetChoices(EditorProject project, EditorAssetType type)
    {
        var pathService = new ProjectAssetPathService();
        var manifest = type switch { EditorAssetType.Face => ProjectAssetPathService.FaceManifestFileName, EditorAssetType.Cabinet3D => ProjectAssetPathService.Cabinet3DManifestFileName, EditorAssetType.Reel => ProjectAssetPathService.ReelManifestFileName, EditorAssetType.Object3D => ProjectAssetPathService.Object3DManifestFileName, _ => throw new ArgumentOutOfRangeException(nameof(type)) };
        return EnumerateManifests(pathService.GetAssetTypeDirectory(project, type), manifest)
            .Select(path => new MachineAssetChoice(type switch { EditorAssetType.Reel when ReelDocumentStorage.TryRead(File.ReadAllText(path), out var reel, out _) => reel.DisplayName, EditorAssetType.Object3D when Object3DDocumentStorage.TryRead(File.ReadAllText(path), out var object3D, out _) => object3D.DisplayName, _ => ProjectAssetPathService.GetPackageAssetNameFromManifestPath(path, type) ?? Path.GetFileName(Path.GetDirectoryName(path)) }, type == EditorAssetType.Face ? pathService.ToProjectRelativePath(project, path) : AssetReference.Project(pathService.ToProjectRelativePath(project, path))))
            .GroupBy(choice => choice.AssetPath?.ToString(), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(choice => choice.DisplayName, StringComparer.OrdinalIgnoreCase).ThenBy(choice => choice.AssetPath?.ToString(), StringComparer.OrdinalIgnoreCase).ToArray();
    }
    private IReadOnlyList<MachineAssetChoice> DiscoverAssetChoices(EditorProject project, EditorAssetType type)
    {
        var result = DiscoverProjectAssetChoices(project, type).ToList();
        result.AddRange(new OasisAssetLibraryCatalog().Discover(LibraryRoot()).Where(entry => entry.AssetType == type).Select(entry => new MachineAssetChoice(entry.DisplayName + " [Library]", entry.Reference)));
        return result;
    }
    private string LibraryRoot() => _libraryRootAccessor?.Invoke() ?? new EditorPreferencesStore().Load().AssetLibrary.RootPath;
    private string Resolve(EditorProject project, AssetReference reference) => new AssetReferenceResolver().Resolve(project, LibraryRoot(), reference);
    private bool TryResolve(EditorProject project, AssetReference reference, out string path)
    {
        try { path = Resolve(project, reference); return true; }
        catch (InvalidOperationException) { path = string.Empty; return false; }
    }
    private static bool SameAssetPath(AssetReference? left, AssetReference? right) => left == right;
    internal void SetMachineSurfaceAssignment(string targetId, string? facePath) => ExecuteMachineMutation(machine =>
    {
        var surfaceAssignments = machine.SurfaceAssignments.Where(item => item.TargetId != targetId)
            .Concat(string.IsNullOrWhiteSpace(facePath) ? [] : [new MachineSurfaceAssignment(targetId, facePath)])
            .ToArray();
        if (_projectAccessor?.Invoke() is not { } project)
            return machine with { SurfaceAssignments = surfaceAssignments };
        var requiredReferences = DiscoverRequiredMachineReelReferences(project, surfaceAssignments).ToHashSet();
        return machine with
        {
            SurfaceAssignments = surfaceAssignments,
            ReelAssignments = machine.ReelAssignments.Where(assignment => requiredReferences.Contains(assignment.MachineReelReference)).ToArray()
        };
    }, "Assign Machine Face");
    internal void SetMachineReelAssignment(MachineObjectReference reference, AssetReference? reelAsset) => ExecuteMachineMutation(machine => machine with { ReelAssignments = machine.ReelAssignments.Where(item => item.MachineReelReference != reference).Concat(reelAsset is null ? [] : [new MachineReelAssignment(reference, reelAsset)]).ToArray() }, "Assign Machine Reel asset");
    private void AddMachineObjectInstance()
    {
        var number = 1;
        var ids = _machineDocumentModel.ObjectInstances.Select(instance => instance.Id).ToHashSet(StringComparer.Ordinal);
        while (ids.Contains($"object{number}")) number++;
        var instance = new MachineObject3DInstance($"object{number}", "Object", null, MachineObjectTransform.Identity);
        ExecuteMachineMutation(machine => machine with { ObjectInstances = [.. machine.ObjectInstances, instance] }, "Add Machine Object3D instance");
    }
    internal void RemoveMachineObjectInstance(string id) => ExecuteMachineMutation(machine => machine with { ObjectInstances = machine.ObjectInstances.Where(instance => !string.Equals(instance.Id, id, StringComparison.Ordinal)).ToArray() }, "Remove Machine Object3D instance");
    internal bool UpdateMachineObjectInstance(string originalId, MachineObject3DInstance replacement, string description)
    {
        if (!MachineObject3DInstanceId.IsValid(replacement.Id) || _machineDocumentModel.ObjectInstances.Any(instance => !string.Equals(instance.Id, originalId, StringComparison.Ordinal) && string.Equals(instance.Id, replacement.Id, StringComparison.Ordinal))) return false;
        ExecuteMachineMutation(machine => machine with { ObjectInstances = machine.ObjectInstances.Select(instance => string.Equals(instance.Id, originalId, StringComparison.Ordinal) ? replacement : instance).ToArray() }, description);
        return true;
    }
    private void AddMachineAnchor()
    {
        var number = 1;
        var ids = _machineDocumentModel.Anchors.Select(anchor => anchor.Id).ToHashSet(StringComparer.Ordinal);
        while (ids.Contains($"anchor{number}")) number++;
        var anchor = MachineAnchor.Create($"anchor{number}");
        ExecuteMachineMutation(machine => machine with { Anchors = [.. machine.Anchors, anchor] }, "Add Machine anchor");
    }
    internal void RemoveMachineAnchor(string id) => ExecuteMachineMutation(machine => machine with { Anchors = machine.Anchors.Where(anchor => !string.Equals(anchor.Id, id, StringComparison.Ordinal)).ToArray() }, "Remove Machine anchor");
    internal bool UpdateMachineAnchor(string originalId, MachineAnchor replacement, string description)
    {
        if (!MachineCompositionId.IsValid(replacement.Id) || _machineDocumentModel.Anchors.Any(anchor => anchor.Id != originalId && anchor.Id == replacement.Id)) return false;
        ExecuteMachineMutation(machine => machine with { Anchors = machine.Anchors.Select(anchor => anchor.Id == originalId ? replacement : anchor).ToArray() }, description);
        return true;
    }
    internal bool IsRefreshingMachineCompositionChoices => _isRefreshingMachineCompositionChoices;

    private void ExecuteMachineMutation(MachineDocument next, string description) => _commandService.Execute(new SetMachineDocumentCommand(this, next, description));
    internal void ExecuteMachineMutation(Func<MachineDocument, MachineDocument> mutation, string description)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        ExecuteMachineMutation(mutation(_machineDocumentModel), description);
    }

    private sealed class SetMachineDocumentCommand : Commands.IDocumentCommand, Commands.IExecutionTrackedCommand
    {
        private readonly DocumentTabViewModel _owner; private readonly MachineDocument _next; private readonly string _description; private MachineDocument? _previous;
        public SetMachineDocumentCommand(DocumentTabViewModel owner, MachineDocument next, string description) { _owner = owner; _next = next; _description = description; }
        public Guid DocumentId => _owner.DocumentId; public string Description => _description; public bool WasExecuted { get; private set; }
        public void Execute() { _previous ??= _owner._machineDocumentModel; if (_owner._machineDocumentModel == _next) return; _owner.SetMachineDocument(_next); WasExecuted = true; }
        public void Undo() { if (_previous is not null) _owner.SetMachineDocument(_previous); }
    }

    private sealed class SetReelDocumentCommand : Commands.IDocumentCommand, Commands.IExecutionTrackedCommand
    {
        private readonly DocumentTabViewModel _owner; private readonly ReelDocument _next; private readonly string _description; private ReelDocument? _previous;
        public SetReelDocumentCommand(DocumentTabViewModel owner, ReelDocument next, string description) { _owner = owner; _next = next; _description = description; }
        public Guid DocumentId => _owner.DocumentId; public string Description => _description; public bool WasExecuted { get; private set; }
        public void Execute() { _previous ??= _owner._reelDocumentModel; if (_owner._reelDocumentModel == _next) return; _owner._reelDocumentModel = _next; _owner.MarkDirty(); Notify(); WasExecuted = true; }
        public void Undo() { if (_previous is null) return; _owner._reelDocumentModel = _previous; _owner.MarkDirty(); Notify(); }
        private void Notify() { foreach (var name in new[] { nameof(ReelDisplayName), nameof(ReelDiameterMm), nameof(ReelWidthMm) }) _owner.PropertyChanged?.Invoke(_owner, new(name)); }
    }

    private sealed class SetObject3DDocumentCommand : Commands.IDocumentCommand, Commands.IExecutionTrackedCommand
    {
        private readonly DocumentTabViewModel _owner; private readonly Object3DDocument _next; private readonly string _description; private Object3DDocument? _previous;
        public SetObject3DDocumentCommand(DocumentTabViewModel owner, Object3DDocument next, string description) { _owner = owner; _next = next; _description = description; }
        public Guid DocumentId => _owner.DocumentId; public string Description => _description; public bool WasExecuted { get; private set; }
        public void Execute() { _previous ??= _owner._object3DDocumentModel; if (_owner._object3DDocumentModel == _next) return; Set(_next); WasExecuted = true; }
        public void Undo() { if (_previous is not null) Set(_previous); }
        private void Set(Object3DDocument value) { _owner._object3DDocumentModel = value; _owner.MarkDirty(); foreach (var property in Object3DPropertyNames) _owner.PropertyChanged?.Invoke(_owner, new(property)); }
        private static readonly string[] Object3DPropertyNames = [nameof(Object3DDisplayName), nameof(Object3DModelPath), nameof(Object3DModelScale), nameof(Object3DUpAxis), nameof(Object3DColliderKind), nameof(Object3DColliderCenterX), nameof(Object3DColliderCenterY), nameof(Object3DColliderCenterZ), nameof(Object3DColliderRadius), nameof(Object3DColliderSizeX), nameof(Object3DColliderSizeY), nameof(Object3DColliderSizeZ), nameof(Object3DColliderHeight), nameof(Object3DColliderAxis), nameof(Object3DRigidbodyEnabled), nameof(Object3DRigidbodyMass), nameof(Object3DUseGravity)];
    }

    private sealed class SetObject3DModelSourceCommand : Commands.IDocumentCommand, Commands.IExecutionTrackedCommand
    {
        private readonly DocumentTabViewModel _owner;
        private readonly Object3DDocument _nextDocument;
        private readonly string _nextSourcePath;
        private Object3DDocument? _previousDocument;
        private string? _previousSourcePath;
        public SetObject3DModelSourceCommand(DocumentTabViewModel owner, Object3DDocument nextDocument, string nextSourcePath)
        { _owner = owner; _nextDocument = nextDocument; _nextSourcePath = nextSourcePath; }
        public Guid DocumentId => _owner.DocumentId;
        public string Description => "Choose Object3D model";
        public bool WasExecuted { get; private set; }
        public void Execute()
        {
            _previousDocument ??= _owner._object3DDocumentModel;
            _previousSourcePath ??= _owner._pendingObject3DModelSourcePath;
            if (_owner._object3DDocumentModel == _nextDocument && string.Equals(_owner._pendingObject3DModelSourcePath, _nextSourcePath, StringComparison.OrdinalIgnoreCase)) return;
            Apply(_nextDocument, _nextSourcePath);
            WasExecuted = true;
        }
        public void Undo() { if (_previousDocument is not null) Apply(_previousDocument, _previousSourcePath); }
        private void Apply(Object3DDocument document, string? sourcePath)
        {
            _owner._object3DDocumentModel = document;
            _owner._pendingObject3DModelSourcePath = sourcePath;
            _owner.MarkDirty();
            _owner.PropertyChanged?.Invoke(_owner, new(nameof(Object3DModelPath)));
        }
    }

    public string GetCabinetDocumentJson()
    {
        return CabinetDocumentStorage.Serialize(_cabinetDocumentModel);
    }

    internal void SetCabinetDocument(CabinetDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        _cabinetDocumentModel = document;
        _cabinetDocumentJson = CabinetDocumentStorage.IsSafePackageRelativePath(document.Model.Path)
            ? GetCabinetDocumentJson()
            : null;
        _cabinetViewer?.RefreshFromDocument(document);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CabinetDocumentJson)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasCabinetViewer)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CabinetViewer)));
    }

    internal void DisposeCabinetViewer()
    {
        _cabinetViewer?.Dispose();
        _cabinetViewer = null;
    }

    public void Dispose()
    {
        CancelCalibrationPlacement();
        InvalidateCorrectionInputCache();
        DisposeCabinetViewer();
        _machineComposition3D?.Dispose();
        SelectionState.SelectionChanged -= OnSelectionStateChanged;
    }

    public string? FaceDocumentJson
    {
        get => Document.DocumentType == EditorDocumentType.Face ? GetFaceDocumentJson() : _faceDocumentJson;
        set
        {
            if (_faceDocumentJsonIsCurrent && string.Equals(_faceDocumentJson, value, StringComparison.Ordinal))
            {
                return;
            }

            _faceDocumentJson = value;
            _faceDocumentJsonIsCurrent = true;
            InvalidateCorrectionInputCache();
            _faceDocumentModel = FaceDocumentStorage.TryRead(value, out var faceDocumentFile)
                ? FaceDocumentStorage.ToModel(faceDocumentFile)
                : new FaceDocumentModel();
            _faceWorkspace?.RefreshSummaries();
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FaceDocumentJson)));
            FacePreviewChanged?.Invoke(new FacePreviewChangedEvent(DocumentId));
        }
    }

    public string? PanelLayoutJson
    {
        get => _panelLayoutJson;
        set
        {
            if (string.Equals(_panelLayoutJson, value, StringComparison.Ordinal))
            {
                return;
            }

            _panelLayoutJson = value;
            _panelDocumentModel = Panel2DDocumentStorage.DeserializeModel(value);
            RebuildLampCaches();
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PanelLayoutJson)));
        }
    }

    public FaceDocumentModel GetFaceDocument()
    {
        return _faceDocumentModel;
    }

    internal string? GetArtworkSourceAbsolutePath()
    {
        var path=_faceDocumentModel.Artwork?.Source.AssetPath; var project=_projectAccessor?.Invoke();
        return string.IsNullOrWhiteSpace(path) || project is null ? null : ResolveGeneratedPath(path, project.ProjectDirectory);
    }

    internal string? GetArtworkAssetAbsolutePath(string? path)
    { var project=_projectAccessor?.Invoke(); return string.IsNullOrWhiteSpace(path)||project is null?null:FaceArtworkGeneratedPathService.Resolve(path,project.ProjectDirectory); }

    public bool ImportArtworkImage(string externalPath, out string? error)
    {
        error = null; var project = _projectAccessor?.Invoke(); var current = _faceDocumentModel.Artwork;
        if (project is null) { error = "No project is open."; return false; }
        try
        {
            var imported = FaceArtworkImageImportService.Import(externalPath, project, _faceDocumentModel.Title);
            var initialized = FaceArtworkImageImportService.CreateArtwork(imported, _faceDocumentModel.Title);
            var artwork = current is null ? initialized : new FaceArtworkModel
            {
                Id=current.Id, Source=initialized.Source, Geometry=initialized.Geometry,
                ProcessingPipeline=current.ProcessingPipeline, CorrectionInputAssetPath=initialized.CorrectionInputAssetPath,
                BaseAssetPath=initialized.BaseAssetPath, OutputAssetPath=initialized.OutputAssetPath,
                OutputWidth=initialized.OutputWidth, OutputHeight=initialized.OutputHeight, Override=current.Override,
                FinalOutputWidth=current.FinalOutputWidth, FinalOutputHeight=current.FinalOutputHeight
            };
            CommandService.Execute(FaceMutationCommands.CreateSetArtworkRecipeCommand(DocumentId, this, artwork,
                new FaceSubsystemProvenanceModel { Origin=FaceSubsystemOrigin.Authored }, "Change artwork source to image"));
            return true;
        }
        catch (Exception exception) { error=exception.Message; return false; }
    }

    public bool ImportLampMaskImage(string externalPath, out string? error)
    {
        error=null;var project=_projectAccessor?.Invoke();if(project is null){error="No project is open.";return false;}
        try{var imported=FaceLampMaskImageImportService.Import(externalPath,project,_faceDocumentModel.Title);var current=_faceDocumentModel.MaskLayer;
            var generatedPath=current?.AssetPath ?? $"Generated/Faces/{new ProjectAssetPathService().SanitizePathSegment(_faceDocumentModel.Title)}/Illumination/lamp-mask.png";
            var mask=new FaceMaskLayerModel{Id=current?.Id??"face-mask-layer",Name="Face Lamp Mask",AssetPath=generatedPath,SourceKind=FaceLampMaskSourceKind.AuthoredImage,AuthoredAssetPath=imported.AssetPath,Width=imported.Width,Height=imported.Height,SourcePanel2DDocumentId=current?.SourcePanel2DDocumentId,SourceRegion=current?.SourceRegion,Contributions=current?.Contributions??[]};
            CommandService.Execute(FaceMutationCommands.CreateSetLampMaskCommand(DocumentId,this,mask,"Use authored lamp-mask image"));return true;
        }catch(Exception exception){error=exception.Message;return false;}
    }

    public bool CanUsePanel2DArtworkSource(out string? unavailableReason)
    {
        unavailableReason = null;
        if (_faceDocumentModel.Artwork?.Source.Kind != FaceArtworkSourceKind.Image)
        {
            unavailableReason = "Panel2D artwork is already active.";
            return false;
        }
        if (!CanAttemptPanel2DArtworkSource(out var error))
        {
            unavailableReason = error;
            return false;
        }
        return true;
    }

    private bool CanAttemptPanel2DArtworkSource(out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(_faceDocumentModel.SourceFaceShapeId))
        {
            error = "The Face has no retained Face Source Shape linkage.";
            return false;
        }
        var sourcePath = _faceDocumentModel.SourcePanel2DDocumentPath?.Trim();
        var sourceId = _faceDocumentModel.SourcePanel2DDocumentId?.Trim();
        var open = (_openDocumentsAccessor?.Invoke() ?? []).Any(document =>
            document.Document.DocumentType == EditorDocumentType.Panel2D
            && ((!string.IsNullOrWhiteSpace(sourcePath) && PathsEqual(document.FilePath, sourcePath))
                || (!string.IsNullOrWhiteSpace(sourceId)
                    && (string.Equals(document.DocumentId.ToString("N"), sourceId, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(document.DocumentId.ToString("D"), sourceId, StringComparison.OrdinalIgnoreCase)
                        || PathsEqual(document.FilePath, sourceId)))));
        if (open) return true;
        var fullPath = ResolveGeneratedPath(sourcePath, _projectAccessor?.Invoke()?.ProjectDirectory);
        if (!string.IsNullOrWhiteSpace(fullPath) && File.Exists(fullPath)) return true;
        error = $"The source Panel2D '{sourcePath ?? sourceId}' is unavailable. Open it or restore the linked file, then retry.";
        return false;
    }

    internal bool CanAttemptSourcePanel() => CanAttemptPanel2DArtworkSource(out _);

    public bool UsePanel2DArtworkSource(out string? error)
    {
        error = null;
        var current = _faceDocumentModel.Artwork;
        if (current?.Source.Kind != FaceArtworkSourceKind.Image)
        {
            error = "Image artwork is not active.";
            return false;
        }
        if (!TryResolvePanel2DArtworkSource(out var panel, out var shape, out error)) return false;

        var background = panel.Elements.First(element => element.Kind == PanelElementKind.Background
            && !string.IsNullOrWhiteSpace(element.AssetPath));
        var size = FaceSourceShapeTransformService.EstimateOutputSize(shape);
        var artwork = new FaceArtworkModel
        {
            Id = current.Id,
            Source = new FaceArtworkSourceModel
            {
                Kind = FaceArtworkSourceKind.Panel2DFaceSourceShape,
                AssetPath = background.AssetPath,
                Panel2DDocumentId = _faceDocumentModel.SourcePanel2DDocumentId,
                Panel2DDocumentPath = _faceDocumentModel.SourcePanel2DDocumentPath,
                FaceSourceShapeId = _faceDocumentModel.SourceFaceShapeId
            },
            Geometry = new FaceArtworkGeometryModel(),
            ProcessingPipeline = current.ProcessingPipeline,
            CorrectionInputAssetPath = current.CorrectionInputAssetPath,
            BaseAssetPath = current.BaseAssetPath,
            OutputAssetPath = current.OutputAssetPath,
            OutputWidth = size.Width,
            OutputHeight = size.Height, Override=current.Override,
            FinalOutputWidth=current.FinalOutputWidth, FinalOutputHeight=current.FinalOutputHeight
        };
        CommandService.Execute(FaceMutationCommands.CreateSetArtworkRecipeCommand(DocumentId, this, artwork,
            FaceBuildStateFactory.CreateDerivedProvenance(_faceDocumentModel.SourcePanel2DDocumentPath).Artwork,
            "Use Panel2D artwork source"));
        return true;
    }

    private bool TryResolvePanel2DArtworkSource(out Panel2DDocumentModel panel,
        out PanelFaceSourceShapeModel shape, out string error)
    {
        shape = new PanelFaceSourceShapeModel();
        if (!TryResolveSourcePanel(out panel, out error)) return false;
        shape = panel.FaceSourceShapes.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, _faceDocumentModel.SourceFaceShapeId, StringComparison.Ordinal))!;
        if (shape is null)
        {
            error = $"Face Source Shape '{_faceDocumentModel.SourceFaceShapeId}' is unavailable in the linked Panel2D.";
            return false;
        }
        var background = panel.Elements.FirstOrDefault(element => element.Kind == PanelElementKind.Background
            && !string.IsNullOrWhiteSpace(element.AssetPath));
        if (background is null)
        {
            error = "The linked Panel2D has no background artwork.";
            return false;
        }
        var project = _projectAccessor?.Invoke();
        var backgroundPath = ResolveGeneratedPath(background.AssetPath, project?.ProjectDirectory);
        if (string.IsNullOrWhiteSpace(backgroundPath) || !File.Exists(backgroundPath))
        {
            error = $"The linked Panel2D background artwork '{background.AssetPath}' is unavailable.";
            return false;
        }
        return true;
    }

    public bool SetArtworkRegistration(FacePerspectiveRegistrationModel registration, string description="Edit artwork registration")
    {
        var current=_faceDocumentModel.Artwork; if (current?.Source.Kind != FaceArtworkSourceKind.Image) return false;
        var normalized=registration.Normalize(); if (!normalized.IsValid()) return false;
        var size=FaceSourceShapeTransformService.EstimateRegisteredImageOutputSize(current.Source.PixelWidth, current.Source.PixelHeight, normalized);
        var artwork=new FaceArtworkModel { Id=current.Id, Source=current.Source,
            Geometry=new FaceArtworkGeometryModel { PerspectiveRegistration=normalized }, ProcessingPipeline=current.ProcessingPipeline,
            CorrectionInputAssetPath=current.CorrectionInputAssetPath, BaseAssetPath=current.BaseAssetPath, OutputAssetPath=current.OutputAssetPath,
            OutputWidth=size.Width, OutputHeight=size.Height, Override=current.Override,
            FinalOutputWidth=current.FinalOutputWidth, FinalOutputHeight=current.FinalOutputHeight };
        CommandService.Execute(FaceMutationCommands.CreateSetArtworkRecipeCommand(DocumentId, this, artwork,
            _faceDocumentModel.Provenance.Artwork, description)); return true;
    }

    public void ReloadArtworkImage()
    {
        if (_faceDocumentModel.Artwork?.Source.Kind != FaceArtworkSourceKind.Image) return;
        InvalidateFaceBuild(FaceBuildInput.ArtworkSource);
    }

    public bool SetArtworkOverrideRegistration(FacePerspectiveRegistrationModel registration,
        string description="Edit Artwork Override geometry")
    {
        var current = _faceDocumentModel.Artwork?.Override;
        if (current is null) return false;
        var normalized = registration.Normalize();
        return normalized.IsValid() && SetArtworkOverride(CopyOverride(current, perspectiveRegistration: normalized), description);
    }

    internal static FaceArtworkOverrideModel CopyOverride(FaceArtworkOverrideModel value,
        bool? enabled = null, FacePerspectiveRegistrationModel? perspectiveRegistration = null,
        double? x = null, double? y = null, double? width = null, double? height = null,
        FaceArtworkOverrideAlphaSource? alphaSource = null) => new()
    {
        Enabled=enabled ?? value.Enabled, AssetPath=value.AssetPath, PixelWidth=value.PixelWidth, PixelHeight=value.PixelHeight,
        PerspectiveRegistration=perspectiveRegistration ?? value.PerspectiveRegistration,
        X=x ?? value.X, Y=y ?? value.Y, Width=width ?? value.Width, Height=height ?? value.Height,
        AlphaSource=alphaSource ?? value.AlphaSource,
        ContentRevision=value.ContentRevision
    };

    public bool CreateArtworkOverrideFromBase(out string? error)
    {
        error=null; var artwork=_faceDocumentModel.Artwork; var project=_projectAccessor?.Invoke();
        if(artwork is null||project is null){error="Artwork or project is unavailable.";return false;}
        if(_faceDocumentModel.BuildState.Get(FaceGeneratedProduct.BaseArtwork).Status!=FaceBuildStatus.Current)
        {error="Build the current Base Artwork before creating an Override.";return false;}
        try { return SetArtworkOverride(FaceArtworkOverrideAssetService.CreateFromBase(artwork,project,_faceDocumentModel.Title),"Create Artwork Override from Base"); }
        catch(Exception exception){error=exception.Message;return false;}
    }

    public bool ImportArtworkOverride(string path, bool preserveAlignment, out string? error)
    {
        error=null;var artwork=_faceDocumentModel.Artwork;var project=_projectAccessor?.Invoke();
        if(artwork is null||project is null){error="Artwork or project is unavailable.";return false;}
        try{return SetArtworkOverride(FaceArtworkOverrideAssetService.Import(path,project,_faceDocumentModel.Title,
            preserveAlignment?artwork.Override:null),artwork.Override is null?"Import Artwork Override":"Replace Artwork Override");}
        catch(Exception exception){error=exception.Message;return false;}
    }

    public bool ReloadArtworkOverride(out string? error)
    {
        error=null;var artwork=_faceDocumentModel.Artwork;var project=_projectAccessor?.Invoke();
        if(artwork?.Override is null||project is null){error="Artwork Override or project is unavailable.";return false;}
        try{return SetArtworkOverride(FaceArtworkOverrideAssetService.Reload(artwork.Override,project),"Reload Artwork Override");}
        catch(Exception exception){error=exception.Message;return false;}
    }

    public bool SetArtworkOverride(FaceArtworkOverrideModel? value, string description="Edit Artwork Override")
    {
        var artwork=_faceDocumentModel.Artwork;if(artwork is null||value is { } configured&&!configured.IsValid())return false;
        CommandService.Execute(FaceMutationCommands.CreateSetArtworkOverrideCommand(DocumentId,this,
            FaceDocumentCopy.WithOverride(artwork,value),description));return true;
    }

    public FaceBuildResult BuildFace(bool force = false)
    {
        FaceBuildConfigurationService.ReconcileArtwork(_faceDocumentModel);
        ReconcileRuntimeAssetsConfiguration();
        var service = new FaceBuildService();
        var result = service.Build(_faceDocumentModel.BuildState, CreateFaceBuildExecutors(), force);
        CompleteFaceBuild(result);
        return result;
    }

    internal sealed record FaceBuildDocumentSnapshot(
        EditorDocument Document,
        string? PanelLayoutJson,
        string? FaceDocumentJson,
        string? CabinetDocumentJson,
        string? MachineDocumentJson);

    internal sealed record FaceBuildWorkItem(
        EditorDocument Document,
        string FaceDocumentJson,
        EditorProject? Project,
        IReadOnlyList<FaceBuildDocumentSnapshot> OpenDocuments,
        bool Force);

    internal sealed record PreparedFaceBuild(
        EditorDocument Document,
        FaceDocumentModel FaceDocument,
        FaceBuildResult Result);

    internal FaceBuildWorkItem PrepareFaceBuild(bool force)
    {
        FaceBuildConfigurationService.ReconcileArtwork(_faceDocumentModel);
        ReconcileRuntimeAssetsConfiguration();
        var snapshots = (_openDocumentsAccessor?.Invoke() ?? [])
            .Select(document => new FaceBuildDocumentSnapshot(
                document.Document,
                document.PanelLayoutJson,
                document.FaceDocumentJson,
                document.CabinetDocumentJson,
                document.GetMachineDocumentJson()))
            .ToArray();
        return new FaceBuildWorkItem(_document, GetFaceDocumentJson(), _projectAccessor?.Invoke(), snapshots, force);
    }

    internal static PreparedFaceBuild ExecutePreparedFaceBuild(
        FaceBuildWorkItem workItem,
        IEditorProgressReporter progress,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var openDocuments = workItem.OpenDocuments.Select(snapshot => new DocumentTabViewModel(
            document: snapshot.Document,
            panelLayoutJson: snapshot.PanelLayoutJson,
            faceDocumentJson: snapshot.FaceDocumentJson,
            cabinetDocumentJson: snapshot.CabinetDocumentJson,
            machineDocumentJson: snapshot.MachineDocumentJson)).ToArray();
        try
        {
            foreach (var openDocument in openDocuments)
            {
                openDocument._isDetachedFaceBuildWorker = true;
                openDocument.SetProjectAccessor(() => workItem.Project);
                openDocument.SetOpenDocumentsAccessor(() => openDocuments);
            }

            using var worker = new DocumentTabViewModel(workItem.Document, faceDocumentJson: workItem.FaceDocumentJson)
            {
                _isDetachedFaceBuildWorker = true,
                _faceBuildCancellationToken = cancellationToken
            };
            worker.SetProjectAccessor(() => workItem.Project);
            worker.SetOpenDocumentsAccessor(() => openDocuments);
            FaceBuildConfigurationService.ReconcileArtwork(worker._faceDocumentModel);
            worker.ReconcileRuntimeAssetsConfiguration();
            var result = new FaceBuildService().Build(
                worker._faceDocumentModel.BuildState,
                worker.CreateFaceBuildExecutors(),
                workItem.Force,
                progress: progress,
                cancellationToken: cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            worker.CompleteFaceBuild(result);
            return new PreparedFaceBuild(worker.Document, worker._faceDocumentModel, result);
        }
        finally
        {
            foreach (var openDocument in openDocuments) openDocument.Dispose();
        }
    }

    internal void CommitPreparedFaceBuild(PreparedFaceBuild prepared)
    {
        ArgumentNullException.ThrowIfNull(prepared);
        _document = prepared.Document;
        _faceDocumentModel = prepared.FaceDocument;
        _faceDocumentJsonIsCurrent = false;
        _faceDocumentJson = GetFaceDocumentJson();
        InvalidateCorrectionInputCache();
        if (prepared.Result.Built.Contains(FaceGeneratedProduct.BaseArtwork))
        {
            _faceWorkspace?.RefreshArtworkPreviews(true, false);
        }
        if (prepared.Result.Built.Contains(FaceGeneratedProduct.ArtworkOutput)
            && _faceDocumentModel.Artwork is { } artwork)
        {
            NotifyGeneratedArtworkChanged(artwork);
        }
        if (prepared.Result.Built.Count > 0 || prepared.Result.Failed.Count > 0)
        {
            PanelChanged?.Invoke(new PanelChangeEvent(
                DocumentId,
                null,
                PanelChangeProperties.Metadata | PanelChangeProperties.Structure,
                AffectsCanvas: prepared.Result.Built.Count > 0,
                AffectsHierarchy: true,
                AffectsInspectorRows: true,
                AffectsPersistence: false));
        }
        _faceWorkspace?.RefreshSummaries();
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FaceDocumentJson)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Document)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Title)));
    }

    internal FaceBuildResult BuildArtwork()
    {
        var result = new FaceBuildService().Build(_faceDocumentModel.BuildState, CreateFaceBuildExecutors(),
            includedProducts: new HashSet<FaceGeneratedProduct>
            {
                FaceGeneratedProduct.ArtworkCorrectionInput,
                FaceGeneratedProduct.BaseArtwork,
                FaceGeneratedProduct.ArtworkOutput
            });
        CompleteFaceBuild(result);
        return result;
    }

    private IReadOnlyDictionary<FaceGeneratedProduct, Func<FaceBuildNodeResult>> CreateFaceBuildExecutors() =>
        new Dictionary<FaceGeneratedProduct, Func<FaceBuildNodeResult>>
        {
            [FaceGeneratedProduct.ArtworkCorrectionInput] = BuildArtworkCorrectionInput,
            [FaceGeneratedProduct.BaseArtwork] = BuildBaseArtwork,
            [FaceGeneratedProduct.ArtworkOutput] = () => TryFinalizeFaceArtwork(out var error)
                ? new(FaceGeneratedProduct.ArtworkOutput, true)
                : new(FaceGeneratedProduct.ArtworkOutput, false, error),
            [FaceGeneratedProduct.LampMask] = BuildLampMask,
            [FaceGeneratedProduct.Trays] = BuildTrays,
            [FaceGeneratedProduct.RuntimeAssets] = BuildRuntimeAssets
        };

    private void CompleteFaceBuild(FaceBuildResult result)
    {
        _faceDocumentJsonIsCurrent = false;
        _faceDocumentJson = GetFaceDocumentJson();
        PersistBuildStateWhenDocumentIsClean(result);
        if(result.Built.Contains(FaceGeneratedProduct.BaseArtwork))_faceWorkspace?.RefreshArtworkPreviews(true,false);
        _faceWorkspace?.RefreshSummaries();
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FaceDocumentJson)));
    }

    private FaceBuildNodeResult BuildLampMask()
    {
        var project = _projectAccessor?.Invoke();
        if (project is null)
        {
            return new(FaceGeneratedProduct.LampMask, false, "The source Panel2D mask cannot be rebuilt because no project is open.");
        }
        var configuredMask=_faceDocumentModel.MaskLayer;
        if(configuredMask?.SourceKind==FaceLampMaskSourceKind.AuthoredImage)
        {
            var source=ResolveGeneratedPath(configuredMask.AuthoredAssetPath,project.ProjectDirectory);
            var destination=ResolveGeneratedPath(configuredMask.AssetPath,project.ProjectDirectory);
            if(string.IsNullOrWhiteSpace(source)||!File.Exists(source))return new(FaceGeneratedProduct.LampMask,false,"The authored lamp-mask image is unavailable.");
            if(string.IsNullOrWhiteSpace(destination))return new(FaceGeneratedProduct.LampMask,false,"The generated lamp-mask path is not configured.");
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            using var bitmap=SkiaSharp.SKBitmap.Decode(source);if(bitmap is null)return new(FaceGeneratedProduct.LampMask,false,"The authored lamp-mask image could not be decoded.");
            var authoredWidth=_faceDocumentModel.Artwork is { } authoredArtworkWidth ? (authoredArtworkWidth.FinalOutputWidth > 0 ? authoredArtworkWidth.FinalOutputWidth : authoredArtworkWidth.OutputWidth) : bitmap.Width;
            var authoredHeight=_faceDocumentModel.Artwork is { } authoredArtworkHeight ? (authoredArtworkHeight.FinalOutputHeight > 0 ? authoredArtworkHeight.FinalOutputHeight : authoredArtworkHeight.OutputHeight) : bitmap.Height;
            using var normalized=bitmap.Width==authoredWidth&&bitmap.Height==authoredHeight?bitmap.Copy():bitmap.Resize(new SkiaSharp.SKImageInfo(authoredWidth,authoredHeight),SkiaSharp.SKFilterQuality.High);
            if(normalized is null)return new(FaceGeneratedProduct.LampMask,false,"The authored lamp-mask image could not be normalized.");
            using var image=SkiaSharp.SKImage.FromBitmap(normalized);using var data=image.Encode(SkiaSharp.SKEncodedImageFormat.Png,100);using var stream=File.Open(destination,FileMode.Create,FileAccess.Write);data.SaveTo(stream);
            return new(FaceGeneratedProduct.LampMask,true);
        }
        if (!TryResolveSourcePanel(out var panel, out var sourceError))
        {
            return new(FaceGeneratedProduct.LampMask, false, sourceError);
        }
        var shape = panel.FaceSourceShapes.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, _faceDocumentModel.SourceFaceShapeId, StringComparison.Ordinal));
        if (shape is null)
        {
            return new(FaceGeneratedProduct.LampMask, false,
                $"Face Source Shape '{_faceDocumentModel.SourceFaceShapeId}' was not found in the source Panel2D.");
        }
        var width = _faceDocumentModel.Artwork is { } artworkWidth ? (artworkWidth.FinalOutputWidth > 0 ? artworkWidth.FinalOutputWidth : artworkWidth.OutputWidth) : _faceDocumentModel.MaskLayer?.Width ?? 0;
        var height = _faceDocumentModel.Artwork is { } artworkHeight ? (artworkHeight.FinalOutputHeight > 0 ? artworkHeight.FinalOutputHeight : artworkHeight.OutputHeight) : _faceDocumentModel.MaskLayer?.Height ?? 0;
        if (width <= 0 || height <= 0)
        {
            return new(FaceGeneratedProduct.LampMask, false, "The Face has no valid output dimensions for mask generation.");
        }
        var lampWindows = _faceDocumentModel.Elements.OfType<FaceLampWindowElement>().Where(window => window.IsVisible).ToArray();
        if (lampWindows.Length == 0)
        {
            return new(FaceGeneratedProduct.LampMask, false, "The configured lamp mask has no visible Face lamp windows to generate.");
        }
        var sourceLamps = panel.Elements.Where(element => element.Kind == PanelElementKind.Lamp
                && !string.IsNullOrWhiteSpace(element.ObjectId))
            .ToDictionary(element => element.ObjectId, StringComparer.Ordinal);
        foreach (var window in lampWindows)
        {
            if (string.IsNullOrWhiteSpace(window.LinkedPanel2DElementId)
                || !sourceLamps.TryGetValue(window.LinkedPanel2DElementId, out var lamp)
                || string.IsNullOrWhiteSpace(lamp.AssetPath))
            {
                return new(FaceGeneratedProduct.LampMask, false,
                    $"Lamp window '{window.Name}' has no usable linked Panel2D lamp artwork.");
            }
            var sourceAssetPath = ResolveGeneratedPath(lamp.AssetPath, project.ProjectDirectory);
            if (string.IsNullOrWhiteSpace(sourceAssetPath) || !File.Exists(sourceAssetPath))
            {
                return new(FaceGeneratedProduct.LampMask, false,
                    $"Lamp artwork '{lamp.AssetPath}' required by lamp window '{window.Name}' is unavailable.");
            }
        }
        var maskPath = ResolveGeneratedPath(_faceDocumentModel.MaskLayer?.AssetPath, project.ProjectDirectory);
        if (string.IsNullOrWhiteSpace(maskPath))
        {
            return new(FaceGeneratedProduct.LampMask, false, "The Face has no configured generated lamp-mask path.");
        }
        var mask = new FaceGenerationService().GenerateMaskFromSourceShape(
            panel, shape, width, height, lampWindows,
            _faceDocumentModel.Id, _faceDocumentModel.SourcePanel2DDocumentId, project.ProjectDirectory,
            ProjectAssetPathService.GetPackageAssetNameFromManifestPath(FilePath, EditorAssetType.Face) ?? _faceDocumentModel.Title,
            maskPath, _faceDocumentModel.GenerationSettings.MaskExtractionThreshold,
            ImageProcessingExecutionPolicy.Current.WithCancellation(_faceBuildCancellationToken));
        if (mask is null)
        {
            return new(FaceGeneratedProduct.LampMask, false, "Lamp-mask generation did not produce an output.");
        }
        _faceDocumentModel = FaceDocumentCopy.WithMaskLayer(_faceDocumentModel, mask);
        return new(FaceGeneratedProduct.LampMask, true);
    }

    private bool TryResolveSourcePanel(out Panel2DDocumentModel panel, out string error)
    {
        panel = new Panel2DDocumentModel();
        error = string.Empty;
        var sourcePath = _faceDocumentModel.SourcePanel2DDocumentPath?.Trim();
        var sourceId = _faceDocumentModel.SourcePanel2DDocumentId?.Trim();
        if (string.IsNullOrWhiteSpace(_faceDocumentModel.SourceFaceShapeId)
            || (string.IsNullOrWhiteSpace(sourcePath) && string.IsNullOrWhiteSpace(sourceId)))
        {
            error = "The Face has no complete Panel2D / Face Source Shape linkage for lamp-mask generation.";
            return false;
        }
        var open = (_openDocumentsAccessor?.Invoke() ?? []).FirstOrDefault(document =>
            document.Document.DocumentType == EditorDocumentType.Panel2D
            && ((!string.IsNullOrWhiteSpace(sourcePath) && PathsEqual(document.FilePath, sourcePath))
                || (!string.IsNullOrWhiteSpace(sourceId)
                    && (string.Equals(document.DocumentId.ToString("N"), sourceId, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(document.DocumentId.ToString("D"), sourceId, StringComparison.OrdinalIgnoreCase)
                        || PathsEqual(document.FilePath, sourceId)))));
        if (open is not null)
        {
            panel = open.GetPanelDocument();
            return true;
        }
        var project = _projectAccessor?.Invoke();
        var fullPath = ResolveGeneratedPath(sourcePath, project?.ProjectDirectory);
        if (!string.IsNullOrWhiteSpace(fullPath) && File.Exists(fullPath))
        {
            var json = File.ReadAllText(fullPath);
            panel = Panel2DDocumentStorage.DeserializeModel(json);
            return true;
        }
        error = $"The source Panel2D '{sourcePath ?? sourceId}' is unavailable. Open it or restore the linked file, then retry.";
        return false;
    }

    internal bool TryConvertComponentsFromSource(out IReadOnlyList<FaceElementModel> components, out string error)
    {
        components=[];
        if(!TryResolveSourcePanel(out var panel,out error))return false;
        var shape=panel.FaceSourceShapes.FirstOrDefault(value=>string.Equals(value.Id,_faceDocumentModel.SourceFaceShapeId,StringComparison.Ordinal));
        if(shape is null){error=$"Face Source Shape '{_faceDocumentModel.SourceFaceShapeId}' was not found in the source Panel2D.";return false;}
        var estimated=FaceSourceShapeTransformService.EstimateOutputSize(shape,null);
        // Component geometry stays in the established Face logical space. It must not follow a replacement
        // artwork bitmap's pixel dimensions (Phase 5 deliberately makes artwork resolution independent).
        var logicalArtwork=_faceDocumentModel.Elements.OfType<FaceArtworkElement>().FirstOrDefault();
        var width=logicalArtwork?.Width>0?(int)Math.Round(logicalArtwork.Width):estimated.Width;
        var height=logicalArtwork?.Height>0?(int)Math.Round(logicalArtwork.Height):estimated.Height;
        var projectDirectory=_projectAccessor?.Invoke()?.ProjectDirectory;
        components=new FaceSemanticElementConversionService().ConvertSupportedElements(panel,shape,width,height,projectDirectory)
            .Where(FaceElementClassification.IsComponent).ToArray();
        return true;
    }

    private void PersistBuildStateWhenDocumentIsClean(FaceBuildResult result)
    {
        if (IsDirty) return; // The next normal Save persists authored changes and build state together.
        if (!string.IsNullOrWhiteSpace(FilePath) && File.Exists(FilePath))
        {
            try { File.WriteAllText(FilePath, _faceDocumentJson); }
            catch (Exception exception)
            {
                foreach (var product in result.Built.ToArray())
                {
                    var message = $"{product} was built, but its Current state could not be saved: {exception.Message}";
                    var node = _faceDocumentModel.BuildState.Get(product);
                    node.Status = FaceBuildStatus.Error;
                    node.ErrorMessage = message;
                    result.Built.Remove(product);
                    result.Failed.Add(new FaceBuildNodeResult(product, false, message));
                }
                _faceDocumentJsonIsCurrent = false;
                _faceDocumentJson = GetFaceDocumentJson();
            }
            return;
        }
        MarkDirty(); // An unsaved document needs a normal Save to establish its .face file.
    }

    private static bool PathsEqual(string? left, string? right) =>
        string.Equals(left?.Replace('\\', '/'), right?.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase);

    private static string? ResolveGeneratedPath(string? path, string? projectDirectory)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        return Path.IsPathRooted(path) ? path : string.IsNullOrWhiteSpace(projectDirectory)
            ? null : Path.Combine(projectDirectory, path.Replace('/', Path.DirectorySeparatorChar));
    }

    private FaceBuildNodeResult BuildTrays()
    {
        var generated = new FaceTrayAutoAuthoringService().AutoAuthor(_faceDocumentModel, _projectAccessor?.Invoke()?.ProjectDirectory);
        _faceDocumentModel = FaceDocumentCopy.WithGeneratedIllumination(_faceDocumentModel, generated.Trays, generated.Emitters);
        return new(FaceGeneratedProduct.Trays, true);
    }

    private FaceBuildNodeResult BuildRuntimeAssets()
    {
        var project = _projectAccessor?.Invoke();
        if (project is null) return new(FaceGeneratedProduct.RuntimeAssets, false, "No project is open.");
        var capability = _runtimeAssetsConfiguration.Evaluate(
            _faceDocumentModel, project, _openDocumentsAccessor?.Invoke() ?? []);
        if (!capability.IsConfigured)
        {
            return new(FaceGeneratedProduct.RuntimeAssets, false,
                capability.Reason ?? "Standalone Face runtime assets are not configured.");
        }
        var exported = new FaceRuntimeExportService().Export(_faceDocumentModel, project, FilePath);
        _faceDocumentModel = exported.Document;
        return new(FaceGeneratedProduct.RuntimeAssets, true);
    }

    internal void ReconcileRuntimeAssetsConfiguration()
    {
        if (Document.DocumentType != EditorDocumentType.Face) return;
        var capability = _runtimeAssetsConfiguration.Evaluate(
            _faceDocumentModel, _projectAccessor?.Invoke(), _openDocumentsAccessor?.Invoke() ?? []);
        _runtimeAssetsConfiguration.Reconcile(_faceDocumentModel, capability);
        _faceDocumentJsonIsCurrent = false;
        _faceWorkspace?.RefreshBuildState();
    }

    internal void InvalidateFaceBuild(FaceBuildInput input)
    {
        new FaceBuildService().Invalidate(_faceDocumentModel.BuildState, input);
        if (input is FaceBuildInput.ArtworkSource or FaceBuildInput.ArtworkPreprocessing)
            InvalidateCorrectionInputCache();
        _faceDocumentJsonIsCurrent = false;
        _faceWorkspace?.RefreshBuildState();
    }

    private FaceBuildNodeResult BuildArtworkCorrectionInput()
    {
        var artwork = _faceDocumentModel.Artwork;
        var project = _projectAccessor?.Invoke();
        if (artwork is null || project is null) return new(FaceGeneratedProduct.ArtworkCorrectionInput, false, "Artwork or project is unavailable.");
        if (string.IsNullOrWhiteSpace(artwork.CorrectionInputAssetPath)) return new(FaceGeneratedProduct.ArtworkCorrectionInput, false, "The correction-input path is not configured.");
        string? built;
        if (artwork.Source.Kind == FaceArtworkSourceKind.Image)
        {
            built = new FaceArtworkRebuildService().RebuildImageCorrectionInput(artwork, project.ProjectDirectory,
                artwork.CorrectionInputAssetPath, _faceDocumentModel.GenerationSettings);
        }
        else
        {
            if (!TryResolveSourcePanel(out var panel, out var error)) return new(FaceGeneratedProduct.ArtworkCorrectionInput, false, error);
            var shape = panel.FaceSourceShapes.FirstOrDefault(candidate => string.Equals(candidate.Id, artwork.Source.FaceSourceShapeId ?? _faceDocumentModel.SourceFaceShapeId, StringComparison.Ordinal));
            if (shape is null) return new(FaceGeneratedProduct.ArtworkCorrectionInput, false, "The linked Face Source Shape is unavailable.");
            built = new FaceArtworkRebuildService().RebuildCorrectionInput(artwork, panel, shape,
                project.ProjectDirectory, artwork.CorrectionInputAssetPath, _faceDocumentModel.GenerationSettings);
        }
        InvalidateCorrectionInputCache();
        return string.IsNullOrWhiteSpace(built)
            ? new(FaceGeneratedProduct.ArtworkCorrectionInput, false, "Correction input generation failed.")
            : new(FaceGeneratedProduct.ArtworkCorrectionInput, true);
    }

    private FaceBuildNodeResult BuildBaseArtwork()
    {
        var artwork = _faceDocumentModel.Artwork;
        var project = _projectAccessor?.Invoke();
        if (artwork is null || project is null) return new(FaceGeneratedProduct.BaseArtwork, false, "Artwork or project is unavailable.");
        var result = new FaceArtworkRebuildService().BuildBaseFromCorrectionInput(artwork, project.ProjectDirectory);
        return new FaceBuildNodeResult(FaceGeneratedProduct.BaseArtwork, result.Succeeded, result.ErrorMessage);
    }

    internal bool TryFinalizeFaceArtwork(out string? errorMessage)
    {
        var artwork = _faceDocumentModel.Artwork;
        var project = _projectAccessor?.Invoke();
        if (artwork is null || project is null) { errorMessage = "Artwork or project is unavailable."; return false; }
        var result = new FaceArtworkRebuildService().FinalizeOutput(artwork, project.ProjectDirectory);
        errorMessage = result.ErrorMessage;
        if (!result.Succeeded) return false;
        var outputPath=FaceArtworkGeneratedPathService.Resolve(artwork.OutputAssetPath!,project.ProjectDirectory);
        using(var output=SKBitmap.Decode(outputPath))
            if(output is not null)_faceDocumentModel=FaceDocumentCopy.WithArtwork(_faceDocumentModel,
                FaceDocumentCopy.WithOverride(artwork,artwork.Override,output.Width,output.Height),_faceDocumentModel.Provenance.Artwork);
        NotifyGeneratedArtworkChanged(artwork);
        return true;
    }

    internal bool TryReadGeneratedArtwork(out byte[] bytes, out string? errorMessage)
    {
        bytes = [];
        errorMessage = null;
        if (!TryGetGeneratedArtworkPath(out var path))
        {
            errorMessage = "The generated artwork path is missing or cannot be resolved because no project is open.";
            return false;
        }
        if (!File.Exists(path))
        {
            errorMessage = $"Generated artwork was not found at '{path}'. Regenerate the Face before applying artwork processing.";
            return false;
        }
        try
        {
            bytes = File.ReadAllBytes(path);
            return true;
        }
        catch (Exception exception)
        {
            errorMessage = $"Generated artwork could not be read from '{path}': {exception.Message}";
            return false;
        }
    }

    internal bool TryRestoreGeneratedArtwork(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (bytes.Length == 0 || _faceDocumentModel.Artwork is not { } artwork || !TryGetGeneratedArtworkPath(out var path)) return false;
        File.WriteAllBytes(path, bytes);
        NotifyGeneratedArtworkChanged(artwork);
        return true;
    }

    private bool TryGetGeneratedArtworkPath(out string path)
    {
        path = string.Empty;
        var artworkPath = _faceDocumentModel.Artwork?.OutputAssetPath;
        var project = _projectAccessor?.Invoke();
        if (project is null || string.IsNullOrWhiteSpace(artworkPath)) return false;
        path = Path.IsPathRooted(artworkPath) ? artworkPath : Path.Combine(project.ProjectDirectory, artworkPath.Replace('/', Path.DirectorySeparatorChar));
        return true;
    }

    private void NotifyGeneratedArtworkChanged(FaceArtworkModel artwork)
    {
        if (_isDetachedFaceBuildWorker) return;
        Views.SkiaFaceEditView.InvalidateArtworkImage(artwork.OutputAssetPath);
        FacePreviewChanged?.Invoke(new FacePreviewChangedEvent(DocumentId));
        PanelChanged?.Invoke(new PanelChangeEvent(DocumentId, artwork.Id, PanelChangeProperties.Metadata, AffectsCanvas: true, AffectsHierarchy: false, AffectsInspectorRows: false, AffectsPersistence: false));
    }

    internal ArtworkCalibrationMeasurements GetArtworkCalibrationMeasurements(
        ArtworkCalibrationOperationModel operation,
        bool allowInputEvaluation = true)
    {
        var sampleColors = new Dictionary<string, string?>(StringComparer.Ordinal);
        string? blackColor = operation.BlackReference.ManualEnabled ? operation.BlackReference.ManualColor : null;
        string? whiteColor = operation.WhiteReference.ManualEnabled ? operation.WhiteReference.ManualColor : null;
        var artwork = _faceDocumentModel.Artwork;
        if (artwork is null || _projectAccessor?.Invoke() is null || !TryGetCorrectionInputBitmap(out var original))
            return new ArtworkCalibrationMeasurements(blackColor, whiteColor, sampleColors);

        var index = artwork.ProcessingPipeline.Operations.ToList().FindIndex(candidate => candidate.Id == operation.Id);
        var input = original;
        if (index > 0)
        {
            var prefixFingerprint = CreateProcessingPrefixFingerprint(artwork.ProcessingPipeline, index);
            if (_calibrationOperationInputs.TryGetValue(operation.Id, out var cached)
                && string.Equals(cached.PrefixFingerprint, prefixFingerprint, StringComparison.Ordinal))
            {
                input = cached.Bitmap;
            }
            else
            {
                if (cached is not null)
                {
                    cached.Bitmap.Dispose();
                    _calibrationOperationInputs.Remove(operation.Id);
                }

                if (!allowInputEvaluation)
                    return new ArtworkCalibrationMeasurements(blackColor, whiteColor, sampleColors);

                input = new FaceArtworkProcessingPipeline().Evaluate(original, artwork.ProcessingPipeline, index);
                _calibrationOperationInputs[operation.Id] = new CalibrationOperationInputCacheEntry(prefixFingerprint, input);
            }
        }
        FaceArtworkProcessingPipeline.TryResolveReferenceColors(input, operation, out blackColor, out whiteColor);
        foreach (var sample in operation.BlackReference.Samples
                     .Concat(operation.WhiteReference.Samples)
                     .Concat(operation.SameColorGroups.SelectMany(group => group.Samples)))
        {
            sampleColors[sample.Id] = FaceArtworkProcessingPipeline.MeasureSampleHex(input, sample);
        }

        return new ArtworkCalibrationMeasurements(blackColor, whiteColor, sampleColors);
    }

    private bool TryGetCorrectionInputBitmap(out SKBitmap bitmap)
    {
        bitmap = null!;
        var artwork = _faceDocumentModel.Artwork;
        var project = _projectAccessor?.Invoke();
        if (artwork is null || project is null || string.IsNullOrWhiteSpace(artwork.CorrectionInputAssetPath)
            || _faceDocumentModel.BuildState.Get(FaceGeneratedProduct.ArtworkCorrectionInput).Status != FaceBuildStatus.Current)
            return false;
        var path = FaceArtworkGeneratedPathService.Resolve(artwork.CorrectionInputAssetPath, project.ProjectDirectory);
        if (!File.Exists(path)) return false;
        var key = $"{Path.GetFullPath(path)}|{File.GetLastWriteTimeUtc(path).Ticks}";
        if (_correctionInputBitmap is null || !string.Equals(_correctionInputCacheKey, key, StringComparison.OrdinalIgnoreCase))
        {
            InvalidateCorrectionInputCache();
            _correctionInputBitmap = SKBitmap.Decode(path);
            _correctionInputCacheKey = _correctionInputBitmap is null ? null : key;
        }
        bitmap = _correctionInputBitmap!;
        return bitmap is not null;
    }

    private void InvalidateCorrectionInputCache()
    {
        foreach (var cached in _calibrationOperationInputs.Values)
            cached.Bitmap.Dispose();
        _calibrationOperationInputs.Clear();
        _correctionInputBitmap?.Dispose();
        _correctionInputBitmap = null;
        _correctionInputCacheKey = null;
    }

    internal static string CreateProcessingPrefixFingerprint(ImageProcessingPipelineModel pipeline, int operationCount)
    {
        return string.Join('\n', pipeline.Operations.Take(operationCount)
            .Select(operation => $"{operation.GetType().FullName}:{JsonSerializer.Serialize(operation, operation.GetType())}"));
    }

    private void ReconcileCalibrationOperationInputCache(ImageProcessingPipelineModel? pipeline)
    {
        foreach (var (operationId, cached) in _calibrationOperationInputs.ToArray())
        {
            var index = pipeline?.Operations.ToList().FindIndex(operation => operation.Id == operationId) ?? -1;
            var stillValid = index > 0
                && string.Equals(cached.PrefixFingerprint,
                    CreateProcessingPrefixFingerprint(pipeline!, index), StringComparison.Ordinal);
            if (stillValid)
                continue;

            cached.Bitmap.Dispose();
            _calibrationOperationInputs.Remove(operationId);
        }
    }

    public string GetFaceDocumentJson()
    {
        if (_faceDocumentJsonIsCurrent && _faceDocumentJson is not null)
            return _faceDocumentJson;

        var json = FaceDocumentStorage.Serialize(_faceDocumentModel);
        _faceDocumentJson = json;
        _faceDocumentJsonIsCurrent = true;
        return json;
    }

    internal void RefreshFaceArtworkProcessingState() => _faceWorkspace?.RefreshArtworkProcessingState();

    internal IReadOnlyList<FaceElementModel> GetFaceElements()
    {
        return _faceDocumentModel.Elements;
    }

    internal bool TryGetFaceElement(PanelSelectionInfo selection, out FaceElementModel element)
    {
        var match = _faceDocumentModel.Elements.FirstOrDefault(candidate =>
            !string.IsNullOrWhiteSpace(selection.ObjectId)
            && string.Equals(candidate.ObjectId, selection.ObjectId, StringComparison.Ordinal));
        if (match is null)
        {
            element = new FaceLampWindowElement();
            return false;
        }

        element = match;
        return true;
    }

    internal void SetFaceDocument(
        FaceDocumentModel model,
        PanelChangeEvent? faceChange = null,
        bool updateSerializedDocument = true,
        bool affectsFacePreview = true,
        bool? affectsPersistence = null,
        bool refreshWorkspaceSummaries = true)
    {
        ArgumentNullException.ThrowIfNull(model);

        ReconcileCalibrationOperationInputCache(model.Artwork?.ProcessingPipeline);
        _faceDocumentModel = model;
        if (affectsPersistence ?? updateSerializedDocument)
            _faceDocumentJsonIsCurrent = false;
        if (refreshWorkspaceSummaries)
            _faceWorkspace?.RefreshSummaries();
        if (updateSerializedDocument)
        {
            ReconcileSelection();
            if (affectsFacePreview)
            {
                FacePreviewChanged?.Invoke(new FacePreviewChangedEvent(DocumentId));
            }
        }

        if (faceChange is PanelChangeEvent change)
        {
            PanelChanged?.Invoke(change);
        }
    }

    internal void ApplySavedFaceDocument(FaceDocumentModel model)
    {
        SetFaceDocument(model, affectsPersistence: false);
        _faceDocumentJson = FaceDocumentStorage.Serialize(model);
        _faceDocumentJsonIsCurrent = true;
    }

    internal void SetFaceElements(IReadOnlyList<FaceElementModel> elements, PanelChangeEvent? faceChange = null, bool updateSerializedDocument = true)
    {
        SetFaceDocument(new FaceDocumentModel
        {
            Id = _faceDocumentModel.Id,
            Title = _faceDocumentModel.Title,
            Summary = _faceDocumentModel.Summary,
            SourcePanel2DDocumentId = _faceDocumentModel.SourcePanel2DDocumentId,
            SourcePanel2DDocumentPath = _faceDocumentModel.SourcePanel2DDocumentPath,
            SourceFaceShapeId = _faceDocumentModel.SourceFaceShapeId,
            SourceRegion = _faceDocumentModel.SourceRegion,
            LastRegeneratedAtUtc = _faceDocumentModel.LastRegeneratedAtUtc,
            GenerationSettings = _faceDocumentModel.GenerationSettings,
            Provenance = _faceDocumentModel.Provenance, BuildState = _faceDocumentModel.BuildState,
            Artwork = _faceDocumentModel.Artwork,
            RuntimeRenderAssets = _faceDocumentModel.RuntimeRenderAssets,
            MaskLayer = _faceDocumentModel.MaskLayer,
            Trays = _faceDocumentModel.Trays,
            LampEmitters = _faceDocumentModel.LampEmitters,
            Layers = _faceDocumentModel.Layers,
            Elements = elements.ToArray()
        }, faceChange, updateSerializedDocument);
    }

    internal Panel2DDocumentModel GetPanelDocument()
    {
        return _panelDocumentModel;
    }

    internal IReadOnlyList<PanelElementModel> GetPanelElements()
    {
        return _panelDocumentModel.Elements;
    }

    internal IReadOnlyList<PanelFaceSourceShapeModel> GetPanelFaceSourceShapes()
    {
        return _panelDocumentModel.FaceSourceShapes;
    }

    internal bool TryGetPanelFaceSourceShape(string id, out PanelFaceSourceShapeModel shape)
    {
        shape = _panelDocumentModel.FaceSourceShapes.FirstOrDefault(s => string.Equals(s.Id, id, StringComparison.Ordinal)) ?? new PanelFaceSourceShapeModel();
        return !string.IsNullOrWhiteSpace(shape.Id);
    }

    internal void SetPanelFaceSourceShapes(IReadOnlyList<PanelFaceSourceShapeModel> shapes, PanelChangeEvent? panelChange = null)
    {
        _panelDocumentModel = new Panel2DDocumentModel
        {
            Title = _panelDocumentModel.Title,
            Summary = _panelDocumentModel.Summary,
            Elements = _panelDocumentModel.Elements,
            FaceSourceShapes = shapes.ToArray()
        };
        _panelLayoutJson = GetPanelLayoutProjectionJson();
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PanelLayoutJson)));
        ReconcileSelection();
        if (panelChange is PanelChangeEvent change) PanelChanged?.Invoke(change);
    }

    internal bool TryGetPanelElement(PanelSelectionInfo selection, out PanelElementModel element)
    {
        var match = _panelDocumentModel.Elements.FirstOrDefault(candidate => IsSelectionMatch(candidate, selection));
        if (match is null)
        {
            element = new PanelElementModel();
            return false;
        }

        element = match;
        return true;
    }

    internal bool TryGetPanelElementByObjectId(string objectId, out PanelElementModel element)
    {
        var match = _panelDocumentModel.Elements.FirstOrDefault(candidate =>
            !string.IsNullOrWhiteSpace(objectId)
            && string.Equals(candidate.ObjectId, objectId, StringComparison.Ordinal));
        element = match ?? new PanelElementModel();
        return match is not null;
    }

    internal bool TryGetFaceElementByObjectId(string objectId, out FaceElementModel element)
    {
        var match = _faceDocumentModel.Elements.FirstOrDefault(candidate =>
            !string.IsNullOrWhiteSpace(objectId)
            && string.Equals(candidate.ObjectId, objectId, StringComparison.Ordinal));
        element = match ?? new FaceLampWindowElement();
        return match is not null;
    }

    internal void ReconcileSelection()
    {
        SelectionState.Reconcile(item => item.Domain switch
        {
            EditorSelectionDomain.PanelElement => _panelDocumentModel.Elements.Any(element => string.Equals(element.ObjectId, item.ObjectId, StringComparison.Ordinal)),
            EditorSelectionDomain.FaceElement => _faceDocumentModel.Elements.Any(element => string.Equals(element.ObjectId, item.ObjectId, StringComparison.Ordinal)),
            EditorSelectionDomain.PanelFaceSourceShape => _panelDocumentModel.FaceSourceShapes.Any(shape => string.Equals(shape.Id, item.ObjectId, StringComparison.Ordinal)),
            EditorSelectionDomain.FaceMaskLayer => _faceDocumentModel.MaskLayer is { } maskLayer && string.Equals(FaceMaskLayerSelectionService.ToSelectionInfo(maskLayer).ObjectId, item.ObjectId, StringComparison.Ordinal),
            _ => false
        });
    }

    internal bool HasPanelElement(PanelSelectionInfo selection)
    {
        return _panelDocumentModel.Elements.Any(element => IsSelectionMatch(element, selection));
    }

    internal void SetPanelElements(IReadOnlyList<PanelElementModel> elements, PanelChangeEvent? panelChange = null)
    {
        _panelDocumentModel = new Panel2DDocumentModel
        {
            Title = _panelDocumentModel.Title,
            Summary = _panelDocumentModel.Summary,
            Elements = elements.ToArray(),
            FaceSourceShapes = _panelDocumentModel.FaceSourceShapes
        };
        RebuildLampCaches();

        _panelLayoutJson = GetPanelLayoutProjectionJson();
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PanelLayoutJson)));

        ReconcileSelection();
        if (panelChange is PanelChangeEvent change)
        {
            PanelChanged?.Invoke(change);
        }
    }

    internal void NotifyPanelVisualPreviewChanged()
    {
        var changedObjectIds = _panelDocumentModel.Elements
            .Where(element => !string.IsNullOrWhiteSpace(element.ObjectId)
                && (element.Kind == PanelElementKind.Lamp || element.Kind == PanelElementKind.Reel || element.Kind == PanelElementKind.Alpha || element.Kind == PanelElementKind.SevenSegment || element.Kind == PanelElementKind.VfdDotMatrix))
            .Select(element => element.ObjectId)
            .ToArray();
        NotifyPanelVisualPreviewChanged(changedObjectIds);
    }

    internal void NotifyPanelVisualPreviewChanged(IReadOnlyCollection<string> changedObjectIds)
    {
        if (changedObjectIds.Count == 0)
        {
            return;
        }

        _lastVisualStateByObjectId ??= new Dictionary<string, object>(StringComparer.Ordinal);
        var deltaByObjectId = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var objectId in changedObjectIds)
        {
            if (string.IsNullOrWhiteSpace(objectId) || !_visualStateObjectIds.Contains(objectId))
            {
                continue;
            }

            var nextState = _lampElementsByObjectId.ContainsKey(objectId)
                ? (object)new LampVisualState(
                _runtimeState.IsLampTestActive
                && !string.IsNullOrWhiteSpace(_runtimeState.LampTestObjectId)
                && string.Equals(objectId, _runtimeState.LampTestObjectId, StringComparison.Ordinal),
                _runtimeState.GetLampIntensity(objectId))
                : _reelElementsByObjectId.ContainsKey(objectId)
                    ? new ReelVisualState(_runtimeState.GetReelPosition(objectId))
                    : _sevenSegmentElementsByObjectId.ContainsKey(objectId)
                        ? new SegmentVisualState(_runtimeState.GetSegmentCellMasks(objectId, 1))
                        : _vfdDotMatrixElementsByObjectId.ContainsKey(objectId)
                            ? new VfdDotMatrixVisualState(_runtimeState.GetVfdDotMatrixDots(objectId, 128 * 8))
                            : new SegmentVisualState(_runtimeState.GetSegmentCellMasks(objectId, 16));
            if (!_lastVisualStateByObjectId.TryGetValue(objectId, out var previous)
                || !Equals(previous, nextState))
            {
                _lastVisualStateByObjectId[objectId] = nextState;
                deltaByObjectId[objectId] = nextState;
            }
        }

        if (deltaByObjectId.Count == 0)
        {
            return;
        }

        PanelVisualStateChanged?.Invoke(new PanelVisualStateChangedEvent(DocumentId, deltaByObjectId));
    }

    internal void NotifyFaceVisualPreviewChanged(IReadOnlyCollection<string> changedObjectIds)
    {
        if (changedObjectIds.Count == 0)
        {
            return;
        }

        var faceRuntimeElementIds = _faceDocumentModel.Elements
            .Where(element => !string.IsNullOrWhiteSpace(element.ObjectId)
                && element.LinkedMachineObjectReference is MachineObjectReference reference
                && reference.Kind is MachineObjectKind.Lamp or MachineObjectKind.Reel or MachineObjectKind.SevenSegmentDisplay or MachineObjectKind.AlphaDisplay
                && !reference.IsEmpty)
            .Select(element => element.ObjectId)
            .ToHashSet(StringComparer.Ordinal);

        var publishedObjectIds = changedObjectIds
            .Where(objectId => !string.IsNullOrWhiteSpace(objectId) && faceRuntimeElementIds.Contains(objectId))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (publishedObjectIds.Length == 0)
        {
            return;
        }

        FaceVisualStateChanged?.Invoke(new FaceVisualStateChangedEvent(DocumentId, publishedObjectIds));
    }

    internal bool TryGetLampElement(string objectId, out PanelElementModel element)
    {
        if (string.IsNullOrWhiteSpace(objectId))
        {
            element = new PanelElementModel();
            return false;
        }

        return _lampElementsByObjectId.TryGetValue(objectId, out element!);
    }



    internal bool TryGetReelElement(string objectId, out PanelElementModel element)
    {
        if (string.IsNullOrWhiteSpace(objectId))
        {
            element = new PanelElementModel();
            return false;
        }

        return _reelElementsByObjectId.TryGetValue(objectId, out element!);
    }

    internal bool TryGetAlphaElement(string objectId, out PanelElementModel element)
    {
        if (string.IsNullOrWhiteSpace(objectId))
        {
            element = new PanelElementModel();
            return false;
        }

        return _alphaElementsByObjectId.TryGetValue(objectId, out element!);
    }


    internal bool TryGetSevenSegmentElement(string objectId, out PanelElementModel element)
    {
        if (string.IsNullOrWhiteSpace(objectId))
        {
            element = new PanelElementModel();
            return false;
        }

        return _sevenSegmentElementsByObjectId.TryGetValue(objectId, out element!);
    }

    internal string GetPanelLayoutProjectionJson()
    {
        return Panel2DDocumentStorage.Serialize(
            _panelDocumentModel.Title,
            _panelDocumentModel.Summary,
            Panel2DDocumentStorage.ToStorageElements(_panelDocumentModel),
            _panelDocumentModel.FaceSourceShapes.Select(Panel2DDocumentStorage.ToStorageFaceSourceShape).ToArray());
    }

    /// <summary>
    /// Temporary single-selection compatibility shim. The document SelectionState is authoritative; this property mirrors only its primary item.
    /// </summary>
    public PanelSelectionInfo? HierarchySelectedPanelSelection
    {
        get => TryGetPrimaryPanelSelection(out var selection) ? selection : null;
        set
        {
            if (value is PanelSelectionInfo selection)
            {
                SelectionState.Replace(HierarchySelectionIdentityService.ToSelectionItem(selection));
            }
            else
            {
                SelectionState.Clear();
            }
        }
    }

    private void OnSelectionStateChanged(object? sender, DocumentSelectionChangedEventArgs eventArgs)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HierarchySelectedPanelSelection)));
        SelectionChanged?.Invoke(this, eventArgs);
    }

    private bool TryGetPrimaryPanelSelection(out PanelSelectionInfo selection)
    {
        if (SelectionState.PrimaryItem is { } item)
        {
            if (item.Domain == EditorSelectionDomain.PanelElement
                && TryGetPanelElementByObjectId(item.ObjectId, out var panelElement))
            {
                selection = PanelSelectionContract.ToSelectionInfo(Panel2DDocumentStorage.ToStorageElement(panelElement));
                return true;
            }

            if (item.Domain == EditorSelectionDomain.FaceElement
                && TryGetFaceElementByObjectId(item.ObjectId, out var faceElement))
            {
                selection = FaceSelectionService.ToSelectionInfo(faceElement);
                return true;
            }

            if (item.Domain == EditorSelectionDomain.PanelFaceSourceShape
                && TryGetPanelFaceSourceShape(item.ObjectId, out var shape))
            {
                selection = PanelFaceSourceShapeCommands.ToSelection(shape);
                return true;
            }

            if (item.Domain == EditorSelectionDomain.FaceMaskLayer
                && _faceDocumentModel.MaskLayer is { } maskLayer
                && string.Equals(FaceMaskLayerSelectionService.ToSelectionInfo(maskLayer).ObjectId, item.ObjectId, StringComparison.Ordinal))
            {
                selection = FaceMaskLayerSelectionService.ToSelectionInfo(maskLayer);
                return true;
            }
        }

        selection = default;
        return false;
    }

    public double PanelZoom
    {
        get => _panelZoom;
        set
        {
            if (Math.Abs(_panelZoom - value) < 0.0001)
            {
                return;
            }

            _panelZoom = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PanelZoom)));
        }
    }

    public double PanelPanX
    {
        get => _panelPanX;
        set
        {
            if (Math.Abs(_panelPanX - value) < 0.0001)
            {
                return;
            }

            _panelPanX = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PanelPanX)));
        }
    }

    public double PanelPanY
    {
        get => _panelPanY;
        set
        {
            if (Math.Abs(_panelPanY - value) < 0.0001)
            {
                return;
            }

            _panelPanY = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PanelPanY)));
        }
    }

    public double FaceZoom
    {
        get => _faceZoom;
        set
        {
            if (Math.Abs(_faceZoom - value) < 0.0001)
            {
                return;
            }

            _faceZoom = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FaceZoom)));
        }
    }

    public double FacePanX
    {
        get => _facePanX;
        set
        {
            if (Math.Abs(_facePanX - value) < 0.0001)
            {
                return;
            }

            _facePanX = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FacePanX)));
        }
    }

    public double FacePanY
    {
        get => _facePanY;
        set
        {
            if (Math.Abs(_facePanY - value) < 0.0001)
            {
                return;
            }

            _facePanY = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FacePanY)));
        }
    }

    private static bool IsSelectionMatch(PanelElementModel element, PanelSelectionInfo selection)
    {
        if (!string.IsNullOrWhiteSpace(selection.ObjectId)
            && string.Equals(element.ObjectId, selection.ObjectId, StringComparison.Ordinal))
        {
            return true;
        }

        var storageElement = Panel2DDocumentStorage.ToStorageElement(element);
        return PanelSelectionContract.IsMatch(storageElement, selection);
    }

    private void RebuildLampCaches()
    {
        _lampElementsByObjectId = _panelDocumentModel.Elements
            .Where(element => element.Kind == PanelElementKind.Lamp
                && !string.IsNullOrWhiteSpace(element.ObjectId))
            .ToDictionary(element => element.ObjectId, element => element, StringComparer.Ordinal);
        _reelElementsByObjectId = _panelDocumentModel.Elements
            .Where(element => element.Kind == PanelElementKind.Reel
                && !string.IsNullOrWhiteSpace(element.ObjectId))
            .ToDictionary(element => element.ObjectId, element => element, StringComparer.Ordinal);
        _alphaElementsByObjectId = _panelDocumentModel.Elements
            .Where(element => element.Kind == PanelElementKind.Alpha && !string.IsNullOrWhiteSpace(element.ObjectId))
            .ToDictionary(element => element.ObjectId, element => element, StringComparer.Ordinal);
        _sevenSegmentElementsByObjectId = _panelDocumentModel.Elements
            .Where(element => element.Kind == PanelElementKind.SevenSegment && !string.IsNullOrWhiteSpace(element.ObjectId))
            .ToDictionary(element => element.ObjectId, element => element, StringComparer.Ordinal);
        _vfdDotMatrixElementsByObjectId = _panelDocumentModel.Elements
            .Where(element => element.Kind == PanelElementKind.VfdDotMatrix && !string.IsNullOrWhiteSpace(element.ObjectId))
            .ToDictionary(element => element.ObjectId, element => element, StringComparer.Ordinal);
        _visualStateObjectIds = _lampElementsByObjectId.Keys
            .Concat(_reelElementsByObjectId.Keys)
            .Concat(_alphaElementsByObjectId.Keys)
            .Concat(_sevenSegmentElementsByObjectId.Keys)
            .Concat(_vfdDotMatrixElementsByObjectId.Keys)
            .ToHashSet(StringComparer.Ordinal);
    }
}

public sealed record MachineAssetChoice(string DisplayName, object? AssetPath);

public sealed class MachineSurfaceAssignmentRow : INotifyPropertyChanged
{
    private readonly DocumentTabViewModel _owner; private string? _selectedAssetPath;
    public MachineSurfaceAssignmentRow(DocumentTabViewModel owner, string targetId, string displayName, IReadOnlyList<MachineAssetChoice> choices, string? selectedPath) { _owner = owner; TargetId = targetId; DisplayName = displayName; Choices = new(choices); _selectedAssetPath = selectedPath; }
    public event PropertyChangedEventHandler? PropertyChanged;
    public string TargetId { get; } public string DisplayName { get; } public ObservableCollection<MachineAssetChoice> Choices { get; }
    public string? SelectedAssetPath { get => _selectedAssetPath; set { if (_owner.IsRefreshingMachineCompositionChoices || string.Equals(_selectedAssetPath, value, StringComparison.OrdinalIgnoreCase)) return; _selectedAssetPath = value; PropertyChanged?.Invoke(this, new(nameof(SelectedAssetPath))); _owner.SetMachineSurfaceAssignment(TargetId, value); } }
    internal void SynchronizeSelectedAssetPath(string? value, bool forceNotification = false) { if (!string.Equals(_selectedAssetPath, value, StringComparison.OrdinalIgnoreCase)) _selectedAssetPath = value; else if (!forceNotification) return; PropertyChanged?.Invoke(this, new(nameof(SelectedAssetPath))); }
    internal void RefreshChoices(IReadOnlyList<MachineAssetChoice> choices) => DocumentTabViewModel.ReconcileMachineAssetChoices(Choices, choices);
}

public sealed class MachineReelAssignmentRow : INotifyPropertyChanged
{
    private readonly DocumentTabViewModel _owner; private AssetReference? _selectedReelAssetPath; private MachineAssetChoice? _selectedChoice;
    public MachineReelAssignmentRow(DocumentTabViewModel owner, MachineObjectReference reference, IReadOnlyList<MachineAssetChoice> choices, object? selectedId) { _owner = owner; Reference = reference; Choices = new(choices); _selectedReelAssetPath = selectedId switch { AssetReference typed => typed, string path when !string.IsNullOrWhiteSpace(path) => AssetReference.Project(path), _ => null }; _selectedChoice = Choices.FirstOrDefault(choice => Equals(choice.AssetPath, _selectedReelAssetPath)); OpenSelectedAssetCommand = new RelayCommand(() => _owner.OpenMachineAsset(_selectedReelAssetPath), () => CanOpenSelectedAsset); }
    public event PropertyChangedEventHandler? PropertyChanged;
    public MachineObjectReference Reference { get; } public string DisplayName => $"Reel {Reference.Id}"; public ObservableCollection<MachineAssetChoice> Choices { get; }
    public System.Windows.Input.ICommand OpenSelectedAssetCommand { get; }
    public bool CanOpenSelectedAsset => _owner.CanOpenMachineAssetReference(_selectedReelAssetPath);
    public MachineAssetChoice? SelectedChoice { get => _selectedChoice; set { if (ReferenceEquals(_selectedChoice, value)) return; _selectedChoice = value; PropertyChanged?.Invoke(this, new(nameof(SelectedChoice))); SelectedReelAssetPath = value?.AssetPath; } }
    public object? SelectedReelAssetPath { get => _selectedReelAssetPath; set { var reference = value switch { AssetReference typed => typed, string path when !string.IsNullOrWhiteSpace(path) => AssetReference.Project(path), _ => null }; if (_owner.IsRefreshingMachineCompositionChoices || _selectedReelAssetPath == reference) return; _selectedReelAssetPath = reference; var selected = Choices.FirstOrDefault(choice => Equals(choice.AssetPath, reference)); if (!ReferenceEquals(_selectedChoice, selected)) { _selectedChoice = selected; PropertyChanged?.Invoke(this, new(nameof(SelectedChoice))); } PropertyChanged?.Invoke(this, new(nameof(SelectedReelAssetPath))); _owner.SetMachineReelAssignment(Reference, reference); NotifyAssetNavigationChanged(); } }
    internal void SynchronizeSelectedReelAssetPath(AssetReference? value, bool forceNotification = false) { var referenceChanged = _selectedReelAssetPath != value; _selectedReelAssetPath = value; var selected = Choices.FirstOrDefault(choice => Equals(choice.AssetPath, value)); var choiceChanged = !ReferenceEquals(_selectedChoice, selected); _selectedChoice = selected; if (referenceChanged || forceNotification) PropertyChanged?.Invoke(this, new(nameof(SelectedReelAssetPath))); if (choiceChanged || forceNotification) PropertyChanged?.Invoke(this, new(nameof(SelectedChoice))); NotifyAssetNavigationChanged(); }
    internal void RefreshChoices(IReadOnlyList<MachineAssetChoice> choices) => DocumentTabViewModel.ReconcileMachineAssetChoices(Choices, choices);
    internal void NotifyAssetNavigationChanged() { PropertyChanged?.Invoke(this, new(nameof(CanOpenSelectedAsset))); if (OpenSelectedAssetCommand is RelayCommand command) command.RaiseCanExecuteChanged(); }
}

public sealed class MachineObjectInstanceRow : INotifyPropertyChanged
{
    private readonly DocumentTabViewModel _owner;
    private MachineObject3DInstance _instance;
    private MachineAssetChoice? _selectedChoice;

    public MachineObjectInstanceRow(DocumentTabViewModel owner, MachineObject3DInstance instance, IReadOnlyList<MachineAssetChoice> choices)
    {
        _owner = owner; _instance = instance; Choices = new(choices);
        _selectedChoice = Choices.FirstOrDefault(choice => Equals(choice.AssetPath, instance.ObjectAsset));
        RemoveCommand = new RelayCommand(() => owner.RemoveMachineObjectInstance(_instance.Id));
        OpenSelectedAssetCommand = new RelayCommand(() => owner.OpenMachineAsset(_instance.ObjectAsset), () => CanOpenSelectedAsset);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public ObservableCollection<MachineAssetChoice> Choices { get; }
    public System.Windows.Input.ICommand RemoveCommand { get; }
    public System.Windows.Input.ICommand OpenSelectedAssetCommand { get; }
    public bool CanOpenSelectedAsset => _owner.CanOpenMachineAssetReference(_instance.ObjectAsset);
    public string Id { get => _instance.Id; set => Update(_instance with { Id = value?.Trim() ?? string.Empty }, "Rename Machine Object3D instance", nameof(Id)); }
    public string DisplayName
    {
        get => _instance.DisplayName;
        set
        {
            if (!string.IsNullOrWhiteSpace(value)) Update(_instance with { DisplayName = value.Trim() }, "Rename Machine Object3D display name", nameof(DisplayName));
            else PropertyChanged?.Invoke(this, new(nameof(DisplayName)));
        }
    }
    public MachineAssetChoice? SelectedChoice
    {
        get => _selectedChoice;
        set
        {
            if (ReferenceEquals(value, _selectedChoice)) return;
            _selectedChoice = value;
            Update(_instance with { ObjectAsset = value?.AssetPath as AssetReference }, "Assign Machine Object3D asset", nameof(SelectedChoice));
            NotifyAssetNavigationChanged();
        }
    }
    public double PositionX { get => _instance.Transform.Position.X; set => SetPosition(_instance.Transform.Position with { X = value }); }
    public double PositionY { get => _instance.Transform.Position.Y; set => SetPosition(_instance.Transform.Position with { Y = value }); }
    public double PositionZ { get => _instance.Transform.Position.Z; set => SetPosition(_instance.Transform.Position with { Z = value }); }
    public double RotationX { get => _instance.Transform.Rotation.X; set => SetRotation(_instance.Transform.Rotation with { X = value }); }
    public double RotationY { get => _instance.Transform.Rotation.Y; set => SetRotation(_instance.Transform.Rotation with { Y = value }); }
    public double RotationZ { get => _instance.Transform.Rotation.Z; set => SetRotation(_instance.Transform.Rotation with { Z = value }); }
    public double ScaleX { get => _instance.Transform.Scale.X; set => SetScale(_instance.Transform.Scale with { X = value }); }
    public double ScaleY { get => _instance.Transform.Scale.Y; set => SetScale(_instance.Transform.Scale with { Y = value }); }
    public double ScaleZ { get => _instance.Transform.Scale.Z; set => SetScale(_instance.Transform.Scale with { Z = value }); }
    private void SetPosition(MachineVector3 value)
    {
        if (Finite(value)) Update(_instance with { Transform = _instance.Transform with { Position = value } }, "Edit Machine Object3D position");
        else NotifyVector(nameof(PositionX), nameof(PositionY), nameof(PositionZ));
    }
    private void SetRotation(MachineVector3 value)
    {
        if (Finite(value)) Update(_instance with { Transform = _instance.Transform with { Rotation = value } }, "Edit Machine Object3D rotation");
        else NotifyVector(nameof(RotationX), nameof(RotationY), nameof(RotationZ));
    }
    private void SetScale(MachineVector3 value)
    {
        if (Finite(value) && value.X > 0 && value.Y > 0 && value.Z > 0) Update(_instance with { Transform = _instance.Transform with { Scale = value } }, "Edit Machine Object3D scale");
        else
        {
            NotifyVector(nameof(ScaleX), nameof(ScaleY), nameof(ScaleZ));
        }
    }
    private void NotifyVector(string x, string y, string z)
    {
        PropertyChanged?.Invoke(this, new(x));
        PropertyChanged?.Invoke(this, new(y));
        PropertyChanged?.Invoke(this, new(z));
    }
    private static bool Finite(MachineVector3 value) => double.IsFinite(value.X) && double.IsFinite(value.Y) && double.IsFinite(value.Z);
    private void Update(MachineObject3DInstance replacement, string description, string? rejectedPropertyName = null)
    {
        if (_owner.UpdateMachineObjectInstance(_instance.Id, replacement, description)) _instance = replacement;
        else if (rejectedPropertyName is not null) PropertyChanged?.Invoke(this, new(rejectedPropertyName));
    }
    internal void NotifyAssetNavigationChanged() { PropertyChanged?.Invoke(this, new(nameof(CanOpenSelectedAsset))); if (OpenSelectedAssetCommand is RelayCommand command) command.RaiseCanExecuteChanged(); }
}

public sealed class MachineAnchorRow : INotifyPropertyChanged
{
    private readonly DocumentTabViewModel _owner;
    private MachineAnchor _anchor;
    public MachineAnchorRow(DocumentTabViewModel owner, MachineAnchor anchor) { _owner = owner; _anchor = anchor; RemoveCommand = new RelayCommand(() => owner.RemoveMachineAnchor(_anchor.Id)); }
    public event PropertyChangedEventHandler? PropertyChanged;
    public System.Windows.Input.ICommand RemoveCommand { get; }
    public string Id { get => _anchor.Id; set => Update(_anchor with { Id = value?.Trim() ?? string.Empty }, "Rename Machine anchor", nameof(Id)); }
    public string DisplayName { get => _anchor.DisplayName; set { if (!string.IsNullOrWhiteSpace(value)) Update(_anchor with { DisplayName = value.Trim() }, "Rename Machine anchor display name", nameof(DisplayName)); else PropertyChanged?.Invoke(this, new(nameof(DisplayName))); } }
    public double PositionX { get => _anchor.Position.X; set => SetPosition(_anchor.Position with { X = value }); }
    public double PositionY { get => _anchor.Position.Y; set => SetPosition(_anchor.Position with { Y = value }); }
    public double PositionZ { get => _anchor.Position.Z; set => SetPosition(_anchor.Position with { Z = value }); }
    public double RotationX { get => _anchor.Rotation.X; set => SetRotation(_anchor.Rotation with { X = value }); }
    public double RotationY { get => _anchor.Rotation.Y; set => SetRotation(_anchor.Rotation with { Y = value }); }
    public double RotationZ { get => _anchor.Rotation.Z; set => SetRotation(_anchor.Rotation with { Z = value }); }
    private void SetPosition(MachineVector3 value) { if (Finite(value)) Update(_anchor with { Position = value }, "Edit Machine anchor position", nameof(PositionX), nameof(PositionY), nameof(PositionZ)); else Notify(nameof(PositionX), nameof(PositionY), nameof(PositionZ)); }
    private void SetRotation(MachineVector3 value) { if (Finite(value)) Update(_anchor with { Rotation = value }, "Edit Machine anchor rotation", nameof(RotationX), nameof(RotationY), nameof(RotationZ)); else Notify(nameof(RotationX), nameof(RotationY), nameof(RotationZ)); }
    private static bool Finite(MachineVector3 value) => double.IsFinite(value.X) && double.IsFinite(value.Y) && double.IsFinite(value.Z);
    private void Update(MachineAnchor replacement, string description, params string[] properties)
    {
        if (_owner.UpdateMachineAnchor(_anchor.Id, replacement, description)) _anchor = replacement;
        Notify(properties);
    }
    private void Notify(params string[] properties) { foreach (var property in properties) PropertyChanged?.Invoke(this, new(property)); }
}

internal readonly record struct LampVisualState(bool IsLampTestOn, double Intensity);
internal readonly record struct ReelVisualState(double Position);
internal readonly record struct SegmentVisualState(int[] CellMasks);
internal readonly record struct VfdDotMatrixVisualState(int[] Dots);

public sealed record PanelVisualStateChangedEvent(
    Guid DocumentId,
    IReadOnlyDictionary<string, object> ValuesByObjectId);

public sealed record FaceVisualStateChangedEvent(
    Guid DocumentId,
    IReadOnlyCollection<string> ObjectIds);

public sealed record FacePreviewChangedEvent(Guid DocumentId);

internal sealed record ArtworkCalibrationMeasurements(
    string? BlackColor,
    string? WhiteColor,
    IReadOnlyDictionary<string, string?> SampleColors);

internal sealed record CalibrationOperationInputCacheEntry(string PrefixFingerprint, SKBitmap Bitmap);
