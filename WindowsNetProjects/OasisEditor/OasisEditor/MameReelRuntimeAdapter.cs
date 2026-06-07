namespace OasisEditor;

public sealed class MameReelRuntimeAdapter : IMameReelRuntimeAdapter
{
    private const int ReelPositionsPerRevolution = 96;
    private readonly object _pendingSync = new();
    private readonly Func<IEnumerable<DocumentTabViewModel>> _documentProvider;
    private readonly Func<FruitMachinePlatformType> _platformProvider;
    private readonly Func<bool> _debugOutputEnabledProvider;
    private readonly Action<string> _infoLogger;
    private readonly Action<Action> _uiDispatch;
    private readonly Dictionary<int, int> _pendingReelValues = new();
    private readonly Dictionary<int, int> _latestReelValues = new();
    private readonly Dictionary<Guid, ReelDocumentMappingCacheEntry> _reelMappingsByDocumentId = new();
    private readonly IMachineObjectReferenceResolver _machineObjectReferenceResolver;
    private bool _uiUpdateScheduled;

    public MameReelRuntimeAdapter(
        Func<IEnumerable<DocumentTabViewModel>> documentProvider,
        Func<FruitMachinePlatformType> platformProvider,
        Func<bool> debugOutputEnabledProvider,
        Action<string> infoLogger,
        Action<Action> uiDispatch)
    {
        _documentProvider = documentProvider ?? throw new ArgumentNullException(nameof(documentProvider));
        _platformProvider = platformProvider ?? throw new ArgumentNullException(nameof(platformProvider));
        _debugOutputEnabledProvider = debugOutputEnabledProvider ?? throw new ArgumentNullException(nameof(debugOutputEnabledProvider));
        _infoLogger = infoLogger ?? throw new ArgumentNullException(nameof(infoLogger));
        _uiDispatch = uiDispatch ?? throw new ArgumentNullException(nameof(uiDispatch));
        _machineObjectReferenceResolver = MachineObjectReferenceResolver.Instance;
    }

    public void ApplyReelState(int reelId, int reelValue)
    {
        lock (_pendingSync)
        {
            _pendingReelValues[reelId] = reelValue;
            _latestReelValues[reelId] = reelValue;
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
            var faceObjectIdsByReel = GetFaceReelMapping(document);
            var changedObjectIds = new HashSet<string>(StringComparer.Ordinal);
            var changedFaceObjectIds = new HashSet<string>(StringComparer.Ordinal);

            foreach (var (reelId, reelValue) in snapshot)
            {
                var reelReference = MachineObjectReference.Reel(reelId);
                var machinePosition = ResolveMachineReelPosition(reelValue);
                if (document.RuntimeState.SetReelPositionIfChanged(reelReference, machinePosition)
                    && faceObjectIdsByReel.TryGetValue(reelReference, out var matchingFaceObjectIds))
                {
                    foreach (var faceObjectId in matchingFaceObjectIds)
                    {
                        changedFaceObjectIds.Add(faceObjectId);
                    }
                }

                if (!objectIdsByReel.TryGetValue(reelReference, out var objectIds) || objectIds.Length == 0)
                {
                    continue;
                }

                foreach (var objectId in objectIds)
                {
                    if (!TryResolveEffectiveReelPosition(document, objectId, reelValue, out var effectiveReelPosition, out var stops, out var normalizedPosition))
                    {
                        continue;
                    }

                    if (_debugOutputEnabledProvider())
                    {
                        _infoLogger($"[MAME-REEL] reel{reelId} raw={reelValue} effective={effectiveReelPosition:0.###} normalized={normalizedPosition:0.###} stops={stops} objectId={objectId}");
                    }

                    if (document.RuntimeState.SetReelPositionIfChanged(objectId, effectiveReelPosition))
                    {
                        changedObjectIds.Add(objectId);
                    }
                }
            }

            if (changedObjectIds.Count > 0)
            {
                document.NotifyPanelVisualPreviewChanged(changedObjectIds);
            }

            if (changedFaceObjectIds.Count > 0)
            {
                document.NotifyFaceVisualPreviewChanged(changedFaceObjectIds);
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

    private static IReadOnlyDictionary<MachineObjectReference, string[]> GetFaceReelMapping(DocumentTabViewModel document)
    {
        return document.GetFaceElements()
            .OfType<FaceReelDisplayElement>()
            .Select(element => new
            {
                Element = element,
                Reference = element.LinkedMachineObjectReference ?? MachineObjectReference.Empty
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Element.ObjectId)
                && item.Reference.Kind == MachineObjectKind.Reel
                && !item.Reference.IsEmpty)
            .GroupBy(item => item.Reference, item => item.Element)
            .ToDictionary(group => group.Key, group => group.Select(element => element.ObjectId).Distinct(StringComparer.Ordinal).ToArray());
    }

    private IReadOnlyDictionary<MachineObjectReference, string[]> GetOrBuildReelMapping(DocumentTabViewModel document)
    {
        if (_reelMappingsByDocumentId.TryGetValue(document.DocumentId, out var cacheEntry) && !cacheEntry.IsDirty)
        {
            return cacheEntry.MappingByReelId;
        }

        var mapping = document.GetPanelElements()
            .Where(element => element.Kind == PanelElementKind.Reel
                && !string.IsNullOrWhiteSpace(element.ObjectId)
                && _machineObjectReferenceResolver.TryGetReference(element, out _))
            .GroupBy(element =>
            {
                _machineObjectReferenceResolver.TryGetReference(element, out var reference);
                return reference;
            })
            .ToDictionary(g => g.Key, g => g.Select(x => x.ObjectId).Distinct(StringComparer.Ordinal).ToArray());

        if (cacheEntry is null)
        {
            cacheEntry = new ReelDocumentMappingCacheEntry(document, mapping, OnDocumentPanelChanged);
            _reelMappingsByDocumentId[document.DocumentId] = cacheEntry;
            return cacheEntry.MappingByReelId;
        }

        cacheEntry.Replace(mapping);
        return cacheEntry.MappingByReelId;
    }

    private static double ResolveMachineReelPosition(int rawReelValue)
    {
        return ((rawReelValue % ReelPositionsPerRevolution) + ReelPositionsPerRevolution) % ReelPositionsPerRevolution;
    }

    private bool TryResolveEffectiveReelPosition(DocumentTabViewModel document, string objectId, int rawReelValue, out double effectiveReelPosition, out int stops, out double normalizedPosition)
    {
        effectiveReelPosition = 0d;
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
        effectiveReelPosition = ResolveEffectiveReelPosition(rawReelValue, reelElement.Stops.Value, reelElement.IsReversed == true, reelElement.BandOffset ?? 0d);
        var wrappedPosition = ((effectiveReelPosition % ReelPositionsPerRevolution) + ReelPositionsPerRevolution) % ReelPositionsPerRevolution;
        normalizedPosition = wrappedPosition / ReelPositionsPerRevolution;
        return true;
    }

    private double ResolveEffectiveReelPosition(int rawReelValue, int stops, bool reelReversed, double reelBandOffset)
    {
        var wrapped = ((rawReelValue % ReelPositionsPerRevolution) + ReelPositionsPerRevolution) % ReelPositionsPerRevolution;
        var platformReversed = RequiresPlatformReversal(_platformProvider());
        var shouldReverse = platformReversed ^ reelReversed;
        var directionAdjusted = shouldReverse && wrapped != 0
            ? ReelPositionsPerRevolution - wrapped
            : wrapped;
        var platformOffset = ResolvePlatformBandOffsetNormalized(_platformProvider(), stops);
        var totalOffset = platformOffset + reelBandOffset;
        var offsetSteps = totalOffset * ReelPositionsPerRevolution;
        var offsetAdjusted = directionAdjusted + offsetSteps;
        return ((offsetAdjusted % ReelPositionsPerRevolution) + ReelPositionsPerRevolution) % ReelPositionsPerRevolution;
    }

    private static bool RequiresPlatformReversal(FruitMachinePlatformType platform)
    {
        return platform == FruitMachinePlatformType.MPU4;
    }

    internal static double ResolvePlatformBandOffsetNormalized(FruitMachinePlatformType platform, int stops)
    {
        return platform switch
        {
            FruitMachinePlatformType.MPU4 when stops == 16 => -0.05d,
            FruitMachinePlatformType.Impact when stops == 12 => -0.025d,
            FruitMachinePlatformType.Impact when stops == 16 => -0.08d,
            FruitMachinePlatformType.Scorpion4 when stops == 12 => 0.2d,
            FruitMachinePlatformType.Scorpion4 when stops == 16 => 0.671d,
            _ => 0d
        };
    }

    private void OnDocumentPanelChanged()
    {
        lock (_pendingSync)
        {
            foreach (var (reelId, reelValue) in _latestReelValues)
            {
                _pendingReelValues[reelId] = reelValue;
            }

            if (_uiUpdateScheduled || _pendingReelValues.Count == 0)
            {
                return;
            }

            _uiUpdateScheduled = true;
        }

        _uiDispatch(ApplyPendingOnUiThread);
    }

    private sealed class ReelDocumentMappingCacheEntry
    {
        private readonly DocumentTabViewModel _document;
        private readonly Action _panelChangedCallback;
        public ReelDocumentMappingCacheEntry(DocumentTabViewModel document, IReadOnlyDictionary<MachineObjectReference, string[]> mappingByReelId, Action panelChangedCallback)
        {
            _document = document;
            _panelChangedCallback = panelChangedCallback;
            MappingByReelId = mappingByReelId;
            _document.PanelChanged += OnPanelChanged;
        }

        public IReadOnlyDictionary<MachineObjectReference, string[]> MappingByReelId { get; private set; }
        public bool IsDirty { get; private set; }
        public void Replace(IReadOnlyDictionary<MachineObjectReference, string[]> mappingByReelId) { MappingByReelId = mappingByReelId; IsDirty = false; }
        public void OnPanelChanged(PanelChangeEvent _)
        {
            IsDirty = true;
            _panelChangedCallback();
        }
        public void Detach() => _document.PanelChanged -= OnPanelChanged;
    }
}
