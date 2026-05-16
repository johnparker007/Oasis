namespace OasisEditor;

public sealed class MameSegmentRuntimeAdapter : IMameSegmentRuntimeAdapter
{
    private static readonly int[] LegacyMameBitToWpfSegmentIndexMap =
    [
        2,  // 0: top-left bar
        3,  // 1: top-right bar
        1,  // 2: right-top bar
        4,  // 3: right-bottom bar
        7,  // 4: bottom-right bar
        6,  // 5: bottom-left bar
        5,  // 6: left-bottom bar
        0,  // 7: left-top bar
        11, // 8: horizontal-middle-left bar
        12, // 9: horizontal-middle-right bar
        15, // 10: vertical-middle-top bar
        8,  // 11: vertical-middle-bottom bar
        9,  // 12: diagonal-left-bottom bar
        14, // 13: diagonal-left-top bar
        13, // 14: diagonal-right-top bar
        10  // 15: diagonal-right-bottom bar
    ];
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
                        cellMasks[i] = RemapMameMaskToWpfSegmentOrder(mask);
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

    private static int RemapMameMaskToWpfSegmentOrder(int rawMask)
    {
        var normalizedMask = rawMask & 0xFFFF;
        var remappedMask = 0;

        for (var bit = 0; bit < LegacyMameBitToWpfSegmentIndexMap.Length; bit++)
        {
            if ((normalizedMask & (1 << bit)) == 0)
            {
                continue;
            }

            remappedMask |= 1 << LegacyMameBitToWpfSegmentIndexMap[bit];
        }

        return remappedMask;
    }
}
