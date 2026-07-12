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
        var document = _selectedDocumentAccessor();
        return document is not null
            && document.SelectionState.Items.Any(IsBulkDeletableSelectionItem);
    }

    public bool CanDeleteItem(HierarchyItemViewModel hierarchyItem)
    {
        var selectedDocument = _selectedDocumentAccessor();
        if (selectedDocument is null || selectedDocument.Document.DocumentType != EditorDocumentType.Panel2D)
        {
            return false;
        }

        return !hierarchyItem.IsGroup
               && hierarchyItem.PanelSelection is PanelSelectionInfo selection
               && selectedDocument.HasPanelElement(selection);
    }

    public bool DeleteItem(HierarchyItemViewModel hierarchyItem)
    {
        if (!CanDeleteItem(hierarchyItem) || hierarchyItem.PanelSelection is not PanelSelectionInfo selection)
        {
            return false;
        }

        var selectedDocument = _selectedDocumentAccessor();
        if (selectedDocument is null)
        {
            return false;
        }

        _updateDocumentSelection(selectedDocument.DocumentId, selection);
        return DeleteSelected();
    }

    public bool DeleteSelected()
    {
        var document = _selectedDocumentAccessor();
        if (document is null)
        {
            return false;
        }

        var selectionSnapshot = document.SelectionState.Items.ToArray();
        if (!selectionSnapshot.Any(IsBulkDeletableSelectionItem))
        {
            return false;
        }

        var command = new BulkDeleteSelectionCommand(document.DocumentId, document, selectionSnapshot);
        return _executeCanvasCommand(document.DocumentId, command);
    }

    private static bool IsBulkDeletableSelectionItem(EditorSelectionItem item)
    {
        return item.Domain is EditorSelectionDomain.PanelElement or EditorSelectionDomain.FaceElement;
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

    public bool CanBringToFrontSelected()
    {
        return CanReorderSelected(ReorderAction.BringToFront);
    }

    public bool CanSendToBackSelected()
    {
        return CanReorderSelected(ReorderAction.SendToBack);
    }

    public bool CanBringForwardSelected()
    {
        return CanReorderSelected(ReorderAction.BringForward);
    }

    public bool CanSendBackwardSelected()
    {
        return CanReorderSelected(ReorderAction.SendBackward);
    }

    public bool CanLockSelected()
    {
        return TryGetSelectionDocument(out var document, out var selection)
               && document.TryGetPanelElement(selection, out var element)
               && !element.IsTransformLocked;
    }

    public bool CanUnlockSelected()
    {
        return TryGetSelectionDocument(out var document, out var selection)
               && document.TryGetPanelElement(selection, out var element)
               && element.IsTransformLocked;
    }

    public bool CanHideSelected()
    {
        return TryGetSelectionDocument(out var document, out var selection)
               && document.TryGetPanelElement(selection, out var element)
               && element.IsVisible;
    }

    public bool CanShowSelected()
    {
        return TryGetSelectionDocument(out var document, out var selection)
               && document.TryGetPanelElement(selection, out var element)
               && !element.IsVisible;
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

    public void ExecuteBringToFrontSelected()
    {
        ExecuteReorderSelected(ReorderAction.BringToFront);
    }

    public void ExecuteSendToBackSelected()
    {
        ExecuteReorderSelected(ReorderAction.SendToBack);
    }

    public void ExecuteBringForwardSelected()
    {
        ExecuteReorderSelected(ReorderAction.BringForward);
    }

    public void ExecuteSendBackwardSelected()
    {
        ExecuteReorderSelected(ReorderAction.SendBackward);
    }

    public void ExecuteLockSelected()
    {
        ExecuteSetLockSelected(true);
    }

    public void ExecuteUnlockSelected()
    {
        ExecuteSetLockSelected(false);
    }

    public void ExecuteHideSelected()
    {
        ExecuteSetVisibleSelected(false);
    }

    public void ExecuteShowSelected()
    {
        ExecuteSetVisibleSelected(true);
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
            Element = PanelElementModelCloner.Clone(element)
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

    private bool CanReorderSelected(ReorderAction action)
    {
        if (!TryGetSelectionDocument(out var document, out var selection) || !document.HasPanelElement(selection))
        {
            return false;
        }

        var elements = document.GetPanelElements();
        if (elements.Count <= 1)
        {
            return false;
        }

        var selectedIndex = -1;
        for (var i = 0; i < elements.Count; i++)
        {
            if (!PanelSelectionContract.IsMatch(Panel2DDocumentStorage.ToStorageElement(elements[i]), selection))
            {
                continue;
            }

            selectedIndex = i;
            break;
        }
        if (selectedIndex < 0)
        {
            return false;
        }

        return action switch
        {
            ReorderAction.BringToFront => selectedIndex < elements.Count - 1,
            ReorderAction.SendToBack => selectedIndex > 0,
            ReorderAction.BringForward => selectedIndex < elements.Count - 1,
            ReorderAction.SendBackward => selectedIndex > 0,
            _ => false
        };
    }

    private void ExecuteReorderSelected(ReorderAction action)
    {
        if (!TryGetSelectionDocument(out var document, out var selection) || !document.HasPanelElement(selection))
        {
            return;
        }

        Commands.ICommand command = action switch
        {
            ReorderAction.BringToFront => CanvasMutationCommands.CreateBringToFrontCommand(document.DocumentId, document, selection),
            ReorderAction.SendToBack => CanvasMutationCommands.CreateSendToBackCommand(document.DocumentId, document, selection),
            ReorderAction.BringForward => CanvasMutationCommands.CreateBringForwardCommand(document.DocumentId, document, selection),
            ReorderAction.SendBackward => CanvasMutationCommands.CreateSendBackwardCommand(document.DocumentId, document, selection),
            _ => throw new InvalidOperationException($"Unsupported reorder action '{action}'.")
        };

        _executeCanvasCommand(document.DocumentId, command);
    }

    private void ExecuteSetLockSelected(bool isTransformLocked)
    {
        if (!TryGetSelectionDocument(out var document, out var selection) || !document.HasPanelElement(selection))
        {
            return;
        }

        var command = CanvasMutationCommands.CreateSetTransformLockedCommand(document.DocumentId, document, selection, isTransformLocked);
        _executeCanvasCommand(document.DocumentId, command);
    }

    private void ExecuteSetVisibleSelected(bool isVisible)
    {
        if (!TryGetSelectionDocument(out var document, out var selection) || !document.HasPanelElement(selection))
        {
            return;
        }

        var command = CanvasMutationCommands.CreateSetVisibleCommand(document.DocumentId, document, selection, isVisible);
        _executeCanvasCommand(document.DocumentId, command);
    }

    private enum ReorderAction
    {
        BringToFront,
        SendToBack,
        BringForward,
        SendBackward
    }
}
