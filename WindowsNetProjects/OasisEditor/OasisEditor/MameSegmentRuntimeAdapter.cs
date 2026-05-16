namespace OasisEditor;

public sealed class MameSegmentRuntimeAdapter : IMameSegmentRuntimeAdapter
{
    // MAME's 16-seg VFD bit order does not match the geometry index order used by
    // `oasis_16_segment_display_definition.json`, so remap incoming masks once here.
    // sourceBit -> targetBit
    private static readonly int[] AlphaSixteenSegmentBitToGeometryIndex =
    [
        2,  // top-left horizontal
        3,  // top-right horizontal
        1,  // upper-right vertical
        4,  // lower-right vertical
        7,  // bottom-right horizontal
        6,  // bottom-left horizontal
        5,  // lower-left vertical
        0,  // upper-left vertical
        11, // middle-left horizontal
        12, // middle-right horizontal
        14, // upper-left diagonal
        13, // upper-right diagonal
        10, // lower-right diagonal
        9,  // lower-left diagonal
        8,  // lower center vertical
        15  // upper center vertical
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
                _latestMasksByCell[cellId] = RemapAlphaSegmentMask(mask);
            }

            foreach (var element in document.GetPanelElements().Where(e => e.Kind == PanelElementKind.Alpha && !string.IsNullOrWhiteSpace(e.ObjectId)))
            {
                var objectId = element.ObjectId!;
                var baseIndex = element.DisplayNumber.GetValueOrDefault(0);
                var isReversed = element.IsReversed == true;
                var cellMasks = new int[16];
                for (var i = 0; i < cellMasks.Length; i++)
                {
                    var mappedCellId = isReversed
                        ? baseIndex + (cellMasks.Length - 1 - i)
                        : baseIndex + i;
                    if (_latestMasksByCell.TryGetValue(mappedCellId, out var mask))
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

    private static int RemapAlphaSegmentMask(int sourceMask)
    {
        var remapped = 0;
        for (var sourceBit = 0; sourceBit < AlphaSixteenSegmentBitToGeometryIndex.Length; sourceBit++)
        {
            if ((sourceMask & (1 << sourceBit)) == 0)
            {
                continue;
            }

            remapped |= 1 << AlphaSixteenSegmentBitToGeometryIndex[sourceBit];
        }

        return remapped;
    }
}
