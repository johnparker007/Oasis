namespace OasisEditor;

/// <summary>WPF compatibility adapter for semantic Face conversion shared with Avalonia.</summary>
internal sealed class FaceSemanticElementConversionService
{
    private readonly IMachineObjectReferenceResolver _resolver;

    public FaceSemanticElementConversionService(IMachineObjectReferenceResolver? resolver = null)
        => _resolver = resolver ?? MachineObjectReferenceResolver.Instance;

    public IReadOnlyList<FaceElementModel> ConvertSupportedElements(Panel2DDocumentModel sourcePanel,
        PanelFaceSourceShapeModel sourceShape, int faceWidth, int faceHeight, string? projectDirectory,
        IReadOnlyList<InputDefinitionModel>? inputDefinitions = null)
        => PortableService().ConvertSupportedElements(sourcePanel, sourceShape, faceWidth, faceHeight, projectDirectory, inputDefinitions);

    public FaceSourceRegionModel? TryTransformBounds(PanelElementModel sourceElement, PanelFaceSourceShapeModel sourceShape,
        int faceWidth, int faceHeight)
        => PortableService().TryTransformBounds(sourceElement, sourceShape, faceWidth, faceHeight);

    public static bool IsCenterInsideSourceShape(PanelElementModel element, PanelFaceSourceShapeModel sourceShape)
        => FacePipeline.FaceSemanticElementConversionService.IsCenterInsideSourceShape(element, sourceShape);

    private FacePipeline.FaceSemanticElementConversionService PortableService()
        => new(new PortableResolver(_resolver));

    private sealed class PortableResolver(IMachineObjectReferenceResolver source) : FacePipeline.IMachineObjectReferenceResolver
    {
        public bool TryGetReference(PanelElementModel element, out MachineObjectReference reference)
            => source.TryGetReference(element, out reference);
        public bool TryGetReference(InputDefinitionModel inputDefinition, out MachineObjectReference reference)
            => source.TryGetReference(inputDefinition, out reference);
    }
}
