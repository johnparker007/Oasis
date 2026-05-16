namespace OasisEditor;

public sealed class MameSegmentRuntimeAdapter : IMameSegmentRuntimeAdapter
{
    private readonly object _pendingSync = new();
    private readonly Func<IEnumerable<DocumentTabViewModel>> _documentProvider;
    private readonly Action<Action> _uiDispatch;
    private readonly Dictionary<int, int> _pendingMasks = new();
    private readonly Dictionary<int, int> _latestMasksByCell = new();
    private bool _uiUpdateScheduled;

    public MameSegmentRuntimeAdapter(Func<IEnumerable<DocumentTabViewModel>> documentProvider, Action<Action> uiDispatch)
    {
        _documentProvider = documentProvider ?? throw new ArgumentNullException(nameof(documentProvider));
        _uiDispatch = uiDispatch ?? throw new ArgumentNullException(nameof(uiDispatch));
    }

    public void ApplySegmentState(int cellId, int segmentMask)
    {
        lock (_pendingSync)
        {
            _pendingMasks[cellId] = segmentMask;
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
            snapshot = new(_pendingMasks);
            _pendingMasks.Clear();
            _uiUpdateScheduled = false;
        }

        foreach (var document in _documentProvider())
        {
            var changedObjectIds = new HashSet<string>(StringComparer.Ordinal);

            foreach (var (cellId, mask) in snapshot)
            {
                _latestMasksByCell[cellId] = mask;
            }

            foreach (var element in document.GetPanelElements().Where(e => e.Kind == PanelElementKind.Alpha && !string.IsNullOrWhiteSpace(e.ObjectId)))
            {
                var objectId = element.ObjectId!;
                var baseIndex = element.DisplayNumber.GetValueOrDefault(0);
                var cellMasks = new int[16];
                for (var i = 0; i < cellMasks.Length; i++)
                {
                    if (_latestMasksByCell.TryGetValue(baseIndex + i, out var mask))
                    {
                        cellMasks[i] = mask;
                    }
                }

                if (document.RuntimeState.SetSegmentCellMasksIfChanged(objectId, cellMasks))
                {
                    changedObjectIds.Add(objectId);
                }
            }

            if (changedObjectIds.Count > 0)
            {
                document.NotifyPanelVisualPreviewChanged(changedObjectIds);
            }
        }
    }
}
