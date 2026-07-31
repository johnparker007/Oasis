namespace OasisEditor;

public sealed class MachineSegmentRuntimeAdapter : IMachineSegmentRuntimeAdapter
{
    private readonly object _pendingSync = new();
    private readonly Func<IEnumerable<DocumentTabViewModel>> _documentProvider;
    private readonly Func<FruitMachinePlatformType> _platformProvider;
    private readonly Action<string>? _diagnosticLogger;
    private readonly Action<Action> _uiDispatch;
    private readonly IMachineObjectReferenceResolver _machineObjectReferenceResolver;
    private readonly Dictionary<int, (int Mask, SegmentOutputType OutputType)> _pendingMasks = new();
    private readonly Dictionary<int, double> _pendingVfdBrightnessByDisplay = new();
    private readonly Dictionary<int, int> _latestVfdMasksByCell = new();
    private readonly Dictionary<int, SegmentOutputType> _latestAlphaOutputTypeByCell = new();
    private readonly Dictionary<int, int> _latestDigitMasksByCell = new();
    private readonly Dictionary<int, double> _latestVfdBrightnessByDisplay = new();
    private bool _uiUpdateScheduled;

    public MachineSegmentRuntimeAdapter(
        Func<IEnumerable<DocumentTabViewModel>> documentProvider,
        Action<Action> uiDispatch,
        Func<FruitMachinePlatformType>? platformProvider = null,
        Action<string>? diagnosticLogger = null)
    {
        _documentProvider = documentProvider ?? throw new ArgumentNullException(nameof(documentProvider));
        _uiDispatch = uiDispatch ?? throw new ArgumentNullException(nameof(uiDispatch));
        _platformProvider = platformProvider ?? (() => FruitMachinePlatformType.MPU4);
        _diagnosticLogger = diagnosticLogger;
        _machineObjectReferenceResolver = MachineObjectReferenceResolver.Instance;
    }

    public void ApplySegmentState(int cellId, int segmentMask, SegmentOutputType outputType)
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
        Dictionary<int, (int Mask, SegmentOutputType OutputType)> snapshot;
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
            var changedMachineReferences = new HashSet<MachineObjectReference>();

            foreach (var (cellId, state) in snapshot)
            {
                if (state.OutputType == SegmentOutputType.Vfd || state.OutputType == SegmentOutputType.NativeAlpha)
                {
                    _latestVfdMasksByCell[cellId] = state.Mask;
                    _latestAlphaOutputTypeByCell[cellId] = state.OutputType;
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
                // A panel seven-segment element is one physical digit. Its display number is
                // the dense Oasis digit-cell ID published by the backend.
                var cellCount = element.Kind == PanelElementKind.SevenSegment ? 1 : 16;
                var cellMasks = new int[cellCount];
                var cellBrightness = new double[cellCount];
                for (var i = 0; i < cellMasks.Length; i++)
                {
                    var source = element.Kind == PanelElementKind.SevenSegment ? _latestDigitMasksByCell : _latestVfdMasksByCell;
                    var sourceOffset = element.Kind == PanelElementKind.Alpha
                        ? AlphaCellOrder.SourceIndexForCanonicalCell(i, cellMasks.Length, element.IsReversed == true, _platformProvider())
                        : i;
                    if (source.TryGetValue(baseIndex + sourceOffset, out var mask))
                    {
                        cellMasks[i] = element.Kind == PanelElementKind.SevenSegment
                            ? mask
                            : mask;
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
                if (element.Kind == PanelElementKind.Alpha)
                {
                    LogAlphaTrace(element, baseIndex, snapshot, cellMasks);
                }
                else
                {
                    LogDigitTrace(element, baseIndex, snapshot, cellMasks, maskChanged);
                }
                var brightnessChanged = element.Kind == PanelElementKind.Alpha
                    && document.RuntimeState.SetSegmentCellBrightnessIfChanged(objectId, cellBrightness);
                if (_machineObjectReferenceResolver.TryGetReference(element, out var machineReference)
                    && !machineReference.IsEmpty)
                {
                    var machineMaskChanged = document.RuntimeState.SetSegmentCellMasksIfChanged(machineReference, cellMasks);
                    var machineBrightnessChanged = element.Kind == PanelElementKind.Alpha
                        ? document.RuntimeState.SetSegmentCellBrightnessIfChanged(machineReference, cellBrightness)
                        : document.RuntimeState.SetSegmentCellBrightnessIfChanged(machineReference, [1d]);
                    if (machineMaskChanged || machineBrightnessChanged)
                    {
                        changedMachineReferences.Add(machineReference);
                        AddChangedFaceDisplayObjectIds(document, machineReference, changedFaceObjectIds);
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
                    || !int.TryParse(reference.Id, out var cellId))
                {
                    continue;
                }

                var cellCount = Math.Max(1, faceDisplay.DigitCount);
                var cellMasks = new int[cellCount];
                var cellBrightness = Enumerable.Repeat(1d, cellCount).ToArray();
                var hasLiveCell = false;
                for (var position = 0; position < cellCount; position++)
                {
                    if (_latestDigitMasksByCell.TryGetValue(cellId + position, out var mask))
                    {
                        cellMasks[position] = mask;
                        hasLiveCell = true;
                    }
                }
                if (!hasLiveCell)
                {
                    continue;
                }

                var maskChanged = document.RuntimeState.SetSegmentCellMasksIfChanged(reference, cellMasks);
                var brightnessChanged = document.RuntimeState.SetSegmentCellBrightnessIfChanged(reference, cellBrightness);
                if (maskChanged || brightnessChanged || changedMachineReferences.Contains(reference))
                {
                    FaceRuntimeDisplayReferenceIndex.AddObjectIdsForReference<FaceSevenSegmentDisplayElement>(document, reference, changedFaceObjectIds);
                    if (_diagnosticLogger is not null && maskChanged)
                    {
                        _diagnosticLogger(
                            $"Seven-segment face update: face={faceDisplay.ObjectId}; reference={reference}; " +
                            $"baseCellId={cellId}; digitCount={cellCount}; masks=[{string.Join(", ", cellMasks.Select(value => $"0x{value:X}"))}].");
                    }
                }
            }

            foreach (var faceDisplay in document.GetFaceElements().OfType<FaceAlphaDisplayElement>())
            {
                if (string.IsNullOrWhiteSpace(faceDisplay.ObjectId)
                    || faceDisplay.LinkedMachineObjectReference is not MachineObjectReference reference
                    || reference.Kind != MachineObjectKind.AlphaDisplay
                    || reference.IsEmpty
                    || !int.TryParse(reference.Id, out var baseIndex))
                {
                    continue;
                }

                var cellMasks = new int[16];
                var cellBrightness = new double[16];
                for (var i = 0; i < cellMasks.Length; i++)
                {
                    var sourceOffset = AlphaCellOrder.SourceIndexForCanonicalCell(i, cellMasks.Length, faceDisplay.IsReversed, _platformProvider());
                    if (_latestVfdMasksByCell.TryGetValue(baseIndex + sourceOffset, out var mask))
                    {
                        cellMasks[i] = mask;
                    }

                    cellBrightness[i] = _latestVfdBrightnessByDisplay.TryGetValue(baseIndex, out var brightness)
                        ? brightness
                        : 1d;
                }

                var maskChanged = document.RuntimeState.SetSegmentCellMasksIfChanged(reference, cellMasks);
                var brightnessChanged = document.RuntimeState.SetSegmentCellBrightnessIfChanged(reference, cellBrightness);
                if (maskChanged || brightnessChanged || changedMachineReferences.Contains(reference))
                {
                    FaceRuntimeDisplayReferenceIndex.AddObjectIdsForReference<FaceAlphaDisplayElement>(document, reference, changedFaceObjectIds);
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


    private static void AddChangedFaceDisplayObjectIds(DocumentTabViewModel document, MachineObjectReference reference, ISet<string> changedFaceObjectIds)
    {
        if (reference.Kind == MachineObjectKind.SevenSegmentDisplay)
        {
            FaceRuntimeDisplayReferenceIndex.AddObjectIdsForReference<FaceSevenSegmentDisplayElement>(document, reference, changedFaceObjectIds);
            return;
        }

        if (reference.Kind == MachineObjectKind.AlphaDisplay)
        {
            FaceRuntimeDisplayReferenceIndex.AddObjectIdsForReference<FaceAlphaDisplayElement>(document, reference, changedFaceObjectIds);
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

    private void LogAlphaTrace(
        PanelElementModel element,
        int baseIndex,
        IReadOnlyDictionary<int, (int Mask, SegmentOutputType OutputType)> updates,
        IReadOnlyList<int> canonicalMasks)
    {
        if (_diagnosticLogger is null
            || !updates.Keys.Any(cellId => cellId >= baseIndex && cellId < baseIndex + canonicalMasks.Count))
        {
            return;
        }

        var platform = _platformProvider();
        var reversed = element.IsReversed == true;
        var raw = updates
            .Where(update => update.Key >= baseIndex && update.Key < baseIndex + canonicalMasks.Count)
            .OrderBy(update => update.Key)
            .Select(update => $"{update.Value.OutputType}:id={update.Key}:mask=0x{update.Value.Mask:X}");
        var mapping = Enumerable.Range(0, canonicalMasks.Count).Select(destinationIndex =>
        {
            var sourceOffset = AlphaCellOrder.SourceIndexForCanonicalCell(destinationIndex, canonicalMasks.Count, reversed, platform);
            var sourceId = baseIndex + sourceOffset;
            var outputType = _latestAlphaOutputTypeByCell.GetValueOrDefault(sourceId);
            return $"dst={destinationIndex}<-srcOffset={sourceOffset}:id={sourceId}:{outputType}";
        });
        var reference = _machineObjectReferenceResolver.TryGetReference(element, out var resolvedReference)
            ? resolvedReference.ToString()
            : "unresolved";

        _diagnosticLogger(
            $"Alpha ordering trace: platform={platform}; element={element.ObjectId}; displayNumber={element.DisplayNumber?.ToString() ?? "null"}; " +
            $"reference={reference}; baseIndex={baseIndex}; reversed={reversed}; raw=[{string.Join(", ", raw)}]; " +
            $"mapping=[{string.Join(", ", mapping)}]; canonical=[{string.Join(", ", canonicalMasks.Select(mask => $"0x{mask:X}"))}]");
    }

    private void LogDigitTrace(
        PanelElementModel element,
        int cellId,
        IReadOnlyDictionary<int, (int Mask, SegmentOutputType OutputType)> updates,
        IReadOnlyList<int> masks,
        bool updated)
    {
        if (_diagnosticLogger is null
            || !updates.TryGetValue(cellId, out var state)
            || state.OutputType != SegmentOutputType.Digit)
        {
            return;
        }

        var reference = _machineObjectReferenceResolver.TryGetReference(element, out var resolvedReference)
            ? resolvedReference.ToString()
            : "unresolved";
        _diagnosticLogger(
            $"Seven-segment panel update: cellId={cellId}; mask=0x{state.Mask:X}; element={element.ObjectId}; " +
            $"displayNumber={element.DisplayNumber?.ToString() ?? "null"}; reference={reference}; updated={updated}; " +
            $"runtimeMasks=[{string.Join(", ", masks.Select(value => $"0x{value:X}"))}].");
    }

}

// Kept explicit so the live trace records the exact currently selected mapping.
// Its physical left/right interpretation must be validated from a captured run.
internal static class AlphaCellOrder
{
    internal static int SourceIndexForCanonicalCell(int canonicalIndex, int cellCount, bool sourceAddressingReversed, FruitMachinePlatformType platform)
    {
        var reverseSource = platform == FruitMachinePlatformType.Impact
            ? !sourceAddressingReversed
            : sourceAddressingReversed;
        return reverseSource ? cellCount - 1 - canonicalIndex : canonicalIndex;
    }
}
