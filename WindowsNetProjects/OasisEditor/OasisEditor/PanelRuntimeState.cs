namespace OasisEditor;

public sealed class PanelRuntimeState
{
    private readonly Dictionary<string, double> _lampIntensityByObjectId = new(StringComparer.Ordinal);

    public string? LampTestObjectId { get; set; }

    public bool IsLampTestActive => !string.IsNullOrWhiteSpace(LampTestObjectId);

    public IReadOnlyDictionary<string, double> LampIntensityByObjectId => _lampIntensityByObjectId;

    public void SetLampIntensity(string objectId, double intensity)
    {
        if (string.IsNullOrWhiteSpace(objectId))
        {
            return;
        }

        _lampIntensityByObjectId[objectId.Trim()] = Math.Clamp(intensity, 0d, 1d);
    }

    public double GetLampIntensity(string objectId)
    {
        if (string.IsNullOrWhiteSpace(objectId))
        {
            return 0d;
        }

        return _lampIntensityByObjectId.GetValueOrDefault(objectId.Trim(), 0d);
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
    }
}
