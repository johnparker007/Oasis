namespace OasisEditor;

internal static class FaceMutationCommands
{
    public static Commands.ICommand CreateSetProcessingPipelineCommand(
        Guid documentId,
        DocumentTabViewModel document,
        ImageProcessingPipelineModel pipeline,
        string description = "Update artwork processing")
    {
        return new SetProcessingPipelineMutationCommand(documentId, document, pipeline, description);
    }

    public static Commands.ICommand CreateAddArtworkCalibrationCommand(Guid documentId, DocumentTabViewModel document)
    {
        var operations = document.GetFaceDocument().Artwork?.ProcessingPipeline.Operations ?? [];
        return CreateSetProcessingPipelineCommand(documentId, document,
            new ImageProcessingPipelineModel { Operations = operations.Append(new ArtworkCalibrationOperationModel()).ToArray() },
            "Add Artwork Calibration");
    }

    public static Commands.ICommand CreateApplyArtworkProcessingCommand(Guid documentId, DocumentTabViewModel document) =>
        new ApplyArtworkProcessingCommand(documentId, document);

    public static Commands.ICommand CreateRemoveProcessingOperationCommand(Guid documentId, DocumentTabViewModel document, string operationId) =>
        TransformPipeline(documentId, document, operationId, "Remove artwork processing operation", (operations, index) => { operations.RemoveAt(index); });

    public static Commands.ICommand CreateMoveProcessingOperationCommand(Guid documentId, DocumentTabViewModel document, string operationId, int offset) =>
        TransformPipeline(documentId, document, operationId, "Reorder artwork processing operations", (operations, index) =>
        {
            var target = Math.Clamp(index + offset, 0, operations.Count - 1);
            if (target == index) return;
            var item = operations[index]; operations.RemoveAt(index); operations.Insert(target, item);
        });

    public static Commands.ICommand CreateUpdateProcessingOperationCommand(Guid documentId, DocumentTabViewModel document, ImageProcessingOperationModel operation, string description) =>
        TransformPipeline(documentId, document, operation.Id, description, (operations, index) => operations[index] = operation);

    private static Commands.ICommand TransformPipeline(Guid documentId, DocumentTabViewModel document, string operationId, string description, Action<List<ImageProcessingOperationModel>, int> transform)
    {
        var operations = (document.GetFaceDocument().Artwork?.ProcessingPipeline.Operations ?? []).ToList();
        var index = operations.FindIndex(operation => string.Equals(operation.Id, operationId, StringComparison.Ordinal));
        if (index >= 0) transform(operations, index);
        return CreateSetProcessingPipelineCommand(documentId, document, new ImageProcessingPipelineModel { Operations = operations }, description);
    }

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
        string? assignedTargetId,
        string? assignedCabinetAssetPath = null)
    {
        return new AssignCabinetFaceTargetMutationCommand(documentId, document, NormalizeTargetId(assignedTargetId), NormalizeAssetPath(assignedCabinetAssetPath));
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
    private static string? NormalizeAssetPath(string? assetPath) => string.IsNullOrWhiteSpace(assetPath) ? null : assetPath.Trim().Replace('\\', '/');

    private static FaceDocumentModel WithAssignedCabinetFaceTarget(FaceDocumentModel faceDocument, string? assignedTargetId, string? assignedCabinetAssetPath)
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
            AssignedCabinetAssetPath = NormalizeAssetPath(assignedCabinetAssetPath),
            SourceRegion = faceDocument.SourceRegion,
            LastRegeneratedAtUtc = faceDocument.LastRegeneratedAtUtc,
            GenerationSettings = faceDocument.GenerationSettings,
            Artwork = faceDocument.Artwork,
            RuntimeRenderAssets = faceDocument.RuntimeRenderAssets,
            MaskLayer = faceDocument.MaskLayer,
            Trays = faceDocument.Trays,
            LampEmitters = faceDocument.LampEmitters,
            Layers = faceDocument.Layers,
            Elements = faceDocument.Elements
        };
    }

    private static FaceDocumentModel WithPipeline(FaceDocumentModel model, ImageProcessingPipelineModel pipeline)
    {
        var artwork = model.Artwork is null ? null : new FaceArtworkModel
        {
            Id = model.Artwork.Id, Source = model.Artwork.Source, ProcessingPipeline = pipeline,
            GeneratedAssetPath = model.Artwork.GeneratedAssetPath, OutputWidth = model.Artwork.OutputWidth, OutputHeight = model.Artwork.OutputHeight
        };
        return new FaceDocumentModel
        {
            Id = model.Id, Title = model.Title, Summary = model.Summary, SourcePanel2DDocumentId = model.SourcePanel2DDocumentId,
            SourcePanel2DDocumentPath = model.SourcePanel2DDocumentPath, SourceFaceShapeId = model.SourceFaceShapeId,
            AssignedCabinetFaceTargetId = model.AssignedCabinetFaceTargetId, AssignedCabinetAssetPath = model.AssignedCabinetAssetPath,
            SourceRegion = model.SourceRegion, LastRegeneratedAtUtc = model.LastRegeneratedAtUtc, GenerationSettings = model.GenerationSettings,
            Artwork = artwork, RuntimeRenderAssets = model.RuntimeRenderAssets, MaskLayer = model.MaskLayer, Trays = model.Trays,
            LampEmitters = model.LampEmitters, Layers = model.Layers, Elements = model.Elements
        };
    }

    private sealed class SetProcessingPipelineMutationCommand : Commands.IDocumentCommand, Commands.IExecutionTrackedCommand
    {
        private readonly Guid _documentId; private readonly DocumentTabViewModel _document; private readonly ImageProcessingPipelineModel _next; private readonly string _description;
        private ImageProcessingPipelineModel? _previous;
        public SetProcessingPipelineMutationCommand(Guid documentId, DocumentTabViewModel document, ImageProcessingPipelineModel next, string description)
        { _documentId = documentId; _document = document; _next = next; _description = description; }
        public Guid DocumentId => _documentId; public string Description => _description; public bool WasExecuted { get; private set; }
        public void Execute()
        {
            WasExecuted = false; var current = _document.GetFaceDocument(); if (current.Artwork is null) return;
            _previous ??= current.Artwork.ProcessingPipeline;
            if (PipelinesEquivalent(_previous, _next)) return;
            var properties = RequiresInspectorRebuild(_previous, _next)
                ? PanelChangeProperties.Metadata | PanelChangeProperties.Structure | PanelChangeProperties.Ordering
                : PanelChangeProperties.Metadata;
            _document.SetFaceDocument(WithPipeline(current, _next), CreateChange(_document, null, properties, structure: properties.HasFlag(PanelChangeProperties.Structure)));
            _document.MarkDirty(); WasExecuted = true;
        }
        public void Undo()
        {
            if (_previous is null) return; var current = _document.GetFaceDocument();
            var properties = RequiresInspectorRebuild(current.Artwork?.ProcessingPipeline ?? new(), _previous)
                ? PanelChangeProperties.Metadata | PanelChangeProperties.Structure | PanelChangeProperties.Ordering
                : PanelChangeProperties.Metadata;
            _document.SetFaceDocument(WithPipeline(current, _previous), CreateChange(_document, null, properties, structure: properties.HasFlag(PanelChangeProperties.Structure)));
            _document.MarkDirty();
        }
    }

    private static bool RequiresInspectorRebuild(ImageProcessingPipelineModel left, ImageProcessingPipelineModel right)
    {
        if (!left.Operations.Select(o=>o.Id).SequenceEqual(right.Operations.Select(o=>o.Id))) return true;
        for(var i=0;i<left.Operations.Count;i++) if(left.Operations[i] is ArtworkCalibrationOperationModel a && right.Operations[i] is ArtworkCalibrationOperationModel b)
            if(a.BlackReference.ManualEnabled!=b.BlackReference.ManualEnabled || a.WhiteReference.ManualEnabled!=b.WhiteReference.ManualEnabled ||
               !a.BlackReference.Samples.Select(x=>x.Id).SequenceEqual(b.BlackReference.Samples.Select(x=>x.Id)) || !a.WhiteReference.Samples.Select(x=>x.Id).SequenceEqual(b.WhiteReference.Samples.Select(x=>x.Id)) ||
               !a.SameColorGroups.Select(g=>(g.Id,g.Name,g.Samples.Count)).SequenceEqual(b.SameColorGroups.Select(g=>(g.Id,g.Name,g.Samples.Count)))) return true;
        return false;
    }

    private sealed class ApplyArtworkProcessingCommand : Commands.IDocumentCommand, Commands.IExecutionTrackedCommand, Commands.IExecutionFailureDiagnostic
    {
        private readonly Guid _documentId;
        private readonly DocumentTabViewModel _document;
        private byte[]? _before;
        private byte[]? _after;

        public ApplyArtworkProcessingCommand(Guid documentId, DocumentTabViewModel document) { _documentId = documentId; _document = document; }
        public Guid DocumentId => _documentId;
        public string Description => "Apply Face Artwork Processing";
        public bool WasExecuted { get; private set; }
        public string? ExecutionFailureMessage { get; private set; }

        public void Execute()
        {
            WasExecuted = false;
            ExecutionFailureMessage = null;
            if (_after is not null)
            {
                WasExecuted = _document.TryRestoreGeneratedArtwork(_after);
                if (!WasExecuted) ExecutionFailureMessage = "The previously applied artwork could not be restored during redo.";
                return;
            }
            if (!_document.TryReadGeneratedArtwork(out var before, out var readBeforeError))
            {
                ExecutionFailureMessage = readBeforeError;
                return;
            }
            if (!_document.TryRebuildFaceArtwork(out var processingError))
            {
                ExecutionFailureMessage = processingError;
                return;
            }
            if (!_document.TryReadGeneratedArtwork(out var after, out var readAfterError))
            {
                ExecutionFailureMessage = readAfterError ?? "Processed artwork could not be read back after Apply.";
                return;
            }
            _before = before;
            _after = after;
            WasExecuted = true;
        }

        public void Undo()
        {
            if (_before is not null) _document.TryRestoreGeneratedArtwork(_before);
        }
    }

    private static bool PipelinesEquivalent(ImageProcessingPipelineModel left, ImageProcessingPipelineModel right)
    {
        if (left.Operations.Count != right.Operations.Count) return false;
        for (var index = 0; index < left.Operations.Count; index++)
        {
            if (left.Operations[index] is not ArtworkCalibrationOperationModel a
                || right.Operations[index] is not ArtworkCalibrationOperationModel b
                || a.Id != b.Id
                || a.Enabled != b.Enabled
                || a.Strength != b.Strength
                || a.CorrectSpatialBrightness != b.CorrectSpatialBrightness
                || a.CorrectSpatialColor != b.CorrectSpatialColor
                || a.NormalizeBlackWhite != b.NormalizeBlackWhite
                || a.NeutralizeWhite != b.NeutralizeWhite
                || !ReferencesEquivalent(a.BlackReference, b.BlackReference)
                || !ReferencesEquivalent(a.WhiteReference, b.WhiteReference)
                || a.SameColorGroups.Count != b.SameColorGroups.Count)
            {
                return false;
            }

            for (var groupIndex = 0; groupIndex < a.SameColorGroups.Count; groupIndex++)
            {
                var leftGroup = a.SameColorGroups[groupIndex];
                var rightGroup = b.SameColorGroups[groupIndex];
                if (leftGroup.Id != rightGroup.Id
                    || leftGroup.Name != rightGroup.Name
                    || !SamplesEquivalent(leftGroup.Samples, rightGroup.Samples))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool ReferencesEquivalent(CalibrationReferenceModel left, CalibrationReferenceModel right) =>
        left.ManualEnabled == right.ManualEnabled
        && left.ManualColor == right.ManualColor
        && SamplesEquivalent(left.Samples, right.Samples);

    private static bool SamplesEquivalent(IReadOnlyList<CalibrationSampleModel> left, IReadOnlyList<CalibrationSampleModel> right) =>
        left.Select(sample => (sample.Id, sample.X, sample.Y, sample.SamplingMode, sample.RadiusNormalized))
            .SequenceEqual(right.Select(sample => (sample.Id, sample.X, sample.Y, sample.SamplingMode, sample.RadiusNormalized)));

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
        private readonly string? _assignedCabinetAssetPath;
        private string? _originalTargetId;
        private string? _originalCabinetAssetPath;

        public AssignCabinetFaceTargetMutationCommand(Guid documentId, DocumentTabViewModel document, string? assignedTargetId, string? assignedCabinetAssetPath)
        {
            _documentId = documentId;
            _document = document;
            _assignedTargetId = assignedTargetId;
            _assignedCabinetAssetPath = assignedCabinetAssetPath;
        }

        public Guid DocumentId => _documentId;
        public string Description => "Assign cabinet face target";
        public bool WasExecuted { get; private set; }

        public void Execute()
        {
            WasExecuted = false;
            var faceDocument = _document.GetFaceDocument();
            var currentTargetId = NormalizeTargetId(faceDocument.AssignedCabinetFaceTargetId);
            var currentCabinetAssetPath = NormalizeAssetPath(faceDocument.AssignedCabinetAssetPath);
            if (string.Equals(currentTargetId, _assignedTargetId, StringComparison.Ordinal)
                && string.Equals(currentCabinetAssetPath, _assignedCabinetAssetPath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _originalTargetId ??= currentTargetId;
            _originalCabinetAssetPath ??= currentCabinetAssetPath;
            _document.SetFaceDocument(
                WithAssignedCabinetFaceTarget(faceDocument, _assignedTargetId, _assignedCabinetAssetPath),
                CreateChange(_document, null, PanelChangeProperties.Metadata));
            _document.MarkDirty();
            WasExecuted = true;
        }

        public void Undo()
        {
            var faceDocument = _document.GetFaceDocument();
            _document.SetFaceDocument(
                WithAssignedCabinetFaceTarget(faceDocument, _originalTargetId, _originalCabinetAssetPath),
                CreateChange(_document, null, PanelChangeProperties.Metadata));
            _document.MarkDirty();
        }
    }
}
