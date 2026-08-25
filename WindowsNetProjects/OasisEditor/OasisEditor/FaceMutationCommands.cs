namespace OasisEditor;

internal static class FaceMutationCommands
{
    public static Commands.ICommand CreateSetGenerationSettingsCommand(Guid documentId, DocumentTabViewModel document,
        FaceGenerationSettingsModel settings) => new SetGenerationSettingsCommand(documentId, document, settings.Normalize());

    private sealed class SetGenerationSettingsCommand : Commands.IDocumentCommand, Commands.IExecutionTrackedCommand
    {
        private readonly Guid _id; private readonly DocumentTabViewModel _document; private readonly FaceGenerationSettingsModel _next;
        private FaceGenerationSettingsModel? _previous;
        public SetGenerationSettingsCommand(Guid id, DocumentTabViewModel document, FaceGenerationSettingsModel next)
        { _id=id; _document=document; _next=next; }
        public Guid DocumentId=>_id; public string Description=>"Update Face generation settings"; public bool WasExecuted{get;private set;}
        public void Execute()
        {
            var face=_document.GetFaceDocument(); _previous ??= face.GenerationSettings;
            if (_previous == _next) return;
            _document.SetFaceDocument(FaceDocumentCopy.WithGenerationSettings(face,_next));
            _document.InvalidateFaceBuild(FaceBuildInput.ArtworkProcessing);
            _document.InvalidateFaceBuild(FaceBuildInput.MaskSettings);
            _document.InvalidateFaceBuild(FaceBuildInput.TraySettings);
            _document.MarkDirty(); WasExecuted=true;
        }
        public void Undo()
        {
            if(_previous is null)return;
            _document.SetFaceDocument(FaceDocumentCopy.WithGenerationSettings(_document.GetFaceDocument(),_previous));
            _document.InvalidateFaceBuild(FaceBuildInput.ArtworkProcessing);
            _document.InvalidateFaceBuild(FaceBuildInput.MaskSettings);
            _document.InvalidateFaceBuild(FaceBuildInput.TraySettings);
            _document.MarkDirty();
        }
    }

    public static Commands.ICommand CreateSetArtworkRecipeCommand(Guid documentId, DocumentTabViewModel document,
        FaceArtworkModel artwork, FaceSubsystemProvenanceModel provenance, string description) =>
        new SetArtworkRecipeMutationCommand(documentId, document, artwork, provenance, description, FaceBuildInput.ArtworkSource);

    public static Commands.ICommand CreateSetArtworkOverrideCommand(Guid documentId, DocumentTabViewModel document,
        FaceArtworkModel artwork, string description) => new SetArtworkRecipeMutationCommand(documentId, document,
            artwork, document.GetFaceDocument().Provenance.Artwork, description, FaceBuildInput.ArtworkOverride);

    private sealed class SetArtworkRecipeMutationCommand : Commands.IDocumentCommand, Commands.IExecutionTrackedCommand
    {
        private readonly Guid _id; private readonly DocumentTabViewModel _document; private readonly FaceArtworkModel _next;
        private readonly FaceSubsystemProvenanceModel _nextProvenance; private readonly string _description; private readonly FaceBuildInput _buildInput;
        private FaceDocumentModel? _previousFace;
        public SetArtworkRecipeMutationCommand(Guid id, DocumentTabViewModel document, FaceArtworkModel next,
            FaceSubsystemProvenanceModel provenance, string description, FaceBuildInput buildInput)
        { _id=id; _document=document; _next=next; _nextProvenance=provenance; _description=description; _buildInput=buildInput; }
        public Guid DocumentId => _id; public string Description => _description; public bool WasExecuted { get; private set; }
        public void Execute()
        {
            var face=_document.GetFaceDocument();
            _previousFace ??= face;
            if (ReferenceEquals(face.Artwork, _next)) return;
            _document.SetFaceDocument(FaceDocumentCopy.WithArtworkAndVisual(face, _next, _nextProvenance));
            FaceBuildConfigurationService.ReconcileArtwork(_document.GetFaceDocument());
            _document.InvalidateFaceBuild(_buildInput); _document.MarkDirty(); WasExecuted=true;
        }
        public void Undo()
        {
            if (_previousFace is null) return;
            _document.SetFaceDocument(_previousFace);
            FaceBuildConfigurationService.ReconcileArtwork(_document.GetFaceDocument());
            _document.InvalidateFaceBuild(_buildInput); _document.MarkDirty();
        }
    }


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


    public static Commands.ICommand CreateSetLampMaskCommand(Guid documentId, DocumentTabViewModel document,
        FaceMaskLayerModel mask, string description) => new SetLampMaskMutationCommand(documentId, document, mask, description);

    private sealed class SetLampMaskMutationCommand : Commands.IDocumentCommand, Commands.IExecutionTrackedCommand
    {
        private readonly Guid _id; private readonly DocumentTabViewModel _document; private readonly FaceMaskLayerModel _next; private readonly string _description;
        private FaceMaskLayerModel? _previous; private FaceSubsystemProvenanceModel? _previousProvenance;
        public SetLampMaskMutationCommand(Guid id, DocumentTabViewModel document, FaceMaskLayerModel next, string description)
        { _id=id; _document=document; _next=next; _description=description; }
        public Guid DocumentId=>_id; public string Description=>_description; public bool WasExecuted{get;private set;}
        public void Execute(){var face=_document.GetFaceDocument();_previous??=face.MaskLayer;_previousProvenance??=face.Provenance.Illumination;Set(_next,FaceDocumentCopy.MarkIlluminationModified(face.Provenance.Illumination));WasExecuted=true;}
        public void Undo(){if(_previous is null||_previousProvenance is null)return;Set(_previous,_previousProvenance);}
        private void Set(FaceMaskLayerModel mask,FaceSubsystemProvenanceModel provenance){var face=_document.GetFaceDocument();_document.SetFaceDocument(FaceDocumentCopy.WithIllumination(face,face.Elements,mask,face.Trays,face.LampEmitters,provenance));_document.InvalidateFaceBuild(FaceBuildInput.LampMaskSource);_document.MarkDirty();}
    }

    public static Commands.ICommand CreateAddLampWindowCommand(Guid documentId, DocumentTabViewModel document, FaceLampWindowElement element)
    {
        return new AddFaceElementMutationCommand(documentId, document, element);
    }

    public static Commands.ICommand CreateAddComponentCommand(Guid documentId, DocumentTabViewModel document, FaceElementModel element) =>
        new AddFaceComponentMutationCommand(documentId, document, element);

    public static Commands.ICommand CreateRebuildComponentsCommand(Guid documentId, DocumentTabViewModel document,
        IReadOnlyList<FaceElementModel> components, string sourcePath) => new RebuildComponentsMutationCommand(documentId,document,components,sourcePath);

    private sealed class RebuildComponentsMutationCommand : Commands.IDocumentCommand, Commands.IExecutionTrackedCommand
    {
        private readonly Guid _id; private readonly DocumentTabViewModel _document; private readonly FaceElementModel[] _components; private readonly string _sourcePath;
        private FaceElementModel[]? _previousElements; private FaceSubsystemProvenanceModel? _previousProvenance;
        public RebuildComponentsMutationCommand(Guid id,DocumentTabViewModel document,IReadOnlyList<FaceElementModel> components,string sourcePath)
        { _id=id;_document=document;_components=components.Select(element => FaceElementModelCloner.Clone(element)).ToArray();_sourcePath=sourcePath; }
        public Guid DocumentId=>_id; public string Description=>"Rebuild Face Components From Source"; public bool WasExecuted{get;private set;}
        public void Execute()
        {
            var face=_document.GetFaceDocument();_previousElements??=face.Elements.Select(element => FaceElementModelCloner.Clone(element)).ToArray();_previousProvenance??=face.Provenance.Components;
            var retained=face.Elements.Where(e=>!FaceElementClassification.IsComponent(e)).Select(element => FaceElementModelCloner.Clone(element));
            Set(retained.Concat(_components.Select(element => FaceElementModelCloner.Clone(element))).ToArray(),new FaceSubsystemProvenanceModel{Origin=FaceSubsystemOrigin.Derived,SourceDocumentPath=_sourcePath});WasExecuted=true;
        }
        public void Undo(){if(_previousElements is null||_previousProvenance is null)return;Set(_previousElements.Select(element => FaceElementModelCloner.Clone(element)).ToArray(),_previousProvenance);}
        private void Set(IReadOnlyList<FaceElementModel> elements,FaceSubsystemProvenanceModel provenance)
        { _document.SetFaceDocument(FaceDocumentCopy.WithElementsAndComponents(_document.GetFaceDocument(),elements,provenance),CreateChange(_document,null,PanelChangeProperties.Structure|PanelChangeProperties.Geometry|PanelChangeProperties.Metadata,true));_document.InvalidateFaceBuild(FaceBuildInput.Components);_document.MarkDirty(); }
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
            Provenance = faceDocument.Provenance, BuildState = faceDocument.BuildState,
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
            Id = model.Artwork.Id, Source = model.Artwork.Source, Geometry = model.Artwork.Geometry, ProcessingPipeline = pipeline,
            CorrectionInputAssetPath = model.Artwork.CorrectionInputAssetPath, BaseAssetPath = model.Artwork.BaseAssetPath, OutputAssetPath = model.Artwork.OutputAssetPath, OutputWidth = model.Artwork.OutputWidth, OutputHeight = model.Artwork.OutputHeight,
            Override = model.Artwork.Override, FinalOutputWidth = model.Artwork.FinalOutputWidth, FinalOutputHeight = model.Artwork.FinalOutputHeight
        };
        return new FaceDocumentModel
        {
            Id = model.Id, Title = model.Title, Summary = model.Summary, SourcePanel2DDocumentId = model.SourcePanel2DDocumentId,
            SourcePanel2DDocumentPath = model.SourcePanel2DDocumentPath, SourceFaceShapeId = model.SourceFaceShapeId,
            AssignedCabinetFaceTargetId = model.AssignedCabinetFaceTargetId, AssignedCabinetAssetPath = model.AssignedCabinetAssetPath,
            SourceRegion = model.SourceRegion, LastRegeneratedAtUtc = model.LastRegeneratedAtUtc, GenerationSettings = model.GenerationSettings,
            Provenance = model.Provenance, BuildState = model.BuildState,
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
            _document.SetFaceDocument(WithPipeline(current, _next), CreateChange(_document, null, properties, structure: properties.HasFlag(PanelChangeProperties.Structure)), updateSerializedDocument: false, affectsFacePreview: false);
            _document.InvalidateFaceBuild(FaceBuildInput.ArtworkProcessing);
            _document.MarkDirty(); WasExecuted = true;
        }
        public void Undo()
        {
            if (_previous is null) return; var current = _document.GetFaceDocument();
            var properties = RequiresInspectorRebuild(current.Artwork?.ProcessingPipeline ?? new(), _previous)
                ? PanelChangeProperties.Metadata | PanelChangeProperties.Structure | PanelChangeProperties.Ordering
                : PanelChangeProperties.Metadata;
            _document.SetFaceDocument(WithPipeline(current, _previous), CreateChange(_document, null, properties, structure: properties.HasFlag(PanelChangeProperties.Structure)), updateSerializedDocument: false, affectsFacePreview: false);
            _document.InvalidateFaceBuild(FaceBuildInput.ArtworkProcessing);
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

    private sealed class AddFaceComponentMutationCommand : Commands.IDocumentCommand, Commands.IExecutionTrackedCommand
    {
        private readonly Guid _id; private readonly DocumentTabViewModel _document; private readonly FaceElementModel _element;
        private int? _index; private FaceSubsystemProvenanceModel? _previousProvenance;
        public AddFaceComponentMutationCommand(Guid id, DocumentTabViewModel document, FaceElementModel element)
        { if (!FaceElementClassification.IsComponent(element)) throw new ArgumentException("Only component elements can be added here.", nameof(element)); _id=id; _document=document; _element=FaceElementModelCloner.Clone(element); }
        public Guid DocumentId=>_id; public string Description=>$"Add {_element.Name}"; public bool WasExecuted{get;private set;}
        public void Execute()
        {
            WasExecuted=false; var face=_document.GetFaceDocument(); if(face.Elements.Any(e=>e.ObjectId==_element.ObjectId))return;
            _previousProvenance ??= face.Provenance.Components; var elements=face.Elements.ToList();
            var index=Math.Clamp(_index??elements.Count,0,elements.Count); elements.Insert(index,FaceElementModelCloner.Clone(_element)); _index=index;
            SetComponents(elements,FaceDocumentCopy.MarkComponentsModified(face.Provenance.Components));
            _document.HierarchySelectedPanelSelection=FaceSelectionService.ToSelectionInfo(_element); WasExecuted=true;
        }
        public void Undo()
        {
            if(_previousProvenance is null)return; var elements=_document.GetFaceElements().Where(e=>e.ObjectId!=_element.ObjectId).ToArray();
            SetComponents(elements,_previousProvenance); if(_document.HierarchySelectedPanelSelection?.ObjectId==_element.ObjectId)_document.HierarchySelectedPanelSelection=null;
        }
        private void SetComponents(IReadOnlyList<FaceElementModel> elements,FaceSubsystemProvenanceModel provenance)
        { _document.SetFaceDocument(FaceDocumentCopy.WithElementsAndComponents(_document.GetFaceDocument(),elements,provenance),CreateChange(_document,_element.ObjectId,PanelChangeProperties.Structure,structure:true)); _document.InvalidateFaceBuild(FaceBuildInput.Components); _document.MarkDirty(); }
    }

    private sealed class AddFaceElementMutationCommand : Commands.IDocumentCommand, Commands.IExecutionTrackedCommand
    {
        private readonly Guid _documentId;
        private readonly DocumentTabViewModel _document;
        private readonly FaceLampWindowElement _element;
        private int? _insertIndex;
        private FaceSubsystemProvenanceModel? _previousProvenance;

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
            var face = _document.GetFaceDocument();
            _previousProvenance ??= face.Provenance.Illumination;
            var elements = _document.GetFaceElements().ToList();
            var index = Math.Clamp(_insertIndex ?? elements.Count, 0, elements.Count);
            elements.Insert(index, _element);
            _insertIndex = index;
            _document.SetFaceDocument(FaceDocumentCopy.WithIllumination(face, elements, face.MaskLayer, face.Trays, face.LampEmitters, FaceDocumentCopy.MarkIlluminationModified(face.Provenance.Illumination)), CreateChange(_document, _element.ObjectId, PanelChangeProperties.Structure, structure: true));
            _document.InvalidateFaceBuild(FaceBuildInput.LampInformation);
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
            var face = _document.GetFaceDocument();
            _document.SetFaceDocument(FaceDocumentCopy.WithIllumination(face, elements, face.MaskLayer, face.Trays, face.LampEmitters, _previousProvenance ?? face.Provenance.Illumination), CreateChange(_document, _element.ObjectId, PanelChangeProperties.Structure, structure: true));
            _document.InvalidateFaceBuild(FaceBuildInput.LampInformation);
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
        private FaceSubsystemProvenanceModel? _originalComponentsProvenance;
        private FaceSubsystemProvenanceModel? _originalIlluminationProvenance;

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
            _originalComponentsProvenance ??= _document.GetFaceDocument().Provenance.Components;
            _originalIlluminationProvenance ??= _document.GetFaceDocument().Provenance.Illumination;
            elements[index] = _updatedElement;
            SetMutatedElements(elements, _updatedElement, FaceElementClassification.IsComponent(_updatedElement)
                ? FaceDocumentCopy.MarkComponentsModified(_document.GetFaceDocument().Provenance.Components) : null);
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
            SetMutatedElements(elements, _originalElement, FaceElementClassification.IsComponent(_originalElement) ? _originalComponentsProvenance : null);
            _document.HierarchySelectedPanelSelection = FaceSelectionService.ToSelectionInfo(_originalElement);
            _document.MarkDirty();
        }

        private void SetMutatedElements(IReadOnlyList<FaceElementModel> elements, FaceElementModel element, FaceSubsystemProvenanceModel? provenance)
        {
            var change=CreateChange(_document,_objectId,PanelChangeProperties.Geometry|PanelChangeProperties.Name|PanelChangeProperties.Visibility|PanelChangeProperties.TransformLockState|PanelChangeProperties.Metadata);
            var face = _document.GetFaceDocument();
            if(element is FaceLampWindowElement)
                _document.SetFaceDocument(FaceDocumentCopy.WithIllumination(face,elements,face.MaskLayer,face.Trays,face.LampEmitters,
                    ReferenceEquals(element,_originalElement) ? (_originalIlluminationProvenance ?? face.Provenance.Illumination) : FaceDocumentCopy.MarkIlluminationModified(face.Provenance.Illumination)),change);
            else if(provenance is null)_document.SetFaceElements(elements,change);
            else _document.SetFaceDocument(FaceDocumentCopy.WithElementsAndComponents(face,elements,provenance),change);
            _document.InvalidateFaceBuild(element is FaceLampWindowElement ? FaceBuildInput.LampInformation : FaceElementClassification.IsComponent(element) ? FaceBuildInput.Components : FaceBuildInput.RuntimeAssetsSettings);
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
        private FaceSubsystemProvenanceModel? _previousComponentsProvenance;

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
            _previousComponentsProvenance ??= _document.GetFaceDocument().Provenance.Components;
            var componentChanged=previous.Keys.Any(id=>elements.FirstOrDefault(e=>e.ObjectId==id) is { } e&&FaceElementClassification.IsComponent(e));
            var change=CreateChange(_document,null,PanelChangeProperties.Geometry|PanelChangeProperties.Name|PanelChangeProperties.Visibility|PanelChangeProperties.TransformLockState|PanelChangeProperties.Metadata);
            if(componentChanged)_document.SetFaceDocument(FaceDocumentCopy.WithElementsAndComponents(_document.GetFaceDocument(),elements,FaceDocumentCopy.MarkComponentsModified(_document.GetFaceDocument().Provenance.Components)),change);
            else _document.SetFaceElements(elements,change);
            if(componentChanged)_document.InvalidateFaceBuild(FaceBuildInput.Components);
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
                var change=CreateChange(_document,null,PanelChangeProperties.Geometry|PanelChangeProperties.Name|PanelChangeProperties.Visibility|PanelChangeProperties.TransformLockState|PanelChangeProperties.Metadata);
                if(_previousComponentsProvenance is not null)_document.SetFaceDocument(FaceDocumentCopy.WithElementsAndComponents(_document.GetFaceDocument(),elements,_previousComponentsProvenance),change); else _document.SetFaceElements(elements,change);
                if(_previousElements.Values.Any(FaceElementClassification.IsComponent))_document.InvalidateFaceBuild(FaceBuildInput.Components);
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
            _document.ReconcileRuntimeAssetsConfiguration();
            _document.MarkDirty();
            WasExecuted = true;
        }

        public void Undo()
        {
            var faceDocument = _document.GetFaceDocument();
            _document.SetFaceDocument(
                WithAssignedCabinetFaceTarget(faceDocument, _originalTargetId, _originalCabinetAssetPath),
                CreateChange(_document, null, PanelChangeProperties.Metadata));
            _document.ReconcileRuntimeAssetsConfiguration();
            _document.MarkDirty();
        }
    }
}
