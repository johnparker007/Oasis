namespace OasisEditor;

public sealed class MameSegmentRuntimeAdapter : IMameSegmentRuntimeAdapter
{
    private static readonly int[] Led16SegBitToWpfSegmentIndexMap = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15];
    private readonly object _pendingSync = new();
    private readonly Func<IEnumerable<DocumentTabViewModel>> _documentProvider;
    private readonly Action<Action> _uiDispatch;
    private readonly Dictionary<int, (int Mask, MameSegmentOutputType OutputType)> _pendingMasks = new();
    private readonly Dictionary<int, double> _pendingVfdBrightnessByCell = new();
    private readonly Dictionary<int, int> _latestVfdMasksByCell = new();
    private readonly Dictionary<int, int> _latestDigitMasksByCell = new();
    private readonly Dictionary<int, double> _latestVfdBrightnessByCell = new();
    private bool _uiUpdateScheduled;

    public MameSegmentRuntimeAdapter(Func<IEnumerable<DocumentTabViewModel>> documentProvider, Action<Action> uiDispatch)
    {
        _documentProvider = documentProvider ?? throw new ArgumentNullException(nameof(documentProvider));
        _uiDispatch = uiDispatch ?? throw new ArgumentNullException(nameof(uiDispatch));
    }

    public void ApplySegmentState(int cellId, int segmentMask, MameSegmentOutputType outputType)
    {
        lock (_pendingSync)
        {
            _pendingMasks[cellId] = (segmentMask, outputType);
            if (_uiUpdateScheduled) return;
            _uiUpdateScheduled = true;
        }

        _uiDispatch(ApplyPendingOnUiThread);
    }

    public void ApplyVfdBrightness(int cellId, double normalizedBrightness)
    {
        lock (_pendingSync)
        {
            _pendingVfdBrightnessByCell[cellId] = Math.Clamp(normalizedBrightness, 0d, 1d);
            if (_uiUpdateScheduled) return;
            _uiUpdateScheduled = true;
        }

        _uiDispatch(ApplyPendingOnUiThread);
    }

    private void ApplyPendingOnUiThread()
    {
        Dictionary<int, (int Mask, MameSegmentOutputType OutputType)> snapshot;
        Dictionary<int, double> brightnessSnapshot;
        lock (_pendingSync)
        {
            snapshot = new(_pendingMasks);
            brightnessSnapshot = new(_pendingVfdBrightnessByCell);
            _pendingMasks.Clear();
            _pendingVfdBrightnessByCell.Clear();
            _uiUpdateScheduled = false;
        }

        foreach (var document in _documentProvider())
        {
            var changedObjectIds = new HashSet<string>(StringComparer.Ordinal);

            foreach (var (cellId, state) in snapshot)
            {
                if (state.OutputType == MameSegmentOutputType.Vfd)
                {
                    _latestVfdMasksByCell[cellId] = state.Mask;
                }
                else
                {
                    _latestDigitMasksByCell[cellId] = state.Mask;
                }
            }

            foreach (var (cellId, brightness) in brightnessSnapshot)
            {
                _latestVfdBrightnessByCell[cellId] = brightness;
            }

            foreach (var element in document.GetPanelElements().Where(e => (e.Kind == PanelElementKind.Alpha || e.Kind == PanelElementKind.SevenSegment) && !string.IsNullOrWhiteSpace(e.ObjectId)))
            {
                var objectId = element.ObjectId!;
                var baseIndex = element.DisplayNumber.GetValueOrDefault(0);
                var cellCount = element.Kind == PanelElementKind.SevenSegment ? 1 : 16;
                var cellMasks = new int[cellCount];
                var cellBrightness = new double[cellCount];
                for (var i = 0; i < cellMasks.Length; i++)
                {
                    var source = element.Kind == PanelElementKind.SevenSegment ? _latestDigitMasksByCell : _latestVfdMasksByCell;
                    if (source.TryGetValue(baseIndex + i, out var mask))
                    {
                        cellMasks[i] = element.Kind == PanelElementKind.SevenSegment
                            ? mask
                            : RemapMameMaskToWpfSegmentOrder(mask);
                    }

                    if (element.Kind == PanelElementKind.Alpha && _latestVfdBrightnessByCell.TryGetValue(baseIndex + i, out var brightness))
                    {
                        cellBrightness[i] = brightness;
                    }
                    else
                    {
                        cellBrightness[i] = 1d;
                    }
                }

                var maskChanged = document.RuntimeState.SetSegmentCellMasksIfChanged(objectId, cellMasks);
                var brightnessChanged = element.Kind == PanelElementKind.Alpha
                    && document.RuntimeState.SetSegmentCellBrightnessIfChanged(objectId, cellBrightness);
                if (maskChanged || brightnessChanged)
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
