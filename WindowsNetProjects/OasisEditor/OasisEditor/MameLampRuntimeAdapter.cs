namespace OasisEditor;

public sealed class MameLampRuntimeAdapter : IMameLampRuntimeAdapter
{
    private readonly object _pendingSync = new();
    private readonly Func<IEnumerable<DocumentTabViewModel>> _documentProvider;
    private readonly Func<bool> _debugOutputEnabledProvider;
    private readonly Action<string> _infoLogger;
    private readonly Action<Action> _uiDispatch;
    private readonly Dictionary<int, int> _pendingLampValues = new();
    private readonly Dictionary<Guid, LampDocumentMappingCacheEntry> _lampMappingsByDocumentId = new();
    private bool _uiUpdateScheduled;

    public MameLampRuntimeAdapter(
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

    public void ApplyLampState(int lampId, int lampValue)
    {
        lock (_pendingSync)
        {
            _pendingLampValues[lampId] = lampValue;
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
            snapshot = new Dictionary<int, int>(_pendingLampValues);
            _pendingLampValues.Clear();
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
            var matchingObjectIdsByLamp = GetOrBuildLampMapping(document);

            var hasAnyApplied = false;
            var changedObjectIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (pendingLampId, pendingLampValue) in snapshot)
            {
                if (!matchingObjectIdsByLamp.TryGetValue(pendingLampId, out var matchingObjectIds)
                    || matchingObjectIds.Length == 0)
                {
                    continue;
                }

                var normalizedIntensity = pendingLampValue switch
                {
                    <= 0 => 0d,
                    <= 1 => 1d,
                    _ => Math.Clamp(pendingLampValue / 255d, 0d, 1d)
                };
                if (_debugOutputEnabledProvider())
                {
                    _infoLogger($"[MAME-LAMP] lamp{pendingLampId} value={pendingLampValue} intensity={normalizedIntensity:0.###}");
                }

                foreach (var objectId in matchingObjectIds)
                {
                    if (document.RuntimeState.SetLampIntensityIfChanged(objectId, normalizedIntensity))
                    {
                        hasAnyApplied = true;
                        changedObjectIds.Add(objectId);
                    }
                }
            }

            if (hasAnyApplied)
            {
                document.NotifyPanelVisualPreviewChanged(changedObjectIds);
            }
        }
    }

    private void PruneDocumentCaches(IReadOnlyCollection<DocumentTabViewModel> documents)
    {
        var activeIds = documents.Select(document => document.DocumentId).ToHashSet();
        var staleDocumentIds = _lampMappingsByDocumentId.Keys
            .Where(documentId => !activeIds.Contains(documentId))
            .ToArray();
        foreach (var staleDocumentId in staleDocumentIds)
        {
            if (_lampMappingsByDocumentId.Remove(staleDocumentId, out var staleEntry))
            {
                staleEntry.Detach();
            }
        }
    }

    private IReadOnlyDictionary<int, string[]> GetOrBuildLampMapping(DocumentTabViewModel document)
    {
        if (_lampMappingsByDocumentId.TryGetValue(document.DocumentId, out var cacheEntry)
            && !cacheEntry.IsDirty)
        {
            return cacheEntry.MappingByLampId;
        }

        var mapping = document
            .GetPanelElements()
            .Where(element => element.Kind == PanelElementKind.Lamp
                && !string.IsNullOrWhiteSpace(element.ObjectId)
                && element.DisplayNumber.HasValue)
            .GroupBy(element => element.DisplayNumber.GetValueOrDefault())
            .ToDictionary(
                group => group.Key,
                group => group.Select(element => element.ObjectId)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray());

        if (cacheEntry is null)
        {
            cacheEntry = new LampDocumentMappingCacheEntry(document, mapping);
            _lampMappingsByDocumentId[document.DocumentId] = cacheEntry;
            return cacheEntry.MappingByLampId;
        }

        cacheEntry.Replace(mapping);
        return cacheEntry.MappingByLampId;
    }

    private sealed class LampDocumentMappingCacheEntry
    {
        private readonly DocumentTabViewModel _document;

        public LampDocumentMappingCacheEntry(DocumentTabViewModel document, IReadOnlyDictionary<int, string[]> mappingByLampId)
        {
            _document = document;
            MappingByLampId = mappingByLampId;
            _document.PanelChanged += OnPanelChanged;
        }

        public IReadOnlyDictionary<int, string[]> MappingByLampId { get; private set; }
        public bool IsDirty { get; private set; }

        public void Replace(IReadOnlyDictionary<int, string[]> mappingByLampId)
        {
            MappingByLampId = mappingByLampId;
            IsDirty = false;
        }

        public void OnPanelChanged(PanelChangeEvent _)
        {
            IsDirty = true;
        }

        public void Detach()
        {
            _document.PanelChanged -= OnPanelChanged;
        }
    }
}
