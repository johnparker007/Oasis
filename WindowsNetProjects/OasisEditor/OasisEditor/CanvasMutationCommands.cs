namespace OasisEditor;

public static class CanvasMutationCommands
{
    public static Commands.ICommand CreateAddRectangleCommand(Guid documentId, DocumentTabViewModel document, PanelElementFile element)
    {
        return new AddRectangleMutationCommand(documentId, document, element);
    }

    public static Commands.ICommand CreateAddImageCommand(Guid documentId, DocumentTabViewModel document, PanelElementFile element)
    {
        return new AddImageMutationCommand(documentId, document, element);
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

    private static bool IsSameElement(PanelElementFile left, PanelElementFile right)
    {
        return string.Equals(left.Kind, right.Kind, StringComparison.OrdinalIgnoreCase)
            && left.X.Equals(right.X)
            && left.Y.Equals(right.Y)
            && left.Width.Equals(right.Width)
            && left.Height.Equals(right.Height);
    }
}
