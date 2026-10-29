namespace OasisEditor;

public sealed partial class AuthoringSession
{
    public void CloseProject()
    {
        if (HasRetainedChanges) throw new InvalidOperationException("Save or undo all Panel edits before closing project.");
        _document = null; _panelPath = null; _projectFilePath = null; _projectDirectory = null;
        ProjectName = null; _diskHash = null; _savedContent = string.Empty;
        _selectedId = null; _selectedIds = [];
        _panelStates.Clear(); _createdFmlImports.Clear(); ClearHistory();
        Changed?.Invoke();
    }
}
