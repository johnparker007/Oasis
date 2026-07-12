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


    public static Commands.ICommand CreateBulkUpdateElementsCommand(
        Guid documentId,
        DocumentTabViewModel document,
        IReadOnlyDictionary<string, FaceElementModel> updatedElements,
        IReadOnlyDictionary<string, FaceElementModel> originalElements,
        string description)
    {
        return new BulkUpdateFaceElementsMutationCommand(documentId, document, updatedElements, originalElements, description);
    }

    public static Commands.ICommand CreateAssignCabinetFaceTargetCommand(
        Guid documentId,
        DocumentTabViewModel document,
        string? assignedTargetId)
    {
        return new AssignCabinetFaceTargetMutationCommand(documentId, document, NormalizeTargetId(assignedTargetId));
    }

    private static PanelChangeEvent CreateChange(DocumentTabViewModel document, string? objectId, PanelChangeProperties properties, bool structure = false)
    {
        return new PanelChangeEvent(
            document.DocumentId,
            objectId,
            properties,
            AffectsCanvas: true,
            AffectsHierarchy: structure || properties.HasFlag(PanelChangeProperties.Name) || properties.HasFlag(PanelChangeProperties.Visibility) || properties.HasFlag(PanelChangeProperties.TransformLockState),
            AffectsInspectorRows: true,
            AffectsPersistence: true);
    }

    private static string? NormalizeTargetId(string? targetId) => string.IsNullOrWhiteSpace(targetId) ? null : targetId.Trim();

    private static FaceDocumentModel WithAssignedCabinetFaceTarget(FaceDocumentModel faceDocument, string? assignedTargetId)
    {
        return new FaceDocumentModel
        {
            Id = faceDocument.Id,
            Title = faceDocument.Title,
            Summary = faceDocument.Summary,
            SourcePanel2DDocumentId = faceDocument.SourcePanel2DDocumentId,
            SourcePanel2DDocumentPath = faceDocument.SourcePanel2DDocumentPath,
            SourceFaceShapeId = faceDocument.SourceFaceShapeId,
            AssignedCabinetFaceTargetId = NormalizeTargetId(assignedTargetId),
            SourceRegion = faceDocument.SourceRegion,
            LastRegeneratedAtUtc = faceDocument.LastRegeneratedAtUtc,
            GenerationSettings = faceDocument.GenerationSettings,
            RuntimeRenderAssets = faceDocument.RuntimeRenderAssets,
            MaskLayer = faceDocument.MaskLayer,
            Trays = faceDocument.Trays,
            LampEmitters = faceDocument.LampEmitters,
            Layers = faceDocument.Layers,
            Elements = faceDocument.Elements
        };
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
            _document.SetFaceElements(elements, CreateChange(_document, _objectId, PanelChangeProperties.Geometry | PanelChangeProperties.Name | PanelChangeProperties.Visibility | PanelChangeProperties.TransformLockState | PanelChangeProperties.Metadata));
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
            _document.SetFaceElements(elements, CreateChange(_document, _objectId, PanelChangeProperties.Geometry | PanelChangeProperties.Name | PanelChangeProperties.Visibility | PanelChangeProperties.TransformLockState | PanelChangeProperties.Metadata));
            _document.HierarchySelectedPanelSelection = FaceSelectionService.ToSelectionInfo(_originalElement);
            _document.MarkDirty();
        }
    }

    private sealed class BulkUpdateFaceElementsMutationCommand : Commands.IDocumentCommand, Commands.IExecutionTrackedCommand
    {
        private readonly Guid _documentId;
        private readonly DocumentTabViewModel _document;
        private readonly Dictionary<string, FaceElementModel> _updatedElements;
        private readonly Dictionary<string, FaceElementModel> _originalElements;
        private readonly string _description;
        private Dictionary<string, FaceElementModel>? _previousElements;

        public BulkUpdateFaceElementsMutationCommand(Guid documentId, DocumentTabViewModel document, IReadOnlyDictionary<string, FaceElementModel> updatedElements, IReadOnlyDictionary<string, FaceElementModel> originalElements, string description)
        {
            _documentId = documentId;
            _document = document;
            _updatedElements = updatedElements.ToDictionary(pair => pair.Key, pair => FaceElementModelCloner.Clone(pair.Value));
            _originalElements = originalElements.ToDictionary(pair => pair.Key, pair => FaceElementModelCloner.Clone(pair.Value));
            _description = string.IsNullOrWhiteSpace(description) ? "Update face elements" : description;
        }

        public Guid DocumentId => _documentId;
        public string Description => _description;
        public bool WasExecuted { get; private set; }

        public void Execute()
        {
            WasExecuted = false;
            var elements = _document.GetFaceElements().ToList();
            var previous = new Dictionary<string, FaceElementModel>();
            var logicalChanged = false;

            for (var i = 0; i < elements.Count; i++)
            {
                var existing = elements[i];
                if (string.IsNullOrWhiteSpace(existing.ObjectId) || !_updatedElements.TryGetValue(existing.ObjectId, out var updated))
                {
                    continue;
                }

                if (updated.GetType() != existing.GetType() || !FaceElementValidation.IsValidForInspectorUpdate(updated))
                {
                    continue;
                }

                var previousElement = _originalElements.TryGetValue(existing.ObjectId, out var original)
                    && original.GetType() == existing.GetType()
                    && FaceElementValidation.IsValidForInspectorUpdate(original)
                        ? FaceElementModelCloner.Clone(original)
                        : FaceElementModelCloner.Clone(existing);
                previous[existing.ObjectId] = previousElement;

                if (!FaceElementModelComparer.AreEquivalent(previousElement, updated))
                {
                    logicalChanged = true;
                }

                if (!FaceElementModelComparer.AreEquivalent(existing, updated))
                {
                    elements[i] = FaceElementModelCloner.Clone(updated);
                }
            }

            if (previous.Count == 0 || !logicalChanged)
            {
                return;
            }

            _previousElements = previous;
            _document.SetFaceElements(elements, CreateChange(_document, null, PanelChangeProperties.Geometry | PanelChangeProperties.Name | PanelChangeProperties.Visibility | PanelChangeProperties.TransformLockState | PanelChangeProperties.Metadata));
            _document.MarkDirty();
            WasExecuted = true;
        }

        public void Undo()
        {
            if (_previousElements is null || _previousElements.Count == 0)
            {
                return;
            }

            var elements = _document.GetFaceElements().ToList();
            var changed = false;
            foreach (var previous in _previousElements)
            {
                var index = elements.FindIndex(element => string.Equals(element.ObjectId, previous.Key, StringComparison.Ordinal));
                if (index >= 0 && !FaceElementModelComparer.AreEquivalent(elements[index], previous.Value))
                {
                    elements[index] = FaceElementModelCloner.Clone(previous.Value);
                    changed = true;
                }
            }

            if (changed)
            {
                _document.SetFaceElements(elements, CreateChange(_document, null, PanelChangeProperties.Geometry | PanelChangeProperties.Name | PanelChangeProperties.Visibility | PanelChangeProperties.TransformLockState | PanelChangeProperties.Metadata));
                _document.MarkDirty();
            }
        }
    }

    private sealed class AssignCabinetFaceTargetMutationCommand : Commands.IDocumentCommand, Commands.IExecutionTrackedCommand
    {
        private readonly Guid _documentId;
        private readonly DocumentTabViewModel _document;
        private readonly string? _assignedTargetId;
        private string? _originalTargetId;

        public AssignCabinetFaceTargetMutationCommand(Guid documentId, DocumentTabViewModel document, string? assignedTargetId)
        {
            _documentId = documentId;
            _document = document;
            _assignedTargetId = assignedTargetId;
        }

        public Guid DocumentId => _documentId;
        public string Description => "Assign cabinet face target";
        public bool WasExecuted { get; private set; }

        public void Execute()
        {
            WasExecuted = false;
            var faceDocument = _document.GetFaceDocument();
            var currentTargetId = NormalizeTargetId(faceDocument.AssignedCabinetFaceTargetId);
            if (string.Equals(currentTargetId, _assignedTargetId, StringComparison.Ordinal))
            {
                return;
            }

            _originalTargetId ??= currentTargetId;
            _document.SetFaceDocument(
                WithAssignedCabinetFaceTarget(faceDocument, _assignedTargetId),
                CreateChange(_document, null, PanelChangeProperties.Metadata));
            _document.MarkDirty();
            WasExecuted = true;
        }

        public void Undo()
        {
            var faceDocument = _document.GetFaceDocument();
            _document.SetFaceDocument(
                WithAssignedCabinetFaceTarget(faceDocument, _originalTargetId),
                CreateChange(_document, null, PanelChangeProperties.Metadata));
            _document.MarkDirty();
        }
    }
}
