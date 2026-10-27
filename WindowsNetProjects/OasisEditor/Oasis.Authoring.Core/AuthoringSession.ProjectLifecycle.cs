namespace OasisEditor;

public sealed partial class AuthoringSession
{
    public void CloseProject()
    {
        EnsureCanReplaceDocument();
        _document = null; _panelPath = null; _projectFilePath = null; _projectDirectory = null;
        ProjectName = null; _diskHash = null; _savedContent = string.Empty;
        _selectedId = null; _selectedIds = [];
        _panelStates.Clear(); _importedInputDefinitions.Clear(); ClearHistory();
        Changed?.Invoke();
    }
}
