namespace OasisEditor;

internal sealed class HierarchyPanelCommandService
{
    private readonly Func<DocumentTabViewModel?> _selectedDocumentAccessor;
    private readonly Func<Guid, Commands.ICommand, bool> _executeCanvasCommand;
    private readonly Action<Guid, PanelSelectionInfo?> _updateDocumentSelection;
    private readonly Action _notifyCanExecuteChanged;
    private PanelElementClipboardPayload? _clipboardPayload;

    public HierarchyPanelCommandService(
        Func<DocumentTabViewModel?> selectedDocumentAccessor,
        Func<Guid, Commands.ICommand, bool> executeCanvasCommand,
        Action<Guid, PanelSelectionInfo?> updateDocumentSelection,
        Action notifyCanExecuteChanged)
    {
        _selectedDocumentAccessor = selectedDocumentAccessor;
        _executeCanvasCommand = executeCanvasCommand;
        _updateDocumentSelection = updateDocumentSelection;
        _notifyCanExecuteChanged = notifyCanExecuteChanged;
    }

    public bool CanDeleteSelected()
    {
        return TryGetSelectionDocument(out var document, out var selection) && document.HasPanelElement(selection);
    }

    public bool DeleteSelected()
    {
        if (!TryGetSelectionDocument(out var document, out var selection) || !document.HasPanelElement(selection))
        {
            return false;
        }

        var command = CanvasMutationCommands.CreateDeleteElementCommand(document.DocumentId, document, selection);
        var wasDeleted = _executeCanvasCommand(document.DocumentId, command);
        if (wasDeleted)
        {
            _updateDocumentSelection(document.DocumentId, null);
        }

        return wasDeleted;
    }

    public bool TryGetSelectedName(out string currentName)
    {
        currentName = string.Empty;
        if (!TryGetSelectionDocument(out var document, out var selection))
        {
            return false;
        }

        if (!document.TryGetPanelElement(selection, out var matchingElement))
        {
            return false;
        }

        currentName = matchingElement.Name ?? string.Empty;
        return true;
    }

    public bool RenameSelected(string newName)
    {
        if (!TryGetSelectionDocument(out var document, out var selection))
        {
            return false;
        }

        var normalizedName = newName?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName) || !document.HasPanelElement(selection))
        {
            return false;
        }

        var command = CanvasMutationCommands.CreateRenameElementCommand(
            document.DocumentId,
            document,
            selection,
            normalizedName);
        return _executeCanvasCommand(document.DocumentId, command);
    }

    public bool CanCopySelected()
    {
        return TryGetSelectionDocument(out var document, out var selection) && document.HasPanelElement(selection);
    }

    public bool CanCutSelected()
    {
        return CanCopySelected();
    }

    public bool CanPasteSelected()
    {
        var selectedDocument = _selectedDocumentAccessor();
        return selectedDocument is not null
               && selectedDocument.Document.DocumentType == EditorDocumentType.Panel2D
               && _clipboardPayload is not null;
    }

    public bool CanDuplicateSelected()
    {
        return TryGetSelectionDocument(out var document, out var selection) && document.HasPanelElement(selection);
    }

    public void ExecuteCutSelected()
    {
        if (!TryGetSelectionDocument(out var document, out var selection) || !document.HasPanelElement(selection))
        {
            return;
        }

        if (!TryCopySelectedToClipboard())
        {
            return;
        }

        DeleteSelected();
    }

    public void ExecuteCopySelected()
    {
        TryCopySelectedToClipboard();
    }

    public void ExecutePasteSelected()
    {
        var selectedDocument = _selectedDocumentAccessor();
        if (selectedDocument is null || selectedDocument.Document.DocumentType != EditorDocumentType.Panel2D)
        {
            return;
        }

        var clipboardPayload = _clipboardPayload;
        if (clipboardPayload is null)
        {
            return;
        }

        var command = CanvasMutationCommands.CreatePasteElementCommand(
            selectedDocument.DocumentId,
            selectedDocument,
            clipboardPayload.Element);
        _executeCanvasCommand(selectedDocument.DocumentId, command);
    }

    public void ExecuteDuplicateSelected()
    {
        if (!TryGetSelectionDocument(out var document, out var selection) || !document.HasPanelElement(selection))
        {
            return;
        }

        var command = CanvasMutationCommands.CreateDuplicateElementCommand(
            document.DocumentId,
            document,
            selection);

        _executeCanvasCommand(document.DocumentId, command);
    }

    private bool TryCopySelectedToClipboard()
    {
        if (!TryGetSelectionDocument(out var document, out var selection))
        {
            return false;
        }

        if (!document.TryGetPanelElement(selection, out var element))
        {
            return false;
        }

        _clipboardPayload = new PanelElementClipboardPayload
        {
            Element = new PanelElementModel
            {
                ObjectId = element.ObjectId,
                Name = element.Name,
                Kind = element.Kind,
                X = element.X,
                Y = element.Y,
                Width = element.Width,
                Height = element.Height
            }
        };

        _notifyCanExecuteChanged();
        return true;
    }

    private bool TryGetSelectionDocument(out DocumentTabViewModel document, out PanelSelectionInfo selection)
    {
        document = null!;
        selection = default;

        var selectedDocument = _selectedDocumentAccessor();
        if (selectedDocument is null || selectedDocument.Document.DocumentType != EditorDocumentType.Panel2D)
        {
            return false;
        }

        if (selectedDocument.HierarchySelectedPanelSelection is not PanelSelectionInfo selected)
        {
            return false;
        }

        document = selectedDocument;
        selection = selected;
        return true;
    }
}
