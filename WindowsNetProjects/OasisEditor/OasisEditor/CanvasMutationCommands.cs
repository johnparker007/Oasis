namespace OasisEditor;

internal static class CanvasMutationCommands
{
    public static Commands.ICommand CreateAddRectangleCommand(Guid documentId, DocumentTabViewModel document, PanelElementFile element)
    {
        return new AddPanelElementMutationCommand(documentId, document, element, "Add rectangle");
    }

    public static Commands.ICommand CreateAddImageCommand(Guid documentId, DocumentTabViewModel document, PanelElementFile element)
    {
        return new AddPanelElementMutationCommand(documentId, document, element, "Add image");
    }

    public static Commands.ICommand CreateDeleteElementCommand(Guid documentId, DocumentTabViewModel document, PanelSelectionInfo selection)
    {
        return new DeleteElementMutationCommand(documentId, document, selection);
    }

    public static Commands.ICommand CreateRenameElementCommand(
        Guid documentId,
        DocumentTabViewModel document,
        PanelSelectionInfo selection,
        string newName)
    {
        return new RenameElementMutationCommand(documentId, document, selection, newName);
    }

    private sealed class AddPanelElementMutationCommand : Commands.IDocumentCommand, Commands.IExecutionTrackedCommand
    {
        private readonly Guid _documentId;
        private readonly DocumentTabViewModel _document;
        private readonly PanelElementFile _element;
        private readonly string _description;
        private int? _insertIndex;

        public AddPanelElementMutationCommand(
            Guid documentId,
            DocumentTabViewModel document,
            PanelElementFile element,
            string description)
        {
            _documentId = documentId;
            _document = document;
            _element = element;
            _description = description;
        }

        public Guid DocumentId => _documentId;

        public string Description => _description;

        public bool WasExecuted { get; private set; }

        public void Execute()
        {
            WasExecuted = false;
            var elements = _document.GetPanelElements().ToList();
            var elementModel = Panel2DDocumentStorage.ToModel(_element);
            var index = Math.Clamp(_insertIndex ?? elements.Count, 0, elements.Count);
            elements.Insert(index, elementModel);
            _insertIndex = index;
            _document.SetPanelElements(elements);
            _document.MarkDirty();
            WasExecuted = true;
        }

        public void Undo()
        {
            var elements = _document.GetPanelElements().ToList();
            if (elements.Count == 0)
            {
                return;
            }

            var removed = false;
            if (_insertIndex is int index
                && index >= 0
                && index < elements.Count
                && IsSameElement(elements[index], _element))
            {
                elements.RemoveAt(index);
                removed = true;
            }

            if (!removed)
            {
                for (var i = elements.Count - 1; i >= 0; i--)
                {
                    if (!IsSameElement(elements[i], _element))
                    {
                        continue;
                    }

                    elements.RemoveAt(i);
                    removed = true;
                    break;
                }
            }

            if (removed)
            {
                _document.SetPanelElements(elements);
                _document.MarkDirty();
            }
        }
    }

    private sealed class DeleteElementMutationCommand : Commands.IDocumentCommand, Commands.IExecutionTrackedCommand
    {
        private readonly Guid _documentId;
        private readonly DocumentTabViewModel _document;
        private readonly PanelSelectionInfo _selection;
        private PanelElementFile? _deletedElement;
        private int? _deletedIndex;

        public DeleteElementMutationCommand(Guid documentId, DocumentTabViewModel document, PanelSelectionInfo selection)
        {
            _documentId = documentId;
            _document = document;
            _selection = selection;
        }

        public Guid DocumentId => _documentId;

        public string Description => "Delete element";

        public bool WasExecuted { get; private set; }

        public void Execute()
        {
            WasExecuted = false;
            var elements = _document.GetPanelElements().ToList();
            if (!TryFindMatchingElementIndex(elements, _selection, out var index))
            {
                return;
            }

            _deletedElement = Panel2DDocumentStorage.ToStorageElement(elements[index]);
            _deletedIndex = index;
            elements.RemoveAt(index);
            _document.SetPanelElements(elements);
            _document.MarkDirty();
            WasExecuted = true;
        }

        public void Undo()
        {
            if (_deletedElement is null || _deletedIndex is not int index)
            {
                return;
            }

            var elements = _document.GetPanelElements().ToList();
            var insertIndex = Math.Clamp(index, 0, elements.Count);
            elements.Insert(insertIndex, Panel2DDocumentStorage.ToModel(_deletedElement));
            _document.SetPanelElements(elements);
            _document.MarkDirty();
        }
    }

    private sealed class RenameElementMutationCommand : Commands.IDocumentCommand, Commands.IExecutionTrackedCommand
    {
        private readonly Guid _documentId;
        private readonly DocumentTabViewModel _document;
        private readonly PanelSelectionInfo _selection;
        private readonly string _newName;
        private int? _renamedIndex;
        private string? _previousName;

        public RenameElementMutationCommand(
            Guid documentId,
            DocumentTabViewModel document,
            PanelSelectionInfo selection,
            string newName)
        {
            _documentId = documentId;
            _document = document;
            _selection = selection;
            _newName = newName;
        }

        public Guid DocumentId => _documentId;

        public string Description => "Rename element";

        public bool WasExecuted { get; private set; }

        public void Execute()
        {
            WasExecuted = false;
            var elements = _document.GetPanelElements().ToList();
            if (!TryFindMatchingElementIndex(elements, _selection, out var index))
            {
                return;
            }

            var existing = elements[index];
            var normalizedNewName = _newName.Trim();
            if (string.Equals(existing.Name.Trim(), normalizedNewName, StringComparison.Ordinal))
            {
                return;
            }

            _renamedIndex = index;
            _previousName = existing.Name;
            elements[index] = new PanelElementModel
            {
                ObjectId = existing.ObjectId,
                Name = normalizedNewName,
                Kind = existing.Kind,
                X = existing.X,
                Y = existing.Y,
                Width = existing.Width,
                Height = existing.Height
            };

            _document.SetPanelElements(elements);
            _document.MarkDirty();
            WasExecuted = true;
        }

        public void Undo()
        {
            if (_renamedIndex is not int index)
            {
                return;
            }

            var elements = _document.GetPanelElements().ToList();
            if (index < 0 || index >= elements.Count)
            {
                return;
            }

            if (!PanelSelectionContract.IsMatch(elements[index], _selection))
            {
                return;
            }

            var existing = elements[index];
            elements[index] = new PanelElementModel
            {
                ObjectId = existing.ObjectId,
                Name = _previousName ?? string.Empty,
                Kind = existing.Kind,
                X = existing.X,
                Y = existing.Y,
                Width = existing.Width,
                Height = existing.Height
            };

            _document.SetPanelElements(elements);
            _document.MarkDirty();
        }
    }

    private static bool TryFindMatchingElementIndex(IReadOnlyList<PanelElementModel> elements, PanelSelectionInfo selection, out int index)
    {
        if (!string.IsNullOrWhiteSpace(selection.ObjectId))
        {
            for (var i = 0; i < elements.Count; i++)
            {
                if (string.Equals(elements[i].ObjectId, selection.ObjectId, StringComparison.Ordinal))
                {
                    index = i;
                    return true;
                }
            }
        }

        for (var i = 0; i < elements.Count; i++)
        {
            var element = elements[i];
            if (!IsMatch(element, selection))
            {
                continue;
            }

            index = i;
            return true;
        }

        index = -1;
        return false;
    }

    private static bool IsSameElement(PanelElementModel model, PanelElementFile storageElement)
    {
        return string.Equals(model.ObjectId, storageElement.ObjectId, StringComparison.Ordinal)
               && string.Equals(model.Name, storageElement.Name, StringComparison.Ordinal)
               && model.Kind == Panel2DDocumentStorage.ParseElementKind(storageElement.Kind)
               && Math.Abs(model.X - storageElement.X) < 0.0001
               && Math.Abs(model.Y - storageElement.Y) < 0.0001
               && Math.Abs(model.Width - storageElement.Width) < 0.0001
               && Math.Abs(model.Height - storageElement.Height) < 0.0001;
    }

    private static bool IsMatch(PanelElementModel element, PanelSelectionInfo selection)
    {
        if (!string.IsNullOrWhiteSpace(selection.ObjectId))
        {
            return string.Equals(element.ObjectId, selection.ObjectId, StringComparison.Ordinal);
        }

        return string.Equals(element.Name, selection.DisplayName, StringComparison.Ordinal)
               && element.Kind == selection.Kind
               && Math.Abs(element.X - selection.X) < 0.0001
               && Math.Abs(element.Y - selection.Y) < 0.0001
               && Math.Abs(element.Width - selection.Width) < 0.0001
               && Math.Abs(element.Height - selection.Height) < 0.0001;
    }
}
