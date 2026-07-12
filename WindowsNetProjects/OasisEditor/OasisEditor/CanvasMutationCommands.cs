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

    public static Commands.ICommand CreateAddPanelElementCommand(Guid documentId, DocumentTabViewModel document, PanelElementFile element)
    {
        var kind = Panel2DDocumentStorage.ParseElementKind(element.Kind);
        var description = kind switch
        {
            PanelElementKind.Lamp => "Add lamp",
            PanelElementKind.Reel => "Add reel",
            PanelElementKind.SevenSegment => "Add 7 segment display",
            PanelElementKind.Alpha => "Add segment alpha",
            PanelElementKind.VfdDotMatrix => "Add VFD dot matrix",
            _ => "Add panel element"
        };

        return new AddPanelElementMutationCommand(documentId, document, element, description);
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

    public static Commands.ICommand CreateDuplicateElementCommand(
        Guid documentId,
        DocumentTabViewModel document,
        PanelSelectionInfo selection)
    {
        return new DuplicateElementMutationCommand(documentId, document, selection);
    }

    public static Commands.ICommand CreatePasteElementCommand(
        Guid documentId,
        DocumentTabViewModel document,
        PanelElementModel sourceElement)
    {
        return new PasteElementMutationCommand(documentId, document, sourceElement);
    }

    public static Commands.ICommand CreateBringToFrontCommand(
        Guid documentId,
        DocumentTabViewModel document,
        PanelSelectionInfo selection)
    {
        return new ReorderElementMutationCommand(documentId, document, selection, ReorderDirection.BringToFront);
    }

    public static Commands.ICommand CreateSendToBackCommand(
        Guid documentId,
        DocumentTabViewModel document,
        PanelSelectionInfo selection)
    {
        return new ReorderElementMutationCommand(documentId, document, selection, ReorderDirection.SendToBack);
    }

    public static Commands.ICommand CreateBringForwardCommand(
        Guid documentId,
        DocumentTabViewModel document,
        PanelSelectionInfo selection)
    {
        return new ReorderElementMutationCommand(documentId, document, selection, ReorderDirection.BringForward);
    }

    public static Commands.ICommand CreateSendBackwardCommand(
        Guid documentId,
        DocumentTabViewModel document,
        PanelSelectionInfo selection)
    {
        return new ReorderElementMutationCommand(documentId, document, selection, ReorderDirection.SendBackward);
    }

    public static Commands.ICommand CreateSetTransformLockedCommand(
        Guid documentId,
        DocumentTabViewModel document,
        PanelSelectionInfo selection,
        bool isTransformLocked)
    {
        return new SetElementTransformLockStateMutationCommand(documentId, document, selection, isTransformLocked);
    }

    public static Commands.ICommand CreateSetVisibleCommand(
        Guid documentId,
        DocumentTabViewModel document,
        PanelSelectionInfo selection,
        bool isVisible)
    {
        return new SetElementVisibilityMutationCommand(documentId, document, selection, isVisible);
    }

    public static Commands.ICommand CreateUpdateElementCommand(
        Guid documentId,
        DocumentTabViewModel document,
        string objectId,
        PanelElementModel updatedElement,
        string description)
    {
        return new UpdateElementMutationCommand(documentId, document, objectId, updatedElement, description);
    }

    public static Commands.ICommand CreateUpdateElementCommand(
        Guid documentId,
        DocumentTabViewModel document,
        string objectId,
        PanelElementModel updatedElement,
        PanelElementModel originalElement,
        string description)
    {
        return new UpdateElementMutationCommand(documentId, document, objectId, updatedElement, description, originalElement);
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
            _document.SetPanelElements(elements, CreateStructureChange(_document, elementModel.ObjectId));
            _document.HierarchySelectedPanelSelection = PanelSelectionContract.ToSelectionInfo(Panel2DDocumentStorage.ToStorageElement(elementModel));
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
                _document.SetPanelElements(elements, CreateStructureChange(_document));
                if (_document.HierarchySelectedPanelSelection is PanelSelectionInfo selection
                    && IsSelectionForElement(selection, _element))
                {
                    _document.HierarchySelectedPanelSelection = null;
                }

                _document.MarkDirty();
            }
        }
    }

    private static bool IsSelectionForElement(PanelSelectionInfo selection, PanelElementFile element)
    {
        return PanelSelectionContract.IsMatch(element, selection);
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
            _document.SetPanelElements(elements, CreateStructureChange(_document, _deletedElement?.ObjectId));
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
            _document.SetPanelElements(elements, CreateStructureChange(_document, _deletedElement.ObjectId));
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
            elements[index] = PanelElementModelCloner.Clone(existing, name: normalizedNewName);

            _document.SetPanelElements(elements, CreateElementChange(_document, existing.ObjectId, PanelChangeProperties.Name, affectsHierarchy: true));
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

            if (!PanelSelectionContract.IsMatch(Panel2DDocumentStorage.ToStorageElement(elements[index]), _selection))
            {
                return;
            }

            var existing = elements[index];
            elements[index] = PanelElementModelCloner.Clone(existing, name: _previousName ?? string.Empty);

            _document.SetPanelElements(elements, CreateElementChange(_document, existing.ObjectId, PanelChangeProperties.Name, affectsHierarchy: true));
            _document.MarkDirty();
        }
    }

    private sealed class DuplicateElementMutationCommand : Commands.IDocumentCommand, Commands.IExecutionTrackedCommand
    {
        private const double DuplicateOffset = 10.0;
        private readonly Guid _documentId;
        private readonly DocumentTabViewModel _document;
        private readonly PanelSelectionInfo _selection;
        private PanelElementModel? _duplicatedElement;
        private int? _insertIndex;

        public DuplicateElementMutationCommand(Guid documentId, DocumentTabViewModel document, PanelSelectionInfo selection)
        {
            _documentId = documentId;
            _document = document;
            _selection = selection;
        }

        public Guid DocumentId => _documentId;

        public string Description => "Duplicate element";

        public bool WasExecuted { get; private set; }

        public void Execute()
        {
            WasExecuted = false;
            var elements = _document.GetPanelElements().ToList();

            if (_duplicatedElement is null)
            {
                if (!TryFindMatchingElementIndex(elements, _selection, out var sourceIndex))
                {
                    return;
                }

                var sourceElement = elements[sourceIndex];
                _duplicatedElement = PanelElementModelCloner.Clone(
                    sourceElement,
                    objectId: BuildUniqueObjectId(elements),
                    name: BuildDuplicateName(sourceElement, elements),
                    x: sourceElement.X + DuplicateOffset,
                    y: sourceElement.Y + DuplicateOffset);
                _insertIndex = Math.Clamp(sourceIndex + 1, 0, elements.Count);
            }

            var duplicate = _duplicatedElement;
            if (duplicate is null || elements.Any(element => string.Equals(element.ObjectId, duplicate.ObjectId, StringComparison.Ordinal)))
            {
                return;
            }

            var insertIndex = Math.Clamp(_insertIndex ?? elements.Count, 0, elements.Count);
            elements.Insert(insertIndex, duplicate);
            _document.SetPanelElements(elements, CreateStructureChange(_document, duplicate.ObjectId));
            _document.MarkDirty();
            WasExecuted = true;
        }

        public void Undo()
        {
            var duplicatedElement = _duplicatedElement;
            if (duplicatedElement is null)
            {
                return;
            }

            var elements = _document.GetPanelElements().ToList();
            var removed = elements.RemoveAll(element => string.Equals(element.ObjectId, duplicatedElement.ObjectId, StringComparison.Ordinal)) > 0;
            if (!removed)
            {
                return;
            }

            _document.SetPanelElements(elements, CreateStructureChange(_document, duplicatedElement.ObjectId));
            _document.MarkDirty();
        }
    }

    private sealed class PasteElementMutationCommand : Commands.IDocumentCommand, Commands.IExecutionTrackedCommand
    {
        private const double PasteOffset = 10.0;
        private readonly Guid _documentId;
        private readonly DocumentTabViewModel _document;
        private readonly PanelElementModel _sourceElement;
        private PanelElementModel? _pastedElement;
        private int? _insertIndex;

        public PasteElementMutationCommand(Guid documentId, DocumentTabViewModel document, PanelElementModel sourceElement)
        {
            _documentId = documentId;
            _document = document;
            _sourceElement = sourceElement;
        }

        public Guid DocumentId => _documentId;

        public string Description => "Paste element";

        public bool WasExecuted { get; private set; }

        public void Execute()
        {
            WasExecuted = false;
            var elements = _document.GetPanelElements().ToList();

            if (_pastedElement is null)
            {
                _pastedElement = PanelElementModelCloner.Clone(
                    _sourceElement,
                    objectId: BuildUniqueObjectId(elements),
                    name: BuildUniqueName(_sourceElement.Name, elements),
                    x: _sourceElement.X + PasteOffset,
                    y: _sourceElement.Y + PasteOffset);
                _insertIndex = elements.Count;
            }

            var pastedElement = _pastedElement;
            if (pastedElement is null || elements.Any(element => string.Equals(element.ObjectId, pastedElement.ObjectId, StringComparison.Ordinal)))
            {
                return;
            }

            var insertIndex = Math.Clamp(_insertIndex ?? elements.Count, 0, elements.Count);
            elements.Insert(insertIndex, pastedElement);
            _document.SetPanelElements(elements, CreateStructureChange(_document, pastedElement.ObjectId));
            _document.MarkDirty();
            WasExecuted = true;
        }

        public void Undo()
        {
            var pastedElement = _pastedElement;
            if (pastedElement is null)
            {
                return;
            }

            var elements = _document.GetPanelElements().ToList();
            var removed = elements.RemoveAll(element => string.Equals(element.ObjectId, pastedElement.ObjectId, StringComparison.Ordinal)) > 0;
            if (!removed)
            {
                return;
            }

            _document.SetPanelElements(elements, CreateStructureChange(_document, pastedElement.ObjectId));
            _document.MarkDirty();
        }
    }

    private sealed class ReorderElementMutationCommand : Commands.IDocumentCommand, Commands.IExecutionTrackedCommand
    {
        private readonly Guid _documentId;
        private readonly DocumentTabViewModel _document;
        private readonly PanelSelectionInfo _selection;
        private readonly ReorderDirection _direction;
        private int? _fromIndex;
        private string? _objectId;

        public ReorderElementMutationCommand(
            Guid documentId,
            DocumentTabViewModel document,
            PanelSelectionInfo selection,
            ReorderDirection direction)
        {
            _documentId = documentId;
            _document = document;
            _selection = selection;
            _direction = direction;
        }

        public Guid DocumentId => _documentId;

        public string Description => _direction switch
        {
            ReorderDirection.BringToFront => "Bring to front",
            ReorderDirection.SendToBack => "Send to back",
            ReorderDirection.BringForward => "Bring forward",
            ReorderDirection.SendBackward => "Send backward",
            _ => "Reorder element"
        };

        public bool WasExecuted { get; private set; }

        public void Execute()
        {
            WasExecuted = false;
            var elements = _document.GetPanelElements().ToList();
            if (!TryFindMatchingElementIndex(elements, _selection, out var fromIndex))
            {
                return;
            }

            if (!CanReorder(fromIndex, elements.Count, _direction))
            {
                return;
            }

            var selected = elements[fromIndex];
            elements.RemoveAt(fromIndex);
            var insertIndex = ResolveInsertIndex(fromIndex, elements.Count + 1, _direction);
            elements.Insert(insertIndex, selected);

            _fromIndex = fromIndex;
            _objectId = selected.ObjectId;
            _document.SetPanelElements(elements, CreateElementChange(_document, selected.ObjectId, PanelChangeProperties.Ordering, affectsHierarchy: true));
            _document.MarkDirty();
            WasExecuted = true;
        }

        public void Undo()
        {
            if (_fromIndex is not int fromIndex || string.IsNullOrWhiteSpace(_objectId))
            {
                return;
            }

            var elements = _document.GetPanelElements().ToList();
            var currentIndex = elements.FindIndex(element => string.Equals(element.ObjectId, _objectId, StringComparison.Ordinal));
            if (currentIndex < 0)
            {
                return;
            }

            var selected = elements[currentIndex];
            elements.RemoveAt(currentIndex);
            var restoreIndex = Math.Clamp(fromIndex, 0, elements.Count);
            elements.Insert(restoreIndex, selected);
            _document.SetPanelElements(elements, CreateElementChange(_document, selected.ObjectId, PanelChangeProperties.Ordering, affectsHierarchy: true));
            _document.MarkDirty();
        }
    }

    private sealed class SetElementTransformLockStateMutationCommand : Commands.IDocumentCommand, Commands.IExecutionTrackedCommand
    {
        private readonly Guid _documentId;
        private readonly DocumentTabViewModel _document;
        private readonly PanelSelectionInfo _selection;
        private readonly bool _isTransformLocked;
        private int? _updatedIndex;
        private bool _previousValue;

        public SetElementTransformLockStateMutationCommand(
            Guid documentId,
            DocumentTabViewModel document,
            PanelSelectionInfo selection,
            bool isTransformLocked)
        {
            _documentId = documentId;
            _document = document;
            _selection = selection;
            _isTransformLocked = isTransformLocked;
        }

        public Guid DocumentId => _documentId;

        public string Description => _isTransformLocked ? "Lock transform" : "Unlock transform";

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
            if (existing.IsTransformLocked == _isTransformLocked)
            {
                return;
            }

            _updatedIndex = index;
            _previousValue = existing.IsTransformLocked;
            elements[index] = PanelElementModelCloner.Clone(existing, isTransformLocked: _isTransformLocked);
            _document.SetPanelElements(elements, CreateElementChange(_document, existing.ObjectId, PanelChangeProperties.TransformLockState, affectsHierarchy: true));
            _document.MarkDirty();
            WasExecuted = true;
        }

        public void Undo()
        {
            if (_updatedIndex is not int index)
            {
                return;
            }

            var elements = _document.GetPanelElements().ToList();
            if (index < 0 || index >= elements.Count)
            {
                return;
            }

            if (!PanelSelectionContract.IsMatch(Panel2DDocumentStorage.ToStorageElement(elements[index]), _selection))
            {
                return;
            }

            elements[index] = PanelElementModelCloner.Clone(elements[index], isTransformLocked: _previousValue);
            _document.SetPanelElements(elements, CreateElementChange(_document, elements[index].ObjectId, PanelChangeProperties.TransformLockState, affectsHierarchy: true));
            _document.MarkDirty();
        }
    }

    private sealed class SetElementVisibilityMutationCommand : Commands.IDocumentCommand, Commands.IExecutionTrackedCommand
    {
        private readonly Guid _documentId;
        private readonly DocumentTabViewModel _document;
        private readonly PanelSelectionInfo _selection;
        private readonly bool _isVisible;
        private int? _updatedIndex;
        private bool _previousValue;

        public SetElementVisibilityMutationCommand(
            Guid documentId,
            DocumentTabViewModel document,
            PanelSelectionInfo selection,
            bool isVisible)
        {
            _documentId = documentId;
            _document = document;
            _selection = selection;
            _isVisible = isVisible;
        }

        public Guid DocumentId => _documentId;

        public string Description => _isVisible ? "Show element" : "Hide element";

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
            if (existing.IsVisible == _isVisible)
            {
                return;
            }

            _updatedIndex = index;
            _previousValue = existing.IsVisible;
            elements[index] = PanelElementModelCloner.Clone(existing, isVisible: _isVisible);
            _document.SetPanelElements(elements, CreateElementChange(_document, existing.ObjectId, PanelChangeProperties.Visibility, affectsHierarchy: true));
            _document.MarkDirty();
            WasExecuted = true;
        }

        public void Undo()
        {
            if (_updatedIndex is not int index)
            {
                return;
            }

            var elements = _document.GetPanelElements().ToList();
            if (index < 0 || index >= elements.Count)
            {
                return;
            }

            if (!PanelSelectionContract.IsMatch(Panel2DDocumentStorage.ToStorageElement(elements[index]), _selection))
            {
                return;
            }

            elements[index] = PanelElementModelCloner.Clone(elements[index], isVisible: _previousValue);
            _document.SetPanelElements(elements, CreateElementChange(_document, elements[index].ObjectId, PanelChangeProperties.Visibility, affectsHierarchy: true));
            _document.MarkDirty();
        }
    }

    private sealed class UpdateElementMutationCommand : Commands.IDocumentCommand, Commands.IExecutionTrackedCommand
    {
        private readonly Guid _documentId;
        private readonly DocumentTabViewModel _document;
        private readonly string _objectId;
        private readonly PanelElementModel _updatedElement;
        private readonly string _description;
        private readonly PanelElementModel? _previewOriginalElement;
        private PanelElementModel? _previousElement;

        public UpdateElementMutationCommand(
            Guid documentId,
            DocumentTabViewModel document,
            string objectId,
            PanelElementModel updatedElement,
            string description,
            PanelElementModel? previewOriginalElement = null)
        {
            _documentId = documentId;
            _document = document;
            _objectId = objectId;
            _updatedElement = updatedElement;
            _previewOriginalElement = previewOriginalElement is null
                ? null
                : PanelElementModelCloner.Clone(previewOriginalElement);
            _description = string.IsNullOrWhiteSpace(description)
                ? "Update element"
                : description;
        }

        public Guid DocumentId => _documentId;

        public string Description => _description;

        public bool WasExecuted { get; private set; }

        public void Execute()
        {
            WasExecuted = false;
            if (string.IsNullOrWhiteSpace(_objectId))
            {
                return;
            }

            var elements = _document.GetPanelElements().ToList();
            var index = elements.FindIndex(element => string.Equals(element.ObjectId, _objectId, StringComparison.Ordinal));
            if (index < 0)
            {
                return;
            }

            var existing = elements[index];
            if (!string.Equals(_updatedElement.ObjectId, _objectId, StringComparison.Ordinal)
                || _updatedElement.Kind != existing.Kind
                || !PanelElementValidation.IsValidForInspectorUpdate(_updatedElement))
            {
                return;
            }

            if (PanelElementModelComparer.AreEquivalent(existing, _updatedElement))
            {
                if (_previewOriginalElement is null
                    || !string.Equals(_previewOriginalElement.ObjectId, _objectId, StringComparison.Ordinal)
                    || _previewOriginalElement.Kind != existing.Kind
                    || !PanelElementValidation.IsValidForInspectorUpdate(_previewOriginalElement)
                    || PanelElementModelComparer.AreEquivalent(_previewOriginalElement, _updatedElement))
                {
                    return;
                }

                _previousElement = PanelElementModelCloner.Clone(_previewOriginalElement);
                _document.MarkDirty();
                WasExecuted = true;
                return;
            }

            _previousElement = IsValidPreviewOriginal(existing)
                ? PanelElementModelCloner.Clone(_previewOriginalElement!)
                : PanelElementModelCloner.Clone(existing);
            elements[index] = PanelElementModelCloner.Clone(_updatedElement);
            var changedProperties = GetChangedProperties(existing, _updatedElement);
            var affectsHierarchy = changedProperties.HasFlag(PanelChangeProperties.Name)
                || changedProperties.HasFlag(PanelChangeProperties.Visibility)
                || changedProperties.HasFlag(PanelChangeProperties.TransformLockState)
                || changedProperties.HasFlag(PanelChangeProperties.Ordering);

            _document.SetPanelElements(elements, CreateElementChange(_document, _objectId, changedProperties, affectsHierarchy: affectsHierarchy));
            _document.MarkDirty();
            WasExecuted = true;
        }

        private bool IsValidPreviewOriginal(PanelElementModel existing)
        {
            return _previewOriginalElement is not null
                   && string.Equals(_previewOriginalElement.ObjectId, _objectId, StringComparison.Ordinal)
                   && _previewOriginalElement.Kind == existing.Kind
                   && PanelElementValidation.IsValidForInspectorUpdate(_previewOriginalElement)
                   && !PanelElementModelComparer.AreEquivalent(_previewOriginalElement, _updatedElement);
        }

        public void Undo()
        {
            if (_previousElement is null || string.IsNullOrWhiteSpace(_objectId))
            {
                return;
            }

            var elements = _document.GetPanelElements().ToList();
            var index = elements.FindIndex(element => string.Equals(element.ObjectId, _objectId, StringComparison.Ordinal));
            if (index < 0)
            {
                return;
            }

            if (PanelElementModelComparer.AreEquivalent(elements[index], _previousElement))
            {
                return;
            }

            var current = elements[index];
            var changedProperties = GetChangedProperties(current, _previousElement);
            var affectsHierarchy = changedProperties.HasFlag(PanelChangeProperties.Name)
                || changedProperties.HasFlag(PanelChangeProperties.Visibility)
                || changedProperties.HasFlag(PanelChangeProperties.TransformLockState)
                || changedProperties.HasFlag(PanelChangeProperties.Ordering);

            elements[index] = PanelElementModelCloner.Clone(_previousElement);
            _document.SetPanelElements(elements, CreateElementChange(_document, _objectId, changedProperties, affectsHierarchy: affectsHierarchy));
            _document.MarkDirty();
        }

        private static PanelChangeProperties GetChangedProperties(PanelElementModel before, PanelElementModel after)
        {
            var changed = PanelChangeProperties.None;

            if (!string.Equals(before.Name, after.Name, StringComparison.Ordinal))
            {
                changed |= PanelChangeProperties.Name;
            }

            if (before.X != after.X || before.Y != after.Y || before.Width != after.Width || before.Height != after.Height)
            {
                changed |= PanelChangeProperties.Geometry;
            }

            if (before.IsVisible != after.IsVisible)
            {
                changed |= PanelChangeProperties.Visibility;
            }

            if (before.IsTransformLocked != after.IsTransformLocked)
            {
                changed |= PanelChangeProperties.TransformLockState;
            }

            if (!string.Equals(before.OnColorHex, after.OnColorHex, StringComparison.Ordinal)
                || !string.Equals(before.OffColorHex, after.OffColorHex, StringComparison.Ordinal)
                || !string.Equals(before.TextColorHex, after.TextColorHex, StringComparison.Ordinal))
            {
                changed |= PanelChangeProperties.Style;
            }

            if (!string.Equals(before.AssetPath, after.AssetPath, StringComparison.Ordinal)
                || !string.Equals(before.SecondaryAssetPath, after.SecondaryAssetPath, StringComparison.Ordinal)
                || before.DisplayNumber != after.DisplayNumber
                || before.LampNumber != after.LampNumber
                || !string.Equals(before.DisplayText, after.DisplayText, StringComparison.Ordinal)
                || !string.Equals(before.TextBoxFontName, after.TextBoxFontName, StringComparison.Ordinal)
                || !string.Equals(before.TextBoxFontStyle, after.TextBoxFontStyle, StringComparison.Ordinal)
                || !string.Equals(before.TextBoxFontSize, after.TextBoxFontSize, StringComparison.Ordinal)
                || before.IsReversed != after.IsReversed
                || before.Stops != after.Stops
                || before.VisibleScale != after.VisibleScale
                || before.BandOffset != after.BandOffset)
            {
                changed |= PanelChangeProperties.Metadata;
            }

            return changed is PanelChangeProperties.None
                ? PanelChangeProperties.Metadata
                : changed;
        }
    }


    private static PanelChangeEvent CreateElementChange(DocumentTabViewModel document, string? objectId, PanelChangeProperties properties, bool affectsHierarchy = false, bool affectsInspectorRows = true)
    {
        return new PanelChangeEvent(document.DocumentId, objectId, properties, AffectsCanvas: true, AffectsHierarchy: affectsHierarchy, AffectsInspectorRows: affectsInspectorRows, AffectsPersistence: true);
    }

    private static PanelChangeEvent CreateStructureChange(DocumentTabViewModel document, string? objectId = null)
    {
        return new PanelChangeEvent(document.DocumentId, objectId, PanelChangeProperties.Structure | PanelChangeProperties.Ordering, AffectsCanvas: true, AffectsHierarchy: true, AffectsInspectorRows: true, AffectsPersistence: true);
    }

    private static bool TryFindMatchingElementIndex(IReadOnlyList<PanelElementModel> elements, PanelSelectionInfo selection, out int index)
    {
        var hasObjectId = !string.IsNullOrWhiteSpace(selection.ObjectId);
        if (hasObjectId)
        {
            for (var i = 0; i < elements.Count; i++)
            {
                if (string.Equals(elements[i].ObjectId, selection.ObjectId, StringComparison.Ordinal))
                {
                    index = i;
                    return true;
                }
            }

            index = -1;
            return false;
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
        return PanelSelectionContract.IsMatch(Panel2DDocumentStorage.ToStorageElement(element), selection);
    }

    private static string BuildUniqueObjectId(IReadOnlyList<PanelElementModel> elements)
    {
        var existingIds = new HashSet<string>(elements.Select(element => element.ObjectId), StringComparer.Ordinal);
        string candidate;
        do
        {
            candidate = Guid.NewGuid().ToString("N");
        } while (existingIds.Contains(candidate));

        return candidate;
    }

    private static string BuildDuplicateName(PanelElementModel sourceElement, IReadOnlyList<PanelElementModel> elements)
    {
        var baseName = string.IsNullOrWhiteSpace(sourceElement.Name)
            ? Panel2DDocumentStorage.SerializeElementKind(sourceElement.Kind)
            : sourceElement.Name.Trim();

        var copyBase = $"{baseName} Copy";
        var existingNames = new HashSet<string>(
            elements.Select(element => element.Name.Trim()),
            StringComparer.OrdinalIgnoreCase);
        if (!existingNames.Contains(copyBase))
        {
            return copyBase;
        }

        var suffix = 2;
        while (true)
        {
            var candidate = $"{copyBase} {suffix}";
            if (!existingNames.Contains(candidate))
            {
                return candidate;
            }

            suffix++;
        }
    }

    private static string BuildUniqueName(string sourceName, IReadOnlyList<PanelElementModel> elements)
    {
        var baseName = string.IsNullOrWhiteSpace(sourceName)
            ? "Element"
            : sourceName.Trim();
        var existingNames = new HashSet<string>(
            elements.Select(element => element.Name.Trim()),
            StringComparer.OrdinalIgnoreCase);
        if (!existingNames.Contains(baseName))
        {
            return baseName;
        }

        var suffix = 2;
        while (true)
        {
            var candidate = $"{baseName} ({suffix})";
            if (!existingNames.Contains(candidate))
            {
                return candidate;
            }

            suffix++;
        }
    }

    private static bool CanReorder(int currentIndex, int count, ReorderDirection direction)
    {
        return direction switch
        {
            ReorderDirection.BringToFront => currentIndex < count - 1,
            ReorderDirection.SendToBack => currentIndex > 0,
            ReorderDirection.BringForward => currentIndex < count - 1,
            ReorderDirection.SendBackward => currentIndex > 0,
            _ => false
        };
    }

    private static int ResolveInsertIndex(int currentIndex, int originalCount, ReorderDirection direction)
    {
        return direction switch
        {
            ReorderDirection.BringToFront => originalCount - 1,
            ReorderDirection.SendToBack => 0,
            ReorderDirection.BringForward => Math.Min(currentIndex + 1, originalCount - 1),
            ReorderDirection.SendBackward => Math.Max(currentIndex - 1, 0),
            _ => currentIndex
        };
    }

    private enum ReorderDirection
    {
        BringToFront,
        SendToBack,
        BringForward,
        SendBackward
    }
}
