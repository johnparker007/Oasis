using System.ComponentModel;
using System.IO;
using System.Linq;
using OasisEditor.Commands;
using OasisEditor.Features.CabinetEditor.Models;
using OasisEditor.Features.CabinetEditor.Services;
using OasisEditor.Features.CabinetEditor.ViewModels;
using SkiaSharp;

namespace OasisEditor;

public sealed class DocumentTabViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly CommandService _commandService;
    private EditorDocument _document;
    private string? _panelLayoutJson;
    private string? _faceDocumentJson;
    private string? _cabinetDocumentJson;
    private CabinetDocument _cabinetDocumentModel;
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
    private Func<IReadOnlyList<DocumentTabViewModel>>? _openDocumentsAccessor;
    private Func<EditorProject?>? _projectAccessor;
    private readonly FaceWorkspaceViewModel? _faceWorkspace;
    private readonly FaceRuntimeAssetsConfigurationService _runtimeAssetsConfiguration = new();
    private SKBitmap? _correctionInputBitmap;
    private string? _correctionInputCacheKey;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action<PanelChangeEvent>? PanelChanged;
    public event Action<PanelVisualStateChangedEvent>? PanelVisualStateChanged;
    public event Action<FaceVisualStateChangedEvent>? FaceVisualStateChanged;
    public event EventHandler<DocumentSelectionChangedEventArgs>? SelectionChanged;

    public DocumentTabViewModel(
        EditorDocument document,
        string? panelLayoutJson = null,
        Guid? documentId = null,
        CommandService? commandService = null,
        MachineRuntimeState? runtimeState = null,
        string? faceDocumentJson = null,
        string? cabinetDocumentJson = null)
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
                AssignedCabinetFaceTargetId = _faceDocumentModel.AssignedCabinetFaceTargetId,
                AssignedCabinetAssetPath = _faceDocumentModel.AssignedCabinetAssetPath,
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
        RebuildLampCaches();
        _faceWorkspace = document.DocumentType == EditorDocumentType.Face ? new FaceWorkspaceViewModel(this) : null;
    }

    public EditorDocument Document => _document;
    public Guid DocumentId { get; }
    public CommandService CommandService => _commandService;
    public MachineRuntimeState RuntimeState => _runtimeState;
    public DocumentSelectionState SelectionState { get; } = new();
    public FaceWorkspaceViewModel? FaceWorkspace => _faceWorkspace;
    public string Title => Document.IsDirty ? $"{Document.Title}*" : Document.Title;
    public string TypeLabel => Document.DocumentType switch
    {
        EditorDocumentType.ProjectOverview => "Project",
        EditorDocumentType.Panel2D => "Panel 2D",
        EditorDocumentType.Cabinet3D => "Cabinet 3D",
        EditorDocumentType.Machine => "Machine",
        EditorDocumentType.Face => "Face",
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
        var viewer = new CabinetModelDocumentViewModel(new SharpGltfWpfModelLoader(), this, _openDocumentsAccessor, _projectAccessor);
        _cabinetViewer = viewer;
        viewer.Initialize();
        return viewer;
    }

    public void SetOpenDocumentsAccessor(Func<IReadOnlyList<DocumentTabViewModel>> openDocumentsAccessor)
    {
        _openDocumentsAccessor = openDocumentsAccessor;
        ReconcileRuntimeAssetsConfiguration();
    }

    public void SetProjectAccessor(Func<EditorProject?> projectAccessor)
    {
        _projectAccessor = projectAccessor;
        ReconcileRuntimeAssetsConfiguration();
        _cabinetViewer?.ReflectionEditor.RefreshProjectContext();
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

    public string GetCabinetDocumentJson()
    {
        return CabinetDocumentStorage.Serialize(_cabinetDocumentModel);
    }

    internal void SetCabinetDocument(CabinetDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        _cabinetDocumentModel = document;
        _cabinetDocumentJson = GetCabinetDocumentJson();
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
        SelectionState.SelectionChanged -= OnSelectionStateChanged;
    }

    public string? FaceDocumentJson
    {
        get => _faceDocumentJson;
        set
        {
            if (string.Equals(_faceDocumentJson, value, StringComparison.Ordinal))
            {
                return;
            }

            _faceDocumentJson = value;
            _faceDocumentModel = FaceDocumentStorage.TryRead(value, out var faceDocumentFile)
                ? FaceDocumentStorage.ToModel(faceDocumentFile)
                : new FaceDocumentModel();
            _faceWorkspace?.RefreshSummaries();
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FaceDocumentJson)));
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

    public FaceBuildResult BuildFace(bool force = false)
    {
        ReconcileRuntimeAssetsConfiguration();
        var service = new FaceBuildService();
        var executors = new Dictionary<FaceGeneratedProduct, Func<FaceBuildNodeResult>>
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
        var result = service.Build(_faceDocumentModel.BuildState, executors, force);
        _faceDocumentJson = GetFaceDocumentJson();
        PersistBuildStateWhenDocumentIsClean(result);
        _faceWorkspace?.RefreshSummaries();
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FaceDocumentJson)));
        return result;
    }

    private FaceBuildNodeResult BuildLampMask()
    {
        var project = _projectAccessor?.Invoke();
        if (project is null)
        {
            return new(FaceGeneratedProduct.LampMask, false, "The source Panel2D mask cannot be rebuilt because no project is open.");
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
        var width = _faceDocumentModel.Artwork?.OutputWidth ?? _faceDocumentModel.MaskLayer?.Width ?? 0;
        var height = _faceDocumentModel.Artwork?.OutputHeight ?? _faceDocumentModel.MaskLayer?.Height ?? 0;
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
            maskPath, _faceDocumentModel.GenerationSettings.MaskExtractionThreshold);
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
            panel = Panel2DDocumentStorage.DeserializeModel(File.ReadAllText(fullPath));
            return true;
        }
        error = $"The source Panel2D '{sourcePath ?? sourceId}' is unavailable. Open it or restore the linked file, then retry.";
        return false;
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
        if (!capability.IsConfigured || capability.CabinetContext is null)
        {
            return new(FaceGeneratedProduct.RuntimeAssets, false,
                capability.Reason ?? "Standalone Face runtime assets are not configured.");
        }
        var exported = new FaceRuntimeExportService().Export(
            _faceDocumentModel, project, capability.CabinetContext, FilePath);
        _faceDocumentModel = exported.Document;
        return new(FaceGeneratedProduct.RuntimeAssets, true);
    }

    internal void ReconcileRuntimeAssetsConfiguration()
    {
        if (Document.DocumentType != EditorDocumentType.Face) return;
        var capability = _runtimeAssetsConfiguration.Evaluate(
            _faceDocumentModel, _projectAccessor?.Invoke(), _openDocumentsAccessor?.Invoke() ?? []);
        _runtimeAssetsConfiguration.Reconcile(_faceDocumentModel, capability);
        _faceDocumentJson = GetFaceDocumentJson();
        _faceWorkspace?.RefreshSummaries();
    }

    internal void InvalidateFaceBuild(FaceBuildInput input)
    {
        new FaceBuildService().Invalidate(_faceDocumentModel.BuildState, input);
        if (input is FaceBuildInput.ArtworkSource or FaceBuildInput.ArtworkPreprocessing)
            InvalidateCorrectionInputCache();
        _faceDocumentJson = GetFaceDocumentJson();
        _faceWorkspace?.RefreshSummaries();
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FaceDocumentJson)));
    }

    private FaceBuildNodeResult BuildArtworkCorrectionInput()
    {
        var artwork = _faceDocumentModel.Artwork;
        var project = _projectAccessor?.Invoke();
        if (artwork is null || project is null) return new(FaceGeneratedProduct.ArtworkCorrectionInput, false, "Artwork or project is unavailable.");
        if (!TryResolveSourcePanel(out var panel, out var error)) return new(FaceGeneratedProduct.ArtworkCorrectionInput, false, error);
        var shape = panel.FaceSourceShapes.FirstOrDefault(candidate => string.Equals(candidate.Id, artwork.Source.FaceSourceShapeId ?? _faceDocumentModel.SourceFaceShapeId, StringComparison.Ordinal));
        if (shape is null) return new(FaceGeneratedProduct.ArtworkCorrectionInput, false, "The linked Face Source Shape is unavailable.");
        if (string.IsNullOrWhiteSpace(artwork.CorrectionInputAssetPath)) return new(FaceGeneratedProduct.ArtworkCorrectionInput, false, "The correction-input path is not configured.");
        var built = new FaceArtworkRebuildService().RebuildCorrectionInput(artwork, panel, shape,
            project.ProjectDirectory, artwork.CorrectionInputAssetPath, _faceDocumentModel.GenerationSettings);
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
        Views.SkiaFaceEditView.InvalidateArtworkImage(artwork.OutputAssetPath);
        PanelChanged?.Invoke(new PanelChangeEvent(DocumentId, artwork.Id, PanelChangeProperties.Metadata, AffectsCanvas: true, AffectsHierarchy: false, AffectsInspectorRows: false, AffectsPersistence: false));
    }

    internal bool TryGetArtworkReferenceColors(ArtworkCalibrationOperationModel operation, out string? blackColor, out string? whiteColor)
    {
        blackColor = operation.BlackReference.ManualEnabled ? operation.BlackReference.ManualColor : null;
        whiteColor = operation.WhiteReference.ManualEnabled ? operation.WhiteReference.ManualColor : null;
        var artwork = _faceDocumentModel.Artwork;
        var project = _projectAccessor?.Invoke();
        if (artwork is null || project is null || !TryGetCorrectionInputBitmap(out var original)) return false;
        var index = artwork.ProcessingPipeline.Operations.ToList().FindIndex(o => o.Id == operation.Id);
        if (index <= 0)
            return FaceArtworkProcessingPipeline.TryResolveReferenceColors(original, operation, out blackColor, out whiteColor);
        using var input = new FaceArtworkProcessingPipeline().Evaluate(original, artwork.ProcessingPipeline, index);
        return FaceArtworkProcessingPipeline.TryResolveReferenceColors(input, operation, out blackColor, out whiteColor);
    }

    internal string? GetArtworkSampleColor(ArtworkCalibrationOperationModel operation, CalibrationSampleModel sample)
    {
        var artwork = _faceDocumentModel.Artwork;
        var project = _projectAccessor?.Invoke();
        if (artwork is null || project is null || !TryGetCorrectionInputBitmap(out var original)) return null;
        var index = artwork.ProcessingPipeline.Operations.ToList().FindIndex(o => o.Id == operation.Id);
        if (index <= 0) return FaceArtworkProcessingPipeline.MeasureSampleHex(original, sample);
        using var input = new FaceArtworkProcessingPipeline().Evaluate(original, artwork.ProcessingPipeline, index);
        return FaceArtworkProcessingPipeline.MeasureSampleHex(input, sample);
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
        _correctionInputBitmap?.Dispose();
        _correctionInputBitmap = null;
        _correctionInputCacheKey = null;
    }

    public string GetFaceDocumentJson()
    {
        return FaceDocumentStorage.Serialize(_faceDocumentModel);
    }

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

    internal void SetFaceDocument(FaceDocumentModel model, PanelChangeEvent? faceChange = null, bool updateSerializedDocument = true)
    {
        ArgumentNullException.ThrowIfNull(model);

        _faceDocumentModel = model;
        _faceWorkspace?.RefreshSummaries();
        if (updateSerializedDocument)
        {
            _faceDocumentJson = GetFaceDocumentJson();
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FaceDocumentJson)));
            ReconcileSelection();
        }

        if (faceChange is PanelChangeEvent change)
        {
            PanelChanged?.Invoke(change);
        }
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
            AssignedCabinetFaceTargetId = _faceDocumentModel.AssignedCabinetFaceTargetId,
                AssignedCabinetAssetPath = _faceDocumentModel.AssignedCabinetAssetPath,
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
