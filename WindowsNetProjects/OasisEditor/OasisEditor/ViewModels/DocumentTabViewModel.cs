using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using OasisEditor.Commands;
using OasisEditor.Features.CabinetEditor.Models;
using OasisEditor.Features.CabinetEditor.Services;
using OasisEditor.Features.CabinetEditor.ViewModels;
using OasisEditor.Progress;
using SkiaSharp;

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
    private readonly Dictionary<string, CalibrationOperationInputCacheEntry> _calibrationOperationInputs = new(StringComparer.Ordinal);
    private IProgressDialogService _progressDialogService = NoOpProgressDialogService.Instance;
    private bool _isDetachedFaceBuildWorker;

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
    internal IProgressDialogService ProgressDialogService => _progressDialogService;
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
        double? x = null, double? y = null, double? width = null, double? height = null) => new()
    {
        Enabled=enabled ?? value.Enabled, AssetPath=value.AssetPath, PixelWidth=value.PixelWidth, PixelHeight=value.PixelHeight,
        PerspectiveRegistration=perspectiveRegistration ?? value.PerspectiveRegistration,
        X=x ?? value.X, Y=y ?? value.Y, Width=width ?? value.Width, Height=height ?? value.Height,
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
        string? CabinetDocumentJson);

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
                document.CabinetDocumentJson))
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
            snapshot.Document,
            snapshot.PanelLayoutJson,
            faceDocumentJson: snapshot.FaceDocumentJson,
            cabinetDocumentJson: snapshot.CabinetDocumentJson)).ToArray();
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
                _isDetachedFaceBuildWorker = true
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

public sealed record FacePreviewChangedEvent(Guid DocumentId);

internal sealed record ArtworkCalibrationMeasurements(
    string? BlackColor,
    string? WhiteColor,
    IReadOnlyDictionary<string, string?> SampleColors);

internal sealed record CalibrationOperationInputCacheEntry(string PrefixFingerprint, SKBitmap Bitmap);
