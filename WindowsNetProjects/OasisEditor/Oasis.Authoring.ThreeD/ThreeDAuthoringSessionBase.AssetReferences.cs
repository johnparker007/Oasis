namespace OasisEditor;

public abstract partial class ThreeDAuthoringSessionBase
{
    public IReadOnlyList<AssetReferenceSnapshot> CaptureAssetReferenceHistory()
    {
        RetainCurrentAsset();
        var result = new List<AssetReferenceSnapshot>();
        foreach (var entry in _retainedAssets)
            result.AddRange(entry.Value.History.Append(entry.Value.Payload).Append(entry.Value.Saved)
                .Select(s => new AssetReferenceSnapshot(Path.GetDirectoryName(entry.Key)!, s)));
        if (ManifestPath is not null)
            result.AddRange(History.Append(CurrentPayload).Select(s => new AssetReferenceSnapshot(Path.GetDirectoryName(ManifestPath)!, s)));
        return result;
    }
}
