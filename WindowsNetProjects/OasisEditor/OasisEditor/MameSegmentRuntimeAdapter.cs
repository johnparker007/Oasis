namespace OasisEditor;

public sealed class MameSegmentRuntimeAdapter : IMameSegmentRuntimeAdapter
{
    private readonly object _pendingSync = new();
    private readonly Func<IEnumerable<DocumentTabViewModel>> _documentProvider;
    private readonly Func<FruitMachinePlatformType> _platformProvider;
    private readonly Action<Action> _uiDispatch;
    private readonly IMachineObjectReferenceResolver _machineObjectReferenceResolver;
    private readonly Dictionary<int, (int Mask, MameSegmentOutputType OutputType)> _pendingMasks = new();
    private readonly Dictionary<int, double> _pendingVfdBrightnessByDisplay = new();
    private readonly Dictionary<int, int> _latestVfdMasksByCell = new();
    private readonly Dictionary<int, int> _latestDigitMasksByCell = new();
    private readonly Dictionary<int, double> _latestVfdBrightnessByDisplay = new();
    private bool _uiUpdateScheduled;

    public MameSegmentRuntimeAdapter(
        Func<IEnumerable<DocumentTabViewModel>> documentProvider,
        Action<Action> uiDispatch,
        Func<FruitMachinePlatformType>? platformProvider = null)
    {
        _documentProvider = documentProvider ?? throw new ArgumentNullException(nameof(documentProvider));
        _uiDispatch = uiDispatch ?? throw new ArgumentNullException(nameof(uiDispatch));
        _platformProvider = platformProvider ?? (() => FruitMachinePlatformType.MPU4);
        _machineObjectReferenceResolver = MachineObjectReferenceResolver.Instance;
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
            _pendingVfdBrightnessByDisplay[cellId] = Math.Clamp(normalizedBrightness, 0d, 1d);
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
            brightnessSnapshot = new(_pendingVfdBrightnessByDisplay);
            _pendingMasks.Clear();
            _pendingVfdBrightnessByDisplay.Clear();
            _uiUpdateScheduled = false;
        }

        foreach (var document in _documentProvider())
        {
            var changedObjectIds = new HashSet<string>(StringComparer.Ordinal);
            var changedFaceObjectIds = new HashSet<string>(StringComparer.Ordinal);

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
                _latestVfdBrightnessByDisplay[cellId] = brightness;
            }

            foreach (var element in document.GetPanelElements().Where(e => (e.Kind == PanelElementKind.Alpha || e.Kind == PanelElementKind.SevenSegment) && !string.IsNullOrWhiteSpace(e.ObjectId)))
            {
                var objectId = element.ObjectId!;
                var baseIndex = ResolveBaseIndex(element);
                var cellCount = element.Kind == PanelElementKind.SevenSegment ? 1 : 16;
                var cellMasks = new int[cellCount];
                var cellBrightness = new double[cellCount];
                var reversePlatformAlphaCells = element.Kind == PanelElementKind.Alpha && RequiresPlatformAlphaCellReversal(_platformProvider());
                for (var i = 0; i < cellMasks.Length; i++)
                {
                    var source = element.Kind == PanelElementKind.SevenSegment ? _latestDigitMasksByCell : _latestVfdMasksByCell;
                    var sourceOffset = reversePlatformAlphaCells ? cellMasks.Length - 1 - i : i;
                    if (source.TryGetValue(baseIndex + sourceOffset, out var mask))
                    {
                        cellMasks[i] = element.Kind == PanelElementKind.SevenSegment
                            ? mask
                            : NormalizeMameMaskForSelectedDisplayType(mask, element.SegmentDisplayType);
                    }

                    if (element.Kind == PanelElementKind.Alpha && _latestVfdBrightnessByDisplay.TryGetValue(baseIndex, out var brightness))
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
                if (_machineObjectReferenceResolver.TryGetReference(element, out var machineReference)
                    && !machineReference.IsEmpty)
                {
                    document.RuntimeState.SetSegmentCellMasksIfChanged(machineReference, cellMasks);
                    if (element.Kind == PanelElementKind.Alpha)
                    {
                        document.RuntimeState.SetSegmentCellBrightnessIfChanged(machineReference, cellBrightness);
                    }
                    else
                    {
                        document.RuntimeState.SetSegmentCellBrightnessIfChanged(machineReference, [1d]);
                    }
                }

                if (maskChanged || brightnessChanged)
                {
                    changedObjectIds.Add(objectId);
                }
            }

            foreach (var faceDisplay in document.GetFaceElements().OfType<FaceSevenSegmentDisplayElement>())
            {
                if (string.IsNullOrWhiteSpace(faceDisplay.ObjectId)
                    || faceDisplay.LinkedMachineObjectReference is not MachineObjectReference reference
                    || reference.Kind != MachineObjectKind.SevenSegmentDisplay
                    || reference.IsEmpty
                    || !int.TryParse(reference.Id, out var cellId)
                    || !_latestDigitMasksByCell.TryGetValue(cellId, out var mask))
                {
                    continue;
                }

                var maskChanged = document.RuntimeState.SetSegmentCellMasksIfChanged(reference, [mask]);
                var brightnessChanged = document.RuntimeState.SetSegmentCellBrightnessIfChanged(reference, [1d]);
                if (maskChanged || brightnessChanged)
                {
                    changedFaceObjectIds.Add(faceDisplay.ObjectId);
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

    private int ResolveBaseIndex(PanelElementModel element)
    {
        if (_machineObjectReferenceResolver.TryGetReference(element, out var reference)
            && int.TryParse(reference.Id, out var sourceIndex))
        {
            return sourceIndex;
        }

        return element.DisplayNumber.GetValueOrDefault(0);
    }

    private static bool RequiresPlatformAlphaCellReversal(FruitMachinePlatformType platform)
    {
        return platform switch
        {
            // Impact VFD cell IDs arrive from MAME in the opposite left-to-right order
            // to the Oasis alpha display geometry. Keep the imported per-alpha Reversed
            // flag as the user/layout transform, and correct the platform source order here.
            FruitMachinePlatformType.Impact => true,
            _ => false
        };
    }

    private static int NormalizeMameMaskForSelectedDisplayType(int rawMask, string? segmentDisplayType)
    {
        var displayType = string.IsNullOrWhiteSpace(segmentDisplayType)
            ? "led16seg"
            : segmentDisplayType.Trim();

        return displayType.ToLowerInvariant() switch
        {
            // MAME stdout VFD values arrive in the legacy/source ordering used by the ROM/platform,
            // not necessarily in the same ordering as the selected Oasis geometry asset.
            // Convert the source mask into the target display geometry's bit order here.
            "led14seg" => CollapseSixteenSegmentSourceToFourteenSegmentTarget(rawMask) & 0x3FFF,
            "led14segsc" => CollapseSixteenSegmentSourceToFourteenSegmentTarget(rawMask) & 0xFFFF,
            "led16segsc" => ExpandFourteenSegmentSourceToSixteenSegmentTarget(rawMask) & 0x3FFFF,
            _ => ExpandFourteenSegmentSourceToSixteenSegmentTarget(rawMask) & 0xFFFF
        };
    }

    private static int ExpandFourteenSegmentSourceToSixteenSegmentTarget(int led14Mask)
    {
        var expandedMask = 0;

        if ((led14Mask & (1 << 0)) != 0) expandedMask |= (1 << 0) | (1 << 1);
        if ((led14Mask & (1 << 1)) != 0) expandedMask |= 1 << 2;
        if ((led14Mask & (1 << 2)) != 0) expandedMask |= 1 << 3;
        if ((led14Mask & (1 << 3)) != 0) expandedMask |= (1 << 4) | (1 << 5);
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
        if ((led14Mask & (1 << 14)) != 0) expandedMask |= 1 << 16;
        if ((led14Mask & (1 << 15)) != 0) expandedMask |= 1 << 17;

        return expandedMask;
    }

    private static int CollapseSixteenSegmentSourceToFourteenSegmentTarget(int led16Mask)
    {
        var collapsedMask = 0;

        if ((led16Mask & ((1 << 0) | (1 << 1))) != 0) collapsedMask |= 1 << 0;
        if ((led16Mask & (1 << 2)) != 0) collapsedMask |= 1 << 1;
        if ((led16Mask & (1 << 3)) != 0) collapsedMask |= 1 << 2;
        if ((led16Mask & ((1 << 4) | (1 << 5))) != 0) collapsedMask |= 1 << 3;
        if ((led16Mask & (1 << 6)) != 0) collapsedMask |= 1 << 4;
        if ((led16Mask & (1 << 7)) != 0) collapsedMask |= 1 << 5;
        if ((led16Mask & (1 << 8)) != 0) collapsedMask |= 1 << 6;
        if ((led16Mask & (1 << 9)) != 0) collapsedMask |= 1 << 7;
        if ((led16Mask & (1 << 10)) != 0) collapsedMask |= 1 << 8;
        if ((led16Mask & (1 << 11)) != 0) collapsedMask |= 1 << 9;
        if ((led16Mask & (1 << 12)) != 0) collapsedMask |= 1 << 10;
        if ((led16Mask & (1 << 13)) != 0) collapsedMask |= 1 << 11;
        if ((led16Mask & (1 << 14)) != 0) collapsedMask |= 1 << 12;
        if ((led16Mask & (1 << 15)) != 0) collapsedMask |= 1 << 13;
        if ((led16Mask & (1 << 16)) != 0) collapsedMask |= 1 << 14;
        if ((led16Mask & (1 << 17)) != 0) collapsedMask |= 1 << 15;

        return collapsedMask;
    }
}
