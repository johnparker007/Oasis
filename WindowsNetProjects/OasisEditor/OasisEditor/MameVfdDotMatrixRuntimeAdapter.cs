namespace OasisEditor;

public sealed class MameVfdDotMatrixRuntimeAdapter : IMameVfdDotMatrixRuntimeAdapter
{
    private const int DotCount = MameVfdDotMatrixStateParser.DotCount;
    private readonly object _pendingSync = new();
    private readonly Func<IEnumerable<DocumentTabViewModel>> _documentProvider;
    private readonly Action<Action> _uiDispatch;
    private readonly Dictionary<int, int> _pendingDots = new();
    private readonly int[] _latestDots = new int[DotCount];
    private bool _uiUpdateScheduled;

    public MameVfdDotMatrixRuntimeAdapter(Func<IEnumerable<DocumentTabViewModel>> documentProvider, Action<Action> uiDispatch)
    {
        _documentProvider = documentProvider ?? throw new ArgumentNullException(nameof(documentProvider));
        _uiDispatch = uiDispatch ?? throw new ArgumentNullException(nameof(uiDispatch));
    }

    public void ApplyDotState(int dotIndex, int dotValue)
    {
        if (dotIndex is < 0 or >= DotCount)
        {
            return;
        }

        lock (_pendingSync)
        {
            _pendingDots[dotIndex] = dotValue == 1 ? 1 : 0;
            if (_uiUpdateScheduled) return;
            _uiUpdateScheduled = true;
        }

        _uiDispatch(ApplyPendingOnUiThread);
    }

    private void ApplyPendingOnUiThread()
    {
        Dictionary<int, int> snapshot;
        lock (_pendingSync)
        {
            snapshot = new(_pendingDots);
            _pendingDots.Clear();
            _uiUpdateScheduled = false;
        }

        foreach (var (dotIndex, dotValue) in snapshot)
        {
            _latestDots[dotIndex] = dotValue == 1 ? 1 : 0;
        }

        foreach (var document in _documentProvider())
        {
            var changedObjectIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var element in document.GetPanelElements().Where(e => e.Kind == PanelElementKind.VfdDotMatrix && !string.IsNullOrWhiteSpace(e.ObjectId)))
            {
                if (document.RuntimeState.SetVfdDotMatrixDotsIfChanged(element.ObjectId, _latestDots))
                {
                    changedObjectIds.Add(element.ObjectId);
                }
            }

            if (changedObjectIds.Count > 0)
            {
                document.NotifyPanelVisualPreviewChanged(changedObjectIds);
            }
        }
    }
}
