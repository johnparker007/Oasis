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
            var elements = Panel2DDocumentStorage.DeserializeLayout(_document.PanelLayoutJson).ToList();
            var index = Math.Clamp(_insertIndex ?? elements.Count, 0, elements.Count);
            elements.Insert(index, _element);
            _insertIndex = index;
            _document.PanelLayoutJson = Panel2DDocumentStorage.SerializeLayout(elements);
            _document.MarkDirty();
            WasExecuted = true;
        }

        public void Undo()
        {
            var elements = Panel2DDocumentStorage.DeserializeLayout(_document.PanelLayoutJson).ToList();
            if (elements.Count == 0)
            {
                return;
            }

            var removed = false;
            if (_insertIndex is int index
                && index >= 0
                && index < elements.Count
                && PanelSelectionContract.IsSame(elements[index], _element))
            {
                elements.RemoveAt(index);
                removed = true;
            }

            if (!removed)
            {
                for (var i = elements.Count - 1; i >= 0; i--)
                {
                    if (!PanelSelectionContract.IsSame(elements[i], _element))
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
                _document.PanelLayoutJson = Panel2DDocumentStorage.SerializeLayout(elements);
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
            var elements = Panel2DDocumentStorage.DeserializeLayout(_document.PanelLayoutJson).ToList();
            if (!TryFindMatchingElementIndex(elements, _selection, out var index))
            {
                return;
            }

            _deletedElement = elements[index];
            _deletedIndex = index;
            elements.RemoveAt(index);
            _document.PanelLayoutJson = Panel2DDocumentStorage.SerializeLayout(elements);
            _document.MarkDirty();
            WasExecuted = true;
        }

        public void Undo()
        {
            if (_deletedElement is null || _deletedIndex is not int index)
            {
                return;
            }

            var elements = Panel2DDocumentStorage.DeserializeLayout(_document.PanelLayoutJson).ToList();
            var insertIndex = Math.Clamp(index, 0, elements.Count);
            elements.Insert(insertIndex, _deletedElement);
            _document.PanelLayoutJson = Panel2DDocumentStorage.SerializeLayout(elements);
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
            var elements = Panel2DDocumentStorage.DeserializeLayout(_document.PanelLayoutJson).ToList();
            if (!TryFindMatchingElementIndex(elements, _selection, out var index))
            {
                return;
            }

            var existing = elements[index];
            var normalizedNewName = _newName.Trim();
            if (string.Equals(existing.Name?.Trim(), normalizedNewName, StringComparison.Ordinal))
            {
                return;
            }

            _renamedIndex = index;
            _previousName = existing.Name;
            elements[index] = new PanelElementFile
            {
                ObjectId = existing.ObjectId,
                Name = normalizedNewName,
                Kind = existing.Kind,
                X = existing.X,
                Y = existing.Y,
                Width = existing.Width,
                Height = existing.Height
            };

            _document.PanelLayoutJson = Panel2DDocumentStorage.SerializeLayout(elements);
            _document.MarkDirty();
            WasExecuted = true;
        }

        public void Undo()
        {
            if (_renamedIndex is not int index)
            {
                return;
            }

            var elements = Panel2DDocumentStorage.DeserializeLayout(_document.PanelLayoutJson).ToList();
            if (index < 0 || index >= elements.Count)
            {
                return;
            }

            if (!PanelSelectionContract.IsMatch(elements[index], _selection))
            {
                return;
            }

            var existing = elements[index];
            elements[index] = new PanelElementFile
            {
                ObjectId = existing.ObjectId,
                Name = _previousName ?? string.Empty,
                Kind = existing.Kind,
                X = existing.X,
                Y = existing.Y,
                Width = existing.Width,
                Height = existing.Height
            };

            _document.PanelLayoutJson = Panel2DDocumentStorage.SerializeLayout(elements);
            _document.MarkDirty();
        }
    }

    private static bool TryFindMatchingElementIndex(IReadOnlyList<PanelElementFile> elements, PanelSelectionInfo selection, out int index)
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
            if (!PanelSelectionContract.IsMatch(element, selection))
            {
                continue;
            }

            index = i;
            return true;
        }

        index = -1;
        return false;
    }
}
