namespace OasisEditor;

internal static class FaceMutationCommands
{
    public static Commands.ICommand CreateAddLampWindowCommand(Guid documentId, DocumentTabViewModel document, FaceLampWindowElement element)
    {
        return new AddFaceElementMutationCommand(documentId, document, element);
    }

    public static Commands.ICommand CreateUpdateElementCommand(
        Guid documentId,
        DocumentTabViewModel document,
        string objectId,
        FaceElementModel updatedElement,
        string description)
    {
        return new UpdateFaceElementMutationCommand(documentId, document, objectId, updatedElement, description);
    }

    private static PanelChangeEvent CreateChange(DocumentTabViewModel document, string? objectId, PanelChangeProperties properties, bool structure = false)
    {
        return new PanelChangeEvent(
            document.DocumentId,
            objectId,
            properties,
            AffectsCanvas: true,
            AffectsHierarchy: structure || properties.HasFlag(PanelChangeProperties.Name) || properties.HasFlag(PanelChangeProperties.Visibility) || properties.HasFlag(PanelChangeProperties.LockState),
            AffectsInspectorRows: true,
            AffectsPersistence: true);
    }

    private sealed class AddFaceElementMutationCommand : Commands.IDocumentCommand, Commands.IExecutionTrackedCommand
    {
        private readonly Guid _documentId;
        private readonly DocumentTabViewModel _document;
        private readonly FaceLampWindowElement _element;
        private int? _insertIndex;

        public AddFaceElementMutationCommand(Guid documentId, DocumentTabViewModel document, FaceLampWindowElement element)
        {
            _documentId = documentId;
            _document = document;
            _element = element;
        }

        public Guid DocumentId => _documentId;
        public string Description => "Add face lamp window";
        public bool WasExecuted { get; private set; }

        public void Execute()
        {
            WasExecuted = false;
            var elements = _document.GetFaceElements().ToList();
            var index = Math.Clamp(_insertIndex ?? elements.Count, 0, elements.Count);
            elements.Insert(index, _element);
            _insertIndex = index;
            _document.SetFaceElements(elements, CreateChange(_document, _element.ObjectId, PanelChangeProperties.Structure, structure: true));
            _document.HierarchySelectedPanelSelection = FaceSelectionService.ToSelectionInfo(_element);
            _document.MarkDirty();
            WasExecuted = true;
        }

        public void Undo()
        {
            var elements = _document.GetFaceElements().ToList();
            var index = elements.FindIndex(element => string.Equals(element.ObjectId, _element.ObjectId, StringComparison.Ordinal));
            if (index < 0)
            {
                return;
            }

            elements.RemoveAt(index);
            _document.SetFaceElements(elements, CreateChange(_document, _element.ObjectId, PanelChangeProperties.Structure, structure: true));
            if (_document.HierarchySelectedPanelSelection is PanelSelectionInfo selection
                && string.Equals(selection.ObjectId, _element.ObjectId, StringComparison.Ordinal))
            {
                _document.HierarchySelectedPanelSelection = null;
            }

            _document.MarkDirty();
        }
    }

    private sealed class UpdateFaceElementMutationCommand : Commands.IDocumentCommand, Commands.IExecutionTrackedCommand
    {
        private readonly Guid _documentId;
        private readonly DocumentTabViewModel _document;
        private readonly string _objectId;
        private readonly FaceElementModel _updatedElement;
        private readonly string _description;
        private FaceElementModel? _originalElement;

        public UpdateFaceElementMutationCommand(Guid documentId, DocumentTabViewModel document, string objectId, FaceElementModel updatedElement, string description)
        {
            _documentId = documentId;
            _document = document;
            _objectId = objectId;
            _updatedElement = updatedElement;
            _description = description;
        }

        public Guid DocumentId => _documentId;
        public string Description => _description;
        public bool WasExecuted { get; private set; }

        public void Execute()
        {
            WasExecuted = false;
            var elements = _document.GetFaceElements().ToList();
            var index = elements.FindIndex(element => string.Equals(element.ObjectId, _objectId, StringComparison.Ordinal));
            if (index < 0)
            {
                return;
            }

            _originalElement ??= elements[index];
            elements[index] = _updatedElement;
            _document.SetFaceElements(elements, CreateChange(_document, _objectId, PanelChangeProperties.Geometry | PanelChangeProperties.Name | PanelChangeProperties.Visibility | PanelChangeProperties.LockState | PanelChangeProperties.Metadata));
            _document.HierarchySelectedPanelSelection = FaceSelectionService.ToSelectionInfo(_updatedElement);
            _document.MarkDirty();
            WasExecuted = true;
        }

        public void Undo()
        {
            if (_originalElement is null)
            {
                return;
            }

            var elements = _document.GetFaceElements().ToList();
            var index = elements.FindIndex(element => string.Equals(element.ObjectId, _objectId, StringComparison.Ordinal));
            if (index < 0)
            {
                return;
            }

            elements[index] = _originalElement;
            _document.SetFaceElements(elements, CreateChange(_document, _objectId, PanelChangeProperties.Geometry | PanelChangeProperties.Name | PanelChangeProperties.Visibility | PanelChangeProperties.LockState | PanelChangeProperties.Metadata));
            _document.HierarchySelectedPanelSelection = FaceSelectionService.ToSelectionInfo(_originalElement);
            _document.MarkDirty();
        }
    }
}
