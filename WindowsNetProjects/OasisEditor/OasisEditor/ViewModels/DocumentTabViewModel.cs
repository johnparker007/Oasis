using System.ComponentModel;
using System.Linq;
using OasisEditor.Commands;

namespace OasisEditor;

public sealed class DocumentTabViewModel : INotifyPropertyChanged
{
    private readonly CommandService _commandService;
    private EditorDocument _document;
    private string? _panelLayoutJson;
    private string? _faceDocumentJson;
    private Panel2DDocumentModel _panelDocumentModel;
    private FaceDocumentModel _faceDocumentModel;
    private Dictionary<string, PanelElementModel> _lampElementsByObjectId = new(StringComparer.Ordinal);
    private Dictionary<string, PanelElementModel> _reelElementsByObjectId = new(StringComparer.Ordinal);
    private Dictionary<string, PanelElementModel> _alphaElementsByObjectId = new(StringComparer.Ordinal);
    private Dictionary<string, PanelElementModel> _sevenSegmentElementsByObjectId = new(StringComparer.Ordinal);
    private Dictionary<string, PanelElementModel> _vfdDotMatrixElementsByObjectId = new(StringComparer.Ordinal);
    private HashSet<string> _visualStateObjectIds = new(StringComparer.Ordinal);
    private PanelSelectionInfo? _hierarchySelectedPanelSelection;
    private double _panelZoom = 1.0;
    private double _panelPanX;
    private double _panelPanY;
    private Dictionary<string, object>? _lastVisualStateByObjectId;
    private readonly MachineRuntimeState _runtimeState;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action<PanelChangeEvent>? PanelChanged;
    public event Action<PanelVisualStateChangedEvent>? PanelVisualStateChanged;

    public DocumentTabViewModel(
        EditorDocument document,
        string? panelLayoutJson = null,
        Guid? documentId = null,
        CommandService? commandService = null,
        MachineRuntimeState? runtimeState = null,
        string? faceDocumentJson = null)
    {
        _document = document;
        DocumentId = documentId ?? Guid.NewGuid();
        _commandService = commandService ?? new CommandService(new CommandHistory(), DocumentId);
        _panelLayoutJson = panelLayoutJson;
        _faceDocumentJson = faceDocumentJson;
        _runtimeState = runtimeState ?? new MachineRuntimeState();
        _panelDocumentModel = new Panel2DDocumentModel
        {
            Elements = Panel2DDocumentStorage.DeserializeLayout(panelLayoutJson)
                .Select(Panel2DDocumentStorage.ToModel)
                .ToArray()
        };
        _faceDocumentModel = FaceDocumentStorage.TryRead(faceDocumentJson, out var faceDocumentFile)
            ? FaceDocumentStorage.ToModel(faceDocumentFile)
            : new FaceDocumentModel();
        RebuildLampCaches();
    }

    public EditorDocument Document => _document;
    public Guid DocumentId { get; }
    public CommandService CommandService => _commandService;
    public MachineRuntimeState RuntimeState => _runtimeState;
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
            _panelDocumentModel = new Panel2DDocumentModel
            {
                Elements = Panel2DDocumentStorage.DeserializeLayout(value)
                    .Select(Panel2DDocumentStorage.ToModel)
                    .ToArray()
            };
            RebuildLampCaches();
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PanelLayoutJson)));
        }
    }

    public FaceDocumentModel GetFaceDocument()
    {
        return _faceDocumentModel;
    }

    public string GetFaceDocumentJson()
    {
        return FaceDocumentStorage.Serialize(_faceDocumentModel);
    }

    internal IReadOnlyList<PanelElementModel> GetPanelElements()
    {
        return _panelDocumentModel.Elements;
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
            Elements = elements.ToArray()
        };
        RebuildLampCaches();

        _panelLayoutJson = GetPanelLayoutProjectionJson();
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PanelLayoutJson)));

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
                            ? new VfdDotMatrixVisualState(_runtimeState.GetVfdDotMatrixDots(objectId, MameVfdDotMatrixStateParser.DotCount))
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
        return Panel2DDocumentStorage.SerializeLayout(
            Panel2DDocumentStorage.ToStorageElements(_panelDocumentModel));
    }

    public PanelSelectionInfo? HierarchySelectedPanelSelection
    {
        get => _hierarchySelectedPanelSelection;
        set
        {
            if (_hierarchySelectedPanelSelection == value)
            {
                return;
            }

            _hierarchySelectedPanelSelection = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HierarchySelectedPanelSelection)));
        }
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
