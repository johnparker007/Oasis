namespace OasisEditor;

public class MachineRuntimeState
{
    private readonly Dictionary<string, double> _lampIntensityByObjectId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, double> _reelPositionByObjectId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, double> _temporaryReelOffsetByObjectId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int[]> _segmentMasksByObjectId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, double[]> _segmentBrightnessByObjectId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int[]> _vfdDotMatrixDotsByObjectId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, double> _lampIntensityByMachineObjectId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, double> _reelPositionByMachineObjectId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int[]> _segmentMasksByMachineObjectId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, double[]> _segmentBrightnessByMachineObjectId = new(StringComparer.Ordinal);

    public string? LampTestObjectId { get; set; }
    public FruitMachinePlatformType FruitMachinePlatform { get; set; } = FruitMachinePlatformType.None;
    public EmulationBackendKind EmulationBackendKind { get; set; } = EmulationBackendKind.Mame;

    public bool IsLampTestActive => !string.IsNullOrWhiteSpace(LampTestObjectId);

    public IReadOnlyDictionary<string, double> LampIntensityByObjectId => _lampIntensityByObjectId;
    public IReadOnlyDictionary<string, double> ReelPositionByObjectId => _reelPositionByObjectId;
    public IReadOnlyDictionary<string, double> TemporaryReelOffsetByObjectId => _temporaryReelOffsetByObjectId;
    public IReadOnlyDictionary<string, int[]> SegmentMasksByObjectId => _segmentMasksByObjectId;
    public IReadOnlyDictionary<string, double[]> SegmentBrightnessByObjectId => _segmentBrightnessByObjectId;
    public IReadOnlyDictionary<string, int[]> VfdDotMatrixDotsByObjectId => _vfdDotMatrixDotsByObjectId;
    public IReadOnlyDictionary<string, double> LampIntensityByMachineObjectId => _lampIntensityByMachineObjectId;
    public IReadOnlyDictionary<string, double> ReelPositionByMachineObjectId => _reelPositionByMachineObjectId;
    public IReadOnlyDictionary<string, int[]> SegmentMasksByMachineObjectId => _segmentMasksByMachineObjectId;
    public IReadOnlyDictionary<string, double[]> SegmentBrightnessByMachineObjectId => _segmentBrightnessByMachineObjectId;

    public bool SetLampIntensityIfChanged(string objectId, double intensity)
    {
        if (string.IsNullOrWhiteSpace(objectId))
        {
            return false;
        }

        var normalizedObjectId = objectId.Trim();
        var normalizedIntensity = Math.Clamp(intensity, 0d, 1d);
        if (_lampIntensityByObjectId.TryGetValue(normalizedObjectId, out var previous)
            && Math.Abs(previous - normalizedIntensity) < 0.0001d)
        {
            return false;
        }

        _lampIntensityByObjectId[normalizedObjectId] = normalizedIntensity;
        return true;
    }

    public bool SetLampIntensityIfChanged(MachineObjectReference machineObjectReference, double intensity)
    {
        if (machineObjectReference.Kind != MachineObjectKind.Lamp || machineObjectReference.IsEmpty)
        {
            return false;
        }

        var key = machineObjectReference.ToString();
        var normalizedIntensity = Math.Clamp(intensity, 0d, 1d);
        if (_lampIntensityByMachineObjectId.TryGetValue(key, out var previous)
            && Math.Abs(previous - normalizedIntensity) < 0.0001d)
        {
            return false;
        }

        _lampIntensityByMachineObjectId[key] = normalizedIntensity;
        return true;
    }

    public bool SetReelPositionIfChanged(string objectId, double position)
    {
        if (string.IsNullOrWhiteSpace(objectId))
        {
            return false;
        }

        var normalizedObjectId = objectId.Trim();
        if (_reelPositionByObjectId.TryGetValue(normalizedObjectId, out var previous)
            && Math.Abs(previous - position) < 0.0001d)
        {
            return false;
        }

        _reelPositionByObjectId[normalizedObjectId] = position;
        return true;
    }

    public bool SetReelPositionIfChanged(MachineObjectReference machineObjectReference, double position)
    {
        if (machineObjectReference.Kind != MachineObjectKind.Reel || machineObjectReference.IsEmpty)
        {
            return false;
        }

        var key = machineObjectReference.ToString();
        if (_reelPositionByMachineObjectId.TryGetValue(key, out var previous)
            && Math.Abs(previous - position) < 0.0001d)
        {
            return false;
        }

        _reelPositionByMachineObjectId[key] = position;
        return true;
    }

    public bool SetTemporaryReelOffsetIfChanged(string objectId, double offset)
    {
        if (string.IsNullOrWhiteSpace(objectId) || double.IsNaN(offset) || double.IsInfinity(offset))
        {
            return false;
        }

        var normalizedObjectId = objectId.Trim();
        if (_temporaryReelOffsetByObjectId.TryGetValue(normalizedObjectId, out var previous)
            && Math.Abs(previous - offset) < 0.0001d)
        {
            return false;
        }

        _temporaryReelOffsetByObjectId[normalizedObjectId] = offset;
        return true;
    }

    public bool ClearTemporaryReelOffsetIfChanged(string objectId)
    {
        if (string.IsNullOrWhiteSpace(objectId))
        {
            return false;
        }

        return _temporaryReelOffsetByObjectId.Remove(objectId.Trim());
    }

    public void SetLampIntensity(string objectId, double intensity)
    {
        SetLampIntensityIfChanged(objectId, intensity);
    }

    public void SetLampIntensity(MachineObjectReference machineObjectReference, double intensity)
    {
        SetLampIntensityIfChanged(machineObjectReference, intensity);
    }

    public double GetLampIntensity(string objectId)
    {
        if (string.IsNullOrWhiteSpace(objectId))
        {
            return 0d;
        }

        return _lampIntensityByObjectId.GetValueOrDefault(objectId.Trim(), 0d);
    }

    public double GetLampIntensity(MachineObjectReference machineObjectReference)
    {
        if (machineObjectReference.Kind != MachineObjectKind.Lamp || machineObjectReference.IsEmpty)
        {
            return 0d;
        }

        return _lampIntensityByMachineObjectId.GetValueOrDefault(machineObjectReference.ToString(), 0d);
    }

    public double GetReelPosition(string objectId)
    {
        return TryGetReelPosition(objectId, out var position)
            ? position
            : 0d;
    }

    public bool TryGetReelPosition(string objectId, out double position)
    {
        if (string.IsNullOrWhiteSpace(objectId))
        {
            position = 0d;
            return false;
        }

        return _reelPositionByObjectId.TryGetValue(objectId.Trim(), out position);
    }

    public double GetReelPosition(MachineObjectReference machineObjectReference)
    {
        if (machineObjectReference.Kind != MachineObjectKind.Reel || machineObjectReference.IsEmpty)
        {
            return 0d;
        }

        return _reelPositionByMachineObjectId.GetValueOrDefault(machineObjectReference.ToString(), 0d);
    }

    public double GetTemporaryReelOffset(string objectId)
    {
        if (string.IsNullOrWhiteSpace(objectId))
        {
            return 0d;
        }

        return _temporaryReelOffsetByObjectId.GetValueOrDefault(objectId.Trim(), 0d);
    }

    public double GetEffectiveReelPosition(string objectId)
    {
        if (string.IsNullOrWhiteSpace(objectId))
        {
            return 0d;
        }

        var normalizedObjectId = objectId.Trim();
        return _reelPositionByObjectId.GetValueOrDefault(normalizedObjectId, 0d)
            + _temporaryReelOffsetByObjectId.GetValueOrDefault(normalizedObjectId, 0d);
    }

    public void ClearLampIntensity(string objectId)
    {
        if (!string.IsNullOrWhiteSpace(objectId))
        {
            _lampIntensityByObjectId.Remove(objectId.Trim());
        }
    }

    public void Clear()
    {
        LampTestObjectId = null;
        FruitMachinePlatform = FruitMachinePlatformType.None;
        _lampIntensityByObjectId.Clear();
        _reelPositionByObjectId.Clear();
        _temporaryReelOffsetByObjectId.Clear();
        _segmentMasksByObjectId.Clear();
        _segmentBrightnessByObjectId.Clear();
        _vfdDotMatrixDotsByObjectId.Clear();
        _lampIntensityByMachineObjectId.Clear();
        _reelPositionByMachineObjectId.Clear();
        _segmentMasksByMachineObjectId.Clear();
        _segmentBrightnessByMachineObjectId.Clear();
    }

    public bool SetSegmentCellMasksIfChanged(string objectId, int[] masks)
    {
        if (string.IsNullOrWhiteSpace(objectId) || masks is null)
        {
            return false;
        }

        var normalizedObjectId = objectId.Trim();
        if (_segmentMasksByObjectId.TryGetValue(normalizedObjectId, out var previous)
            && previous.SequenceEqual(masks))
        {
            return false;
        }

        _segmentMasksByObjectId[normalizedObjectId] = masks.ToArray();
        return true;
    }


    public bool SetSegmentCellMasksIfChanged(MachineObjectReference machineObjectReference, int[] masks)
    {
        if (!IsDisplayReference(machineObjectReference) || masks is null)
        {
            return false;
        }

        var key = machineObjectReference.ToString();
        if (_segmentMasksByMachineObjectId.TryGetValue(key, out var previous)
            && previous.SequenceEqual(masks))
        {
            return false;
        }

        _segmentMasksByMachineObjectId[key] = masks.ToArray();
        return true;
    }


    public bool SetSegmentCellBrightnessIfChanged(string objectId, double[] brightnessByCell)
    {
        if (string.IsNullOrWhiteSpace(objectId) || brightnessByCell is null)
        {
            return false;
        }

        var normalizedObjectId = objectId.Trim();
        if (_segmentBrightnessByObjectId.TryGetValue(normalizedObjectId, out var previous)
            && previous.Length == brightnessByCell.Length
            && previous.Zip(brightnessByCell, (left, right) => Math.Abs(left - right) < 0.0001d).All(equal => equal))
        {
            return false;
        }

        _segmentBrightnessByObjectId[normalizedObjectId] = brightnessByCell.Select(value => Math.Clamp(value, 0d, 1d)).ToArray();
        return true;
    }

    public bool SetSegmentCellBrightnessIfChanged(MachineObjectReference machineObjectReference, double[] brightnessByCell)
    {
        if (!IsDisplayReference(machineObjectReference) || brightnessByCell is null)
        {
            return false;
        }

        var key = machineObjectReference.ToString();
        if (_segmentBrightnessByMachineObjectId.TryGetValue(key, out var previous)
            && previous.Length == brightnessByCell.Length
            && previous.Zip(brightnessByCell, (left, right) => Math.Abs(left - right) < 0.0001d).All(equal => equal))
        {
            return false;
        }

        _segmentBrightnessByMachineObjectId[key] = brightnessByCell.Select(value => Math.Clamp(value, 0d, 1d)).ToArray();
        return true;
    }

    public double[] GetSegmentCellBrightness(string objectId, int cellCount)
    {
        if (string.IsNullOrWhiteSpace(objectId) || cellCount <= 0)
        {
            return Array.Empty<double>();
        }

        return _segmentBrightnessByObjectId.TryGetValue(objectId.Trim(), out var value)
            ? value.ToArray()
            : Enumerable.Repeat(1d, cellCount).ToArray();
    }

    public double[] GetSegmentCellBrightness(MachineObjectReference machineObjectReference, int cellCount)
    {
        if (!IsDisplayReference(machineObjectReference) || cellCount <= 0)
        {
            return Array.Empty<double>();
        }

        return _segmentBrightnessByMachineObjectId.TryGetValue(machineObjectReference.ToString(), out var value)
            ? value.ToArray()
            : Enumerable.Repeat(1d, cellCount).ToArray();
    }

    public int[] GetSegmentCellMasks(string objectId, int cellCount)
    {
        if (string.IsNullOrWhiteSpace(objectId) || cellCount <= 0)
        {
            return Array.Empty<int>();
        }

        return _segmentMasksByObjectId.TryGetValue(objectId.Trim(), out var value)
            ? value.ToArray()
            : new int[cellCount];
    }

    public int[] GetSegmentCellMasks(MachineObjectReference machineObjectReference, int cellCount)
    {
        if (!IsDisplayReference(machineObjectReference) || cellCount <= 0)
        {
            return Array.Empty<int>();
        }

        return _segmentMasksByMachineObjectId.TryGetValue(machineObjectReference.ToString(), out var value)
            ? value.ToArray()
            : new int[cellCount];
    }

    public bool SetVfdDotMatrixDotsIfChanged(string objectId, int[] dots)
    {
        if (string.IsNullOrWhiteSpace(objectId) || dots is null)
        {
            return false;
        }

        var normalizedObjectId = objectId.Trim();
        var normalizedDots = dots.Select(value => value == 1 ? 1 : 0).ToArray();
        if (_vfdDotMatrixDotsByObjectId.TryGetValue(normalizedObjectId, out var previous)
            && previous.SequenceEqual(normalizedDots))
        {
            return false;
        }

        _vfdDotMatrixDotsByObjectId[normalizedObjectId] = normalizedDots;
        return true;
    }

    public int[] GetVfdDotMatrixDots(string objectId, int dotCount)
    {
        if (string.IsNullOrWhiteSpace(objectId) || dotCount <= 0)
        {
            return Array.Empty<int>();
        }

        return _vfdDotMatrixDotsByObjectId.TryGetValue(objectId.Trim(), out var value)
            ? value.ToArray()
            : new int[dotCount];
    }

    private static bool IsDisplayReference(MachineObjectReference machineObjectReference)
    {
        return !machineObjectReference.IsEmpty
            && machineObjectReference.Kind is MachineObjectKind.AlphaDisplay or MachineObjectKind.SevenSegmentDisplay;
    }
}

public sealed class PanelRuntimeState : MachineRuntimeState
{
}
