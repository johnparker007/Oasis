namespace OasisEditor;

public sealed class MameLampRuntimeAdapter : IMameLampRuntimeAdapter
{
    private readonly Func<IEnumerable<DocumentTabViewModel>> _documentProvider;
    private readonly Action<Action> _uiDispatch;

    public MameLampRuntimeAdapter(Func<IEnumerable<DocumentTabViewModel>> documentProvider, Action<Action> uiDispatch)
    {
        _documentProvider = documentProvider ?? throw new ArgumentNullException(nameof(documentProvider));
        _uiDispatch = uiDispatch ?? throw new ArgumentNullException(nameof(uiDispatch));
    }

    public void ApplyLampState(int lampId, int lampValue)
    {
        _uiDispatch(() => ApplyOnUiThread(lampId, lampValue));
    }

    private void ApplyOnUiThread(int lampId, int lampValue)
    {
        var normalizedIntensity = Math.Clamp(lampValue / 255d, 0d, 1d);
        foreach (var document in _documentProvider())
        {
            var matchingObjectIds = document
                .GetPanelElements()
                .Where(element => element.Kind == PanelElementKind.Lamp
                    && element.DisplayNumber.GetValueOrDefault() == lampId
                    && !string.IsNullOrWhiteSpace(element.ObjectId))
                .Select(element => element.ObjectId)
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            if (matchingObjectIds.Length == 0)
            {
                continue;
            }

            foreach (var objectId in matchingObjectIds)
            {
                document.RuntimeState.SetLampIntensity(objectId, normalizedIntensity);
            }

            document.NotifyPanelVisualPreviewChanged();
        }
    }
}
