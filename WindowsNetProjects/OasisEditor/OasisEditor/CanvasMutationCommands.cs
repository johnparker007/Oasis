namespace OasisEditor;

internal static class CanvasMutationCommands
{
    public static Commands.ICommand CreateAddRectangleCommand(Guid documentId, DocumentTabViewModel document, PanelElementFile element)
    {
        return new AddRectangleMutationCommand(documentId, document, element);
    }

    public static Commands.ICommand CreateAddImageCommand(Guid documentId, DocumentTabViewModel document, PanelElementFile element)
    {
        return new AddImageMutationCommand(documentId, document, element);
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

    private sealed class AddRectangleMutationCommand : Commands.IDocumentCommand
    {
        private readonly Guid _documentId;
        private readonly DocumentTabViewModel _document;
        private readonly PanelElementFile _element;
        private int? _insertIndex;

        public AddRectangleMutationCommand(Guid documentId, DocumentTabViewModel document, PanelElementFile element)
        {
            _documentId = documentId;
            _document = document;
            _element = element;
        }

        public Guid DocumentId => _documentId;

        public string Description => "Add rectangle";

        public void Execute()
        {
            var elements = Panel2DDocumentStorage.DeserializeLayout(_document.PanelLayoutJson).ToList();
            var index = Math.Clamp(_insertIndex ?? elements.Count, 0, elements.Count);
            elements.Insert(index, _element);
            _insertIndex = index;
            _document.PanelLayoutJson = Panel2DDocumentStorage.SerializeLayout(elements);
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
                _document.PanelLayoutJson = Panel2DDocumentStorage.SerializeLayout(elements);
            }
        }
    }

    private sealed class AddImageMutationCommand : Commands.IDocumentCommand
    {
        private readonly Guid _documentId;
        private readonly DocumentTabViewModel _document;
        private readonly PanelElementFile _element;
        private int? _insertIndex;

        public AddImageMutationCommand(Guid documentId, DocumentTabViewModel document, PanelElementFile element)
        {
            _documentId = documentId;
            _document = document;
            _element = element;
        }

        public Guid DocumentId => _documentId;

        public string Description => "Add image";

        public void Execute()
        {
            var elements = Panel2DDocumentStorage.DeserializeLayout(_document.PanelLayoutJson).ToList();
            var index = Math.Clamp(_insertIndex ?? elements.Count, 0, elements.Count);
            elements.Insert(index, _element);
            _insertIndex = index;
            _document.PanelLayoutJson = Panel2DDocumentStorage.SerializeLayout(elements);
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
                _document.PanelLayoutJson = Panel2DDocumentStorage.SerializeLayout(elements);
            }
        }
    }

    private sealed class DeleteElementMutationCommand : Commands.IDocumentCommand
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

        public void Execute()
        {
            var elements = Panel2DDocumentStorage.DeserializeLayout(_document.PanelLayoutJson).ToList();
            if (!TryFindMatchingElementIndex(elements, _selection, out var index))
            {
                return;
            }

            _deletedElement = elements[index];
            _deletedIndex = index;
            elements.RemoveAt(index);
            _document.PanelLayoutJson = Panel2DDocumentStorage.SerializeLayout(elements);
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
        }
    }

    private sealed class RenameElementMutationCommand : Commands.IDocumentCommand
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

        public void Execute()
        {
            var elements = Panel2DDocumentStorage.DeserializeLayout(_document.PanelLayoutJson).ToList();
            if (!TryFindMatchingElementIndex(elements, _selection, out var index))
            {
                return;
            }

            var existing = elements[index];
            _renamedIndex = index;
            _previousName = existing.Name;
            elements[index] = new PanelElementFile
            {
                ObjectId = existing.ObjectId,
                Name = _newName,
                Kind = existing.Kind,
                X = existing.X,
                Y = existing.Y,
                Width = existing.Width,
                Height = existing.Height
            };

            _document.PanelLayoutJson = Panel2DDocumentStorage.SerializeLayout(elements);
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

            if (!IsSelectionMatch(elements[index], _selection))
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
            if (!string.Equals(element.Kind, selection.Kind, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!element.X.Equals(selection.X)
                || !element.Y.Equals(selection.Y)
                || !element.Width.Equals(selection.Width)
                || !element.Height.Equals(selection.Height))
            {
                continue;
            }

            index = i;
            return true;
        }

        index = -1;
        return false;
    }

    private static bool IsSameElement(PanelElementFile left, PanelElementFile right)
    {
        if (!string.IsNullOrWhiteSpace(left.ObjectId)
            && !string.IsNullOrWhiteSpace(right.ObjectId))
        {
            return string.Equals(left.ObjectId, right.ObjectId, StringComparison.Ordinal);
        }

        return string.Equals(left.Kind, right.Kind, StringComparison.OrdinalIgnoreCase)
            && left.X.Equals(right.X)
            && left.Y.Equals(right.Y)
            && left.Width.Equals(right.Width)
            && left.Height.Equals(right.Height);
    }

    private static bool IsSelectionMatch(PanelElementFile element, PanelSelectionInfo selection)
    {
        if (!string.IsNullOrWhiteSpace(selection.ObjectId)
            && !string.IsNullOrWhiteSpace(element.ObjectId))
        {
            return string.Equals(element.ObjectId, selection.ObjectId, StringComparison.Ordinal);
        }

        return string.Equals(element.Kind, selection.Kind, StringComparison.OrdinalIgnoreCase)
            && element.X.Equals(selection.X)
            && element.Y.Equals(selection.Y)
            && element.Width.Equals(selection.Width)
            && element.Height.Equals(selection.Height);
    }
}
