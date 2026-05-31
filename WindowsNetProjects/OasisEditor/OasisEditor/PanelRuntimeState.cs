namespace OasisEditor;

public sealed class PanelRuntimeState
{
    private readonly Dictionary<string, double> _lampIntensityByObjectId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, double> _reelPositionByObjectId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int[]> _segmentMasksByObjectId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, double[]> _segmentBrightnessByObjectId = new(StringComparer.Ordinal);

    public string? LampTestObjectId { get; set; }

    public bool IsLampTestActive => !string.IsNullOrWhiteSpace(LampTestObjectId);

    public IReadOnlyDictionary<string, double> LampIntensityByObjectId => _lampIntensityByObjectId;
    public IReadOnlyDictionary<string, double> ReelPositionByObjectId => _reelPositionByObjectId;
    public IReadOnlyDictionary<string, int[]> SegmentMasksByObjectId => _segmentMasksByObjectId;
    public IReadOnlyDictionary<string, double[]> SegmentBrightnessByObjectId => _segmentBrightnessByObjectId;

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

    public void SetLampIntensity(string objectId, double intensity)
    {
        SetLampIntensityIfChanged(objectId, intensity);
    }

    public double GetLampIntensity(string objectId)
    {
        if (string.IsNullOrWhiteSpace(objectId))
        {
            return 0d;
        }

        return _lampIntensityByObjectId.GetValueOrDefault(objectId.Trim(), 0d);
    }

    public double GetReelPosition(string objectId)
    {
        if (string.IsNullOrWhiteSpace(objectId))
        {
            return 0;
        }

        return _reelPositionByObjectId.GetValueOrDefault(objectId.Trim(), 0d);
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
        _lampIntensityByObjectId.Clear();
        _reelPositionByObjectId.Clear();
        _segmentMasksByObjectId.Clear();
        _segmentBrightnessByObjectId.Clear();
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
}
