namespace OasisEditor;

/// <summary>Reference-bearing content held by an open document, including undo and redo.</summary>
public sealed record AssetReferenceSnapshot(string BaseDirectory, string Content);

public sealed partial class AuthoringSession
{
    public IReadOnlyList<AssetReferenceSnapshot> CaptureAssetReferenceHistory()
    {
        StorePanelState();
        var result = new List<AssetReferenceSnapshot>();
        foreach (var entry in _panelStates)
            result.AddRange(entry.Value.History.Select(d => new AssetReferenceSnapshot(Path.GetDirectoryName(entry.Key)!, Payload(d))));
        if (_panelPath is not null)
        {
            result.AddRange(_history.Select(d => new AssetReferenceSnapshot(Path.GetDirectoryName(_panelPath)!, Payload(d))));
        }
        if (_panelClipboard.Length > 0 && _panelClipboardProjectRoot is not null)
            result.Add(new(_panelClipboardProjectRoot, System.Text.Json.JsonSerializer.Serialize(new { Elements = _panelClipboard })));
        return result;
    }
}

public sealed partial class FaceAuthoringSession
{
    public IReadOnlyList<AssetReferenceSnapshot> CaptureAssetReferenceHistory()
    {
        RetainCurrentFace();
        var result = new List<AssetReferenceSnapshot>();
        foreach (var entry in _retainedFaces)
            result.AddRange(entry.Value.History.Select(d => new AssetReferenceSnapshot(Path.GetDirectoryName(entry.Key)!, FaceDocumentStorage.SerializePayload(d))));
        if (_facePath is not null)
            result.AddRange(_history.Select(d => new AssetReferenceSnapshot(Path.GetDirectoryName(_facePath)!, FaceDocumentStorage.SerializePayload(d))));
        return result;
    }
}

public sealed partial class MachineAuthoringSession
{
    public IReadOnlyList<AssetReferenceSnapshot> CaptureAssetReferenceHistory()
    {
        RetainCurrentMachine();
        var result = new List<AssetReferenceSnapshot>();
        foreach (var entry in retainedMachines)
            result.AddRange(entry.Value.History.Select(Payload).Concat(entry.Value.BehaviorHistory)
                .Append(Payload(entry.Value.Document)).Select(s => new AssetReferenceSnapshot(Path.GetDirectoryName(entry.Key)!, s)));
        if (machinePath is not null)
            result.AddRange(history.Select(Payload).Concat(behaviorHistory).Append(behaviorSource)
                .Concat(document is null ? [] : new[] { Payload(document) })
                .Select(s => new AssetReferenceSnapshot(Path.GetDirectoryName(machinePath)!, s)));
        return result;
    }
}
