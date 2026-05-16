using System.ComponentModel;
using System.Linq;
using OasisEditor.Commands;

namespace OasisEditor;

public sealed class DocumentTabViewModel : INotifyPropertyChanged
{
    private readonly CommandService _commandService;
    private EditorDocument _document;
    private string? _panelLayoutJson;
    private Panel2DDocumentModel _panelDocumentModel;
    private Dictionary<string, PanelElementModel> _lampElementsByObjectId = new(StringComparer.Ordinal);
    private Dictionary<string, PanelElementModel> _reelElementsByObjectId = new(StringComparer.Ordinal);
    private HashSet<string> _visualStateObjectIds = new(StringComparer.Ordinal);
    private PanelSelectionInfo? _hierarchySelectedPanelSelection;
    private double _panelZoom = 1.0;
    private double _panelPanX;
    private double _panelPanY;
    private Dictionary<string, object>? _lastVisualStateByObjectId;
    private readonly PanelRuntimeState _runtimeState;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action<PanelChangeEvent>? PanelChanged;
    public event Action<PanelVisualStateChangedEvent>? PanelVisualStateChanged;

    public DocumentTabViewModel(
        EditorDocument document,
        string? panelLayoutJson = null,
        Guid? documentId = null,
        CommandService? commandService = null,
        PanelRuntimeState? runtimeState = null)
    {
        _document = document;
        DocumentId = documentId ?? Guid.NewGuid();
        _commandService = commandService ?? new CommandService(new CommandHistory(), DocumentId);
        _panelLayoutJson = panelLayoutJson;
        _runtimeState = runtimeState ?? new PanelRuntimeState();
        _panelDocumentModel = new Panel2DDocumentModel
        {
            Elements = Panel2DDocumentStorage.DeserializeLayout(panelLayoutJson)
                .Select(Panel2DDocumentStorage.ToModel)
                .ToArray()
        };
        RebuildLampCaches();
    }

    public EditorDocument Document => _document;
    public Guid DocumentId { get; }
    public CommandService CommandService => _commandService;
    public PanelRuntimeState RuntimeState => _runtimeState;
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
                && (element.Kind == PanelElementKind.Lamp || element.Kind == PanelElementKind.Reel))
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
                : new ReelVisualState(_runtimeState.GetReelPosition(objectId));
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
        _visualStateObjectIds = _lampElementsByObjectId.Keys
            .Concat(_reelElementsByObjectId.Keys)
            .ToHashSet(StringComparer.Ordinal);
    }
}

internal readonly record struct LampVisualState(bool IsLampTestOn, double Intensity);
internal readonly record struct ReelVisualState(int Position);

public sealed record PanelVisualStateChangedEvent(
    Guid DocumentId,
    IReadOnlyDictionary<string, object> ValuesByObjectId);
