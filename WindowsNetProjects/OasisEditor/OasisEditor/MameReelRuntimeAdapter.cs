namespace OasisEditor;

public sealed class MameReelRuntimeAdapter : IMameReelRuntimeAdapter
{
    private readonly object _pendingSync = new();
    private readonly Func<IEnumerable<DocumentTabViewModel>> _documentProvider;
    private readonly Func<bool> _debugOutputEnabledProvider;
    private readonly Action<string> _infoLogger;
    private readonly Action<Action> _uiDispatch;
    private readonly Dictionary<int, int> _pendingReelValues = new();
    private readonly Dictionary<Guid, ReelDocumentMappingCacheEntry> _reelMappingsByDocumentId = new();
    private bool _uiUpdateScheduled;

    public MameReelRuntimeAdapter(
        Func<IEnumerable<DocumentTabViewModel>> documentProvider,
        Func<bool> debugOutputEnabledProvider,
        Action<string> infoLogger,
        Action<Action> uiDispatch)
    {
        _documentProvider = documentProvider ?? throw new ArgumentNullException(nameof(documentProvider));
        _debugOutputEnabledProvider = debugOutputEnabledProvider ?? throw new ArgumentNullException(nameof(debugOutputEnabledProvider));
        _infoLogger = infoLogger ?? throw new ArgumentNullException(nameof(infoLogger));
        _uiDispatch = uiDispatch ?? throw new ArgumentNullException(nameof(uiDispatch));
    }

    public void ApplyReelState(int reelId, int reelValue)
    {
        lock (_pendingSync)
        {
            _pendingReelValues[reelId] = reelValue;
            if (_uiUpdateScheduled)
            {
                return;
            }

            _uiUpdateScheduled = true;
        }

        _uiDispatch(ApplyPendingOnUiThread);
    }

    private void ApplyPendingOnUiThread()
    {
        Dictionary<int, int> snapshot;
        lock (_pendingSync)
        {
            snapshot = new Dictionary<int, int>(_pendingReelValues);
            _pendingReelValues.Clear();
            _uiUpdateScheduled = false;
        }

        if (snapshot.Count == 0)
        {
            return;
        }

        var documents = _documentProvider().ToArray();
        PruneDocumentCaches(documents);

        foreach (var document in documents)
        {
            var objectIdsByReel = GetOrBuildReelMapping(document);
            var changedObjectIds = new HashSet<string>(StringComparer.Ordinal);

            foreach (var (reelId, reelValue) in snapshot)
            {
                if (!objectIdsByReel.TryGetValue(reelId, out var objectIds) || objectIds.Length == 0)
                {
                    continue;
                }

                foreach (var objectId in objectIds)
                {
                    if (_debugOutputEnabledProvider() && TryGetReelDetails(document, objectId, reelValue, out var stops, out var normalizedPosition))
                    {
                        _infoLogger($"[MAME-REEL] reel{reelId} raw={reelValue} normalized={normalizedPosition:0.###} stops={stops} objectId={objectId}");
                    }

                    if (document.RuntimeState.SetReelPositionIfChanged(objectId, reelValue))
                    {
                        changedObjectIds.Add(objectId);
                    }
                }
            }

            if (changedObjectIds.Count > 0)
            {
                document.NotifyPanelVisualPreviewChanged(changedObjectIds);
            }
        }
    }

    private void PruneDocumentCaches(IReadOnlyCollection<DocumentTabViewModel> documents)
    {
        var activeIds = documents.Select(document => document.DocumentId).ToHashSet();
        foreach (var staleDocumentId in _reelMappingsByDocumentId.Keys.Where(id => !activeIds.Contains(id)).ToArray())
        {
            if (_reelMappingsByDocumentId.Remove(staleDocumentId, out var staleEntry))
            {
                staleEntry.Detach();
            }
        }
    }

    private IReadOnlyDictionary<int, string[]> GetOrBuildReelMapping(DocumentTabViewModel document)
    {
        if (_reelMappingsByDocumentId.TryGetValue(document.DocumentId, out var cacheEntry) && !cacheEntry.IsDirty)
        {
            return cacheEntry.MappingByReelId;
        }

        var mapping = document.GetPanelElements()
            .Where(element => element.Kind == PanelElementKind.Reel && !string.IsNullOrWhiteSpace(element.ObjectId) && element.DisplayNumber.HasValue)
            .GroupBy(element => element.DisplayNumber!.Value)
            .ToDictionary(g => g.Key, g => g.Select(x => x.ObjectId).Distinct(StringComparer.Ordinal).ToArray());

        if (cacheEntry is null)
        {
            cacheEntry = new ReelDocumentMappingCacheEntry(document, mapping);
            _reelMappingsByDocumentId[document.DocumentId] = cacheEntry;
            return cacheEntry.MappingByReelId;
        }

        cacheEntry.Replace(mapping);
        return cacheEntry.MappingByReelId;
    }

    private static bool TryGetReelDetails(DocumentTabViewModel document, string objectId, int rawReelValue, out int stops, out double normalizedPosition)
    {
        stops = 0;
        normalizedPosition = 0d;
        var reelElement = document.GetPanelElements()
            .FirstOrDefault(element => element.Kind == PanelElementKind.Reel
                && string.Equals(element.ObjectId, objectId, StringComparison.Ordinal));
        if (reelElement is null || reelElement.Stops is null or <= 0)
        {
            return false;
        }

        stops = reelElement.Stops.Value;
        var wrappedPosition = ((rawReelValue % stops) + stops) % stops;
        normalizedPosition = wrappedPosition / (double)stops;
        return true;
    }

    private sealed class ReelDocumentMappingCacheEntry
    {
        private readonly DocumentTabViewModel _document;
        public ReelDocumentMappingCacheEntry(DocumentTabViewModel document, IReadOnlyDictionary<int, string[]> mappingByReelId)
        {
            _document = document;
            MappingByReelId = mappingByReelId;
            _document.PanelChanged += OnPanelChanged;
        }

        public IReadOnlyDictionary<int, string[]> MappingByReelId { get; private set; }
        public bool IsDirty { get; private set; }
        public void Replace(IReadOnlyDictionary<int, string[]> mappingByReelId) { MappingByReelId = mappingByReelId; IsDirty = false; }
        public void OnPanelChanged(PanelChangeEvent _) => IsDirty = true;
        public void Detach() => _document.PanelChanged -= OnPanelChanged;
    }
}
