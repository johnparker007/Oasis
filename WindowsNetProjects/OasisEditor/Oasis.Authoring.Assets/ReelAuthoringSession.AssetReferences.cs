namespace OasisEditor;

public sealed partial class ReelAuthoringSession
{
    public IReadOnlyList<AssetReferenceSnapshot> CaptureAssetReferenceHistory()
    {
        RetainCurrentReel();
        var result = new List<AssetReferenceSnapshot>();
        foreach (var entry in _retainedReels)
            result.AddRange(entry.Value.History.Append(entry.Value.Document).Select(Payload)
                .Select(s => new AssetReferenceSnapshot(Path.GetDirectoryName(entry.Key)!, s)));
        if (_filePath is not null && _document is not null)
            result.AddRange(_history.Append(_document).Select(Payload)
                .Select(s => new AssetReferenceSnapshot(Path.GetDirectoryName(_filePath)!, s)));
        return result;
    }
}
