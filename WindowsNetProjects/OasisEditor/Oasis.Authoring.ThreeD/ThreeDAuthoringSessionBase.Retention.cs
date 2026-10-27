namespace OasisEditor;

public abstract partial class ThreeDAuthoringSessionBase
{
    private sealed record RetainedAsset(string Payload,string[] History,int Index,string Saved,Dictionary<string,string> Hashes,string? Library);
    private readonly Dictionary<string,RetainedAsset> _retainedAssets = new(OperatingSystem.IsWindows()?StringComparer.OrdinalIgnoreCase:StringComparer.Ordinal);
    public bool RetainDocuments { get; private set; }
    public void EnableDocumentRetention()=>RetainDocuments=true;
    public bool HasRetainedChanges=>IsDirty || _retainedAssets.Where(p=>!_retainedAssets.Comparer.Equals(p.Key,ManifestPath)).Any(p=>p.Value.Payload!=p.Value.Saved);
    public IReadOnlyList<OpenAuthoringDocument> OpenDocuments
    { get { RetainCurrentAsset();return _retainedAssets.Select(p=>new OpenAuthoringDocument(p.Key,p.Value.Payload!=p.Value.Saved,p.Value.Library)).ToArray(); } }
    private void RetainCurrentAsset()
    {
        if(!RetainDocuments || ManifestPath is null)return;
        _retainedAssets[ManifestPath]=new(CurrentPayload,History.ToArray(),HistoryIndex,SavedPayload,new(_packageHashes,_packageHashes.Comparer),_boundLibraryRoot);
    }
    protected bool RestoreRetainedAsset(string path)
    {
        if(!RetainDocuments)return false;
        EnsureActiveProject();RetainCurrentAsset();
        if(!_retainedAssets.TryGetValue(path,out var s))return false;
        ManifestPath=path;PackageDirectory=Path.GetDirectoryName(path);_boundLibraryRoot=s.Library;
        History.Clear();History.AddRange(s.History);HistoryIndex=s.Index;SavedPayload=s.Saved;ReplacePackageHashes(s.Hashes);SetCurrentPayload(s.Payload);
        return true;
    }
    public void CloseAllDocuments()
    {
        if(HasRetainedChanges)throw new InvalidOperationException("Save or undo all asset edits before closing.");
        Close();_retainedAssets.Clear();
    }
    public void CloseRetainedDocument(string path)
    {
        if(_retainedAssets.Comparer.Equals(path,ManifestPath)){Close();return;}
        if(!_retainedAssets.TryGetValue(path,out var s))throw new KeyNotFoundException("Asset is not open.");
        if(s.Payload!=s.Saved)throw new InvalidOperationException("Save or undo asset edits before closing.");
        _retainedAssets.Remove(path);
    }
}
