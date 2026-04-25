namespace OasisEditor;

public sealed class ActiveDocumentContextService
{
    private readonly Dictionary<Guid, PanelSelectionInfo?> _panelSelectionsByDocument = new();
    private Guid? _activeDocumentId;

    public Guid? ActiveDocumentId => _activeDocumentId;

    public PanelSelectionInfo? ActivePanelSelection
    {
        get
        {
            if (_activeDocumentId is not Guid activeDocumentId)
            {
                return null;
            }

            return _panelSelectionsByDocument.GetValueOrDefault(activeDocumentId);
        }
    }

    public void SetActiveDocument(DocumentTabViewModel? activeDocument)
    {
        _activeDocumentId = activeDocument?.DocumentId;
    }

    public void SetPanelSelection(Guid documentId, PanelSelectionInfo? selection)
    {
        _panelSelectionsByDocument[documentId] = selection;
    }

    public void ClearDocumentState(Guid documentId)
    {
        _panelSelectionsByDocument.Remove(documentId);
        if (_activeDocumentId == documentId)
        {
            _activeDocumentId = null;
        }
    }

    public void ClearAll()
    {
        _panelSelectionsByDocument.Clear();
        _activeDocumentId = null;
    }
}

public readonly record struct PanelSelectionInfo(
    string ObjectId,
    string Kind,
    double X,
    double Y,
    double Width,
    double Height);
