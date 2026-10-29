namespace OasisEditor;

/// <summary>Read-only identity of an open document; pending contents remain in its authoring session.</summary>
public sealed record OpenAuthoringDocument(string Path, bool IsDirty, string? LibraryRoot = null);

public sealed partial class AuthoringSession
{
    public bool RetainDocuments { get; private set; }
    public void EnableDocumentRetention() => RetainDocuments = true;
    public bool HasRetainedChanges => IsDirty || _panelStates.Values.Any(s => Payload(s.History[s.HistoryIndex]) != s.SavedContent);
    public IReadOnlyList<OpenAuthoringDocument> OpenDocuments
    {
        get { StorePanelState(); return _panelStates.Select(p => new OpenAuthoringDocument(p.Key, Payload(p.Value.History[p.Value.HistoryIndex]) != p.Value.SavedContent)).ToArray(); }
    }
    public void CloseActiveDocument()
    {
        if (IsDirty) throw new InvalidOperationException("Save or undo Panel edits before closing.");
        if (_panelPath is { } path) { _panelStates.Remove(path); _createdFmlImports.Remove(path); }
        _document = null; _panelPath = null; _diskHash = null; _savedContent = string.Empty;
        _selectedId = null; _selectedIds = []; ClearHistory(); Changed?.Invoke();
    }
    public void CloseRetainedDocument(string path)
    {
        if (_panelStates.Comparer.Equals(path,_panelPath)) { CloseActiveDocument();return; }
        if (!_panelStates.TryGetValue(path,out var s))throw new KeyNotFoundException("Panel is not open.");
        if(Payload(s.History[s.HistoryIndex])!=s.SavedContent)throw new InvalidOperationException("Save or undo Panel edits before closing.");
        _panelStates.Remove(path);_createdFmlImports.Remove(path);Changed?.Invoke();
    }
}

public sealed partial class FaceAuthoringSession
{
    private sealed record RetainedFace(string? Project, string? Root, string? Package, FaceDocumentFile[] History, int Index,
        string? Saved, Dictionary<string,string> Files, Dictionary<string,string> Generated, string? Selection);
    private readonly Dictionary<string,RetainedFace> _retainedFaces = new(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
    public bool RetainDocuments { get; private set; }
    public void EnableDocumentRetention() => RetainDocuments = true;
    public bool HasRetainedChanges => IsDirty || _retainedFaces.Where(p => !SamePath(p.Key,_facePath)).Any(p => FaceDocumentStorage.SerializePayload(p.Value.History[p.Value.Index]) != p.Value.Saved);
    public IReadOnlyList<OpenAuthoringDocument> OpenDocuments
    {
        get { RetainCurrentFace(); return _retainedFaces.Select(p => new OpenAuthoringDocument(p.Key, FaceDocumentStorage.SerializePayload(p.Value.History[p.Value.Index]) != p.Value.Saved)).ToArray(); }
    }
    private void RetainCurrentFace()
    {
        if (!RetainDocuments || _facePath is null) return;
        _retainedFaces[_facePath] = new(_projectFileAtOpen,_projectRootAtOpen,_packageDirectory,_history.ToArray(),_historyIndex,_savedPayload,
            new(_savedPackageFiles,_savedPackageFiles.Comparer),new(_savedGeneratedFiles,_savedGeneratedFiles.Comparer),_selectedId);
    }
    private bool RestoreRetainedFace(string path)
    {
        if (!RetainDocuments) return false;
        RetainCurrentFace();
        if (!_retainedFaces.TryGetValue(path,out var s) || !SamePath(s.Project,_project.ProjectFilePath)) return false;
        _facePath=path;_projectFileAtOpen=s.Project;_projectRootAtOpen=s.Root;_packageDirectory=s.Package;
        _history.Clear();_history.AddRange(s.History);_historyIndex=s.Index;_savedPayload=s.Saved;
        _savedPackageFiles=new(s.Files,s.Files.Comparer);_savedGeneratedFiles=new(s.Generated,s.Generated.Comparer);_selectedId=s.Selection;
        Changed?.Invoke();return true;
    }
    public void CloseAllDocuments()
    {
        if(HasRetainedChanges)throw new InvalidOperationException("Save or undo all Face edits before closing.");
        _retainedFaces.Clear();ClearDocument();Changed?.Invoke();
    }
    public void CloseRetainedDocument(string path)
    {
        if(SamePath(path,_facePath)){Close();return;}
        if(!_retainedFaces.TryGetValue(path,out var s))throw new KeyNotFoundException("Face is not open.");
        if(FaceDocumentStorage.SerializePayload(s.History[s.Index])!=s.Saved)throw new InvalidOperationException("Save or undo Face edits before closing.");
        _retainedFaces.Remove(path);Changed?.Invoke();
    }
}

public sealed partial class MachineAuthoringSession
{
    private sealed record RetainedMachine(MachineDocument Document, MachineDocument[] History, string[] BehaviorHistory, int Index,
        string? DiskHash,string Saved,string Behavior,string SavedBehavior,string? BehaviorHash);
    private readonly Dictionary<string,RetainedMachine> retainedMachines = new(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
    public bool RetainDocuments { get; private set; }
    public void EnableDocumentRetention() => RetainDocuments = true;
    public bool HasRetainedChanges => IsDirty || retainedMachines.Where(p => !SamePath(p.Key,machinePath)).Any(p => Payload(p.Value.Document)!=p.Value.Saved || p.Value.Behavior!=p.Value.SavedBehavior);
    public IReadOnlyList<OpenAuthoringDocument> OpenDocuments
    {
        get { RetainCurrentMachine();return retainedMachines.Select(p=>new OpenAuthoringDocument(p.Key,Payload(p.Value.Document)!=p.Value.Saved || p.Value.Behavior!=p.Value.SavedBehavior)).ToArray(); }
    }
    private void RetainCurrentMachine()
    {
        if(!RetainDocuments || machinePath is null || document is null)return;
        retainedMachines[machinePath]=new(CloneDocument(document),history.Select(CloneDocument).ToArray(),behaviorHistory.ToArray(),historyIndex,diskHash,savedPayload,behaviorSource,savedBehaviorSource,behaviorDiskHash);
    }
    private bool RestoreRetainedMachine(string path)
    {
        if(!RetainDocuments)return false;
        RetainCurrentMachine();
        if(!retainedMachines.TryGetValue(path,out var s))return false;
        machinePath=path;document=CloneDocument(s.Document);history.Clear();history.AddRange(s.History.Select(CloneDocument));
        behaviorHistory.Clear();behaviorHistory.AddRange(s.BehaviorHistory);historyIndex=s.Index;diskHash=s.DiskHash;savedPayload=s.Saved;
        behaviorSource=s.Behavior;savedBehaviorSource=s.SavedBehavior;behaviorDiskHash=s.BehaviorHash;
        Changed?.Invoke();return true;
    }
    public void CloseActiveDocument()
    {
        if(IsDirty)throw new InvalidOperationException("Save or undo Machine edits before closing.");
        if(machinePath is {} path)retainedMachines.Remove(path);
        machinePath=null;document=null;diskHash=null;savedPayload=string.Empty;behaviorSource=savedBehaviorSource=string.Empty;behaviorDiskHash=null;ResetHistory();Changed?.Invoke();
    }
    public void CloseProject()
    {
        if(HasRetainedChanges)throw new InvalidOperationException("Save or undo all Machine edits before closing the project.");
        CloseActiveDocument();retainedMachines.Clear();projectDirectory=null;projectFilePath=null;
    }
    public void CloseRetainedDocument(string path)
    {
        if(SamePath(path,machinePath)){CloseActiveDocument();return;}
        if(!retainedMachines.TryGetValue(path,out var s))throw new KeyNotFoundException("Machine is not open.");
        if(Payload(s.Document)!=s.Saved||s.Behavior!=s.SavedBehavior)throw new InvalidOperationException("Save or undo Machine edits before closing.");
        retainedMachines.Remove(path);Changed?.Invoke();
    }
}
