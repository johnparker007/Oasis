namespace OasisEditor;

public sealed partial class ReelAuthoringSession
{
    private sealed record RetainedReel(ReelDocument Document,ReelDocument[] History,int Index,string? Saved,Dictionary<string,string> Hashes,string? Library);
    private readonly Dictionary<string,RetainedReel> _retainedReels = new(PathComparer);
    public bool RetainDocuments { get; private set; }
    public void EnableDocumentRetention()=>RetainDocuments=true;
    public bool HasRetainedChanges=>IsDirty || _retainedReels.Where(p=>!PathComparer.Equals(p.Key,_filePath)).Any(p=>Payload(p.Value.Document)!=p.Value.Saved);
    public IReadOnlyList<OpenAuthoringDocument> OpenDocuments
    {get{RetainCurrentReel();return _retainedReels.Select(p=>new OpenAuthoringDocument(p.Key,Payload(p.Value.Document)!=p.Value.Saved,p.Value.Library)).ToArray();}}
    private void RetainCurrentReel()
    {
        if(!RetainDocuments || _filePath is null || _document is null)return;
        _retainedReels[_filePath]=new(_document,_history.ToArray(),_historyIndex,_savedPayload,new(_packageHashes,PathComparer),_libraryRoot);
    }
    private bool RestoreRetainedReel(string path)
    {
        if(!RetainDocuments)return false;
        EnsureActiveProject();RetainCurrentReel();
        if(!_retainedReels.TryGetValue(path,out var s))return false;
        _filePath=path;_document=s.Document;_history.Clear();_history.AddRange(s.History);_historyIndex=s.Index;
        _savedPayload=s.Saved;ReplaceHashes(s.Hashes);_libraryRoot=s.Library;return true;
    }
    public void CloseAllDocuments()
    {
        if(HasRetainedChanges)throw new InvalidOperationException("Save or undo all Reel edits before closing.");
        Close();_retainedReels.Clear();
    }
    public void CloseRetainedDocument(string path)
    {
        if(PathComparer.Equals(path,_filePath)){Close();return;}
        if(!_retainedReels.TryGetValue(path,out var s))throw new KeyNotFoundException("Reel is not open.");
        if(Payload(s.Document)!=s.Saved)throw new InvalidOperationException("Save or undo Reel edits before closing.");
        _retainedReels.Remove(path);Changed?.Invoke();
    }
}
