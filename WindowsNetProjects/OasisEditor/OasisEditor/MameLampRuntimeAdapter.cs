namespace OasisEditor;

public sealed class MameLampRuntimeAdapter : IMameLampRuntimeAdapter
{
    private readonly object _pendingSync = new();
    private readonly Func<IEnumerable<DocumentTabViewModel>> _documentProvider;
    private readonly Func<bool> _debugOutputEnabledProvider;
    private readonly Action<string> _infoLogger;
    private readonly Action<Action> _uiDispatch;
    private readonly Dictionary<int, int> _pendingLampValues = new();
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

        foreach (var document in _documentProvider())
        {
            var matchingObjectIdsByLamp = document
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

            var hasAnyApplied = false;
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
                    document.RuntimeState.SetLampIntensity(objectId, normalizedIntensity);
                    hasAnyApplied = true;
                }
            }

            if (hasAnyApplied)
            {
                document.NotifyPanelVisualPreviewChanged();
            }
        }
    }
}
