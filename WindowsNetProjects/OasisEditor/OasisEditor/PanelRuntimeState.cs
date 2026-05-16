namespace OasisEditor;

public sealed class PanelRuntimeState
{
    private readonly Dictionary<string, double> _lampIntensityByObjectId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _reelPositionByObjectId = new(StringComparer.Ordinal);

    public string? LampTestObjectId { get; set; }

    public bool IsLampTestActive => !string.IsNullOrWhiteSpace(LampTestObjectId);

    public IReadOnlyDictionary<string, double> LampIntensityByObjectId => _lampIntensityByObjectId;
    public IReadOnlyDictionary<string, int> ReelPositionByObjectId => _reelPositionByObjectId;

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

    public bool SetReelPositionIfChanged(string objectId, int position)
    {
        if (string.IsNullOrWhiteSpace(objectId))
        {
            return false;
        }

        var normalizedObjectId = objectId.Trim();
        if (_reelPositionByObjectId.TryGetValue(normalizedObjectId, out var previous) && previous == position)
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

    public int GetReelPosition(string objectId)
    {
        if (string.IsNullOrWhiteSpace(objectId))
        {
            return 0;
        }

        return _reelPositionByObjectId.GetValueOrDefault(objectId.Trim(), 0);
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
    }
}
