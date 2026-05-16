namespace OasisEditor;

public sealed class MameSegmentRuntimeAdapter : IMameSegmentRuntimeAdapter
{
    private static readonly int[] Led16SegBitToWpfSegmentIndexMap = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15];
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

        if ((normalizedMask & 0xC000) == 0)
        {
            return ExpandLed14SegIntoSixteenSegmentMask(normalizedMask);
        }

        var remappedMask = 0;

        for (var bit = 0; bit < Led16SegBitToWpfSegmentIndexMap.Length; bit++)
        {
            if ((normalizedMask & (1 << bit)) == 0)
            {
                continue;
            }

            remappedMask |= 1 << Led16SegBitToWpfSegmentIndexMap[bit];
        }

        return remappedMask;
    }

    private static int ExpandLed14SegIntoSixteenSegmentMask(int led14Mask)
    {
        var expandedMask = 0;

        // led14seg/led14segsc ordering from legacy Unity renderer:
        // 0 -> both top split segments
        // 3 -> both bottom split segments
        if ((led14Mask & (1 << 0)) != 0)
        {
            expandedMask |= (1 << 0) | (1 << 1);
        }

        if ((led14Mask & (1 << 1)) != 0) expandedMask |= 1 << 2;
        if ((led14Mask & (1 << 2)) != 0) expandedMask |= 1 << 3;

        if ((led14Mask & (1 << 3)) != 0)
        {
            expandedMask |= (1 << 4) | (1 << 5);
        }

        if ((led14Mask & (1 << 4)) != 0) expandedMask |= 1 << 6;
        if ((led14Mask & (1 << 5)) != 0) expandedMask |= 1 << 7;
        if ((led14Mask & (1 << 6)) != 0) expandedMask |= 1 << 8;
        if ((led14Mask & (1 << 7)) != 0) expandedMask |= 1 << 9;
        if ((led14Mask & (1 << 8)) != 0) expandedMask |= 1 << 10;
        if ((led14Mask & (1 << 9)) != 0) expandedMask |= 1 << 11;
        if ((led14Mask & (1 << 10)) != 0) expandedMask |= 1 << 12;
        if ((led14Mask & (1 << 11)) != 0) expandedMask |= 1 << 13;
        if ((led14Mask & (1 << 12)) != 0) expandedMask |= 1 << 14;
        if ((led14Mask & (1 << 13)) != 0) expandedMask |= 1 << 15;

        return expandedMask;
    }
}
