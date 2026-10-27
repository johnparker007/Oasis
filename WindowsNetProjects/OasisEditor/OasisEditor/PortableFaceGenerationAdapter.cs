using OasisEditor.Progress;
using SkiaSharp;

namespace OasisEditor;

internal sealed class FaceGenerationResult(FaceDocumentModel document, int convertedLampCount, int artworkElementCount,
    int convertedButtonCount, int convertedSevenSegmentDisplayCount, int convertedAlphaDisplayCount, int convertedReelMountCount)
{
    public FaceDocumentModel Document { get; } = document;
    public int ConvertedLampCount { get; } = convertedLampCount;
    public int ArtworkElementCount { get; } = artworkElementCount;
    public int ConvertedButtonCount { get; } = convertedButtonCount;
    public int ConvertedSevenSegmentDisplayCount { get; } = convertedSevenSegmentDisplayCount;
    public int ConvertedAlphaDisplayCount { get; } = convertedAlphaDisplayCount;
    public int ConvertedReelMountCount { get; } = convertedReelMountCount;
}

/// <summary>WPF progress and reference adapter for the shared Core Face generator.</summary>
internal sealed class FaceGenerationService
{
    private readonly IMachineObjectReferenceResolver? _resolver;

    public FaceGenerationService(IMachineObjectReferenceResolver? machineObjectReferenceResolver = null)
        => _resolver = machineObjectReferenceResolver;

    public FaceGenerationResult GenerateFromPanelFaceSourceShape(Panel2DDocumentModel sourcePanel,
        PanelFaceSourceShapeModel sourceShape, string title, string? sourcePanel2DDocumentId = null,
        double? targetAspectRatio = null, string? projectDirectory = null, string? generatedDirectory = null,
        string? faceAssetName = null, string? faceAssetDirectory = null,
        FaceGenerationSettingsModel? generationSettings = null, IEditorProgressReporter? progress = null,
        string? sourcePanel2DDocumentPath = null, IReadOnlyList<InputDefinitionModel>? inputDefinitions = null,
        CancellationToken cancellationToken = default)
    {
        var service = CreatePortableService();
        var result = service.GenerateFromPanelFaceSourceShape(sourcePanel, sourceShape, title,
            sourcePanel2DDocumentId, targetAspectRatio, projectDirectory, generatedDirectory, faceAssetName,
            faceAssetDirectory, generationSettings, progress is null ? null : (value, message) => progress.Report(value, message),
            sourcePanel2DDocumentPath, inputDefinitions, cancellationToken);
        return new FaceGenerationResult(result.Document, result.ConvertedLampCount, result.ArtworkElementCount,
            result.ConvertedButtonCount, result.ConvertedSevenSegmentDisplayCount,
            result.ConvertedAlphaDisplayCount, result.ConvertedReelMountCount);
    }

    internal FaceMaskLayerModel? GenerateMaskFromSourceShape(Panel2DDocumentModel sourcePanel,
        PanelFaceSourceShapeModel sourceShape, int faceWidth, int faceHeight,
        IReadOnlyList<FaceLampWindowElement> lampWindows, string faceDocumentId,
        string? sourcePanel2DDocumentId, string? projectDirectory, string faceAssetName,
        string? outputPath, byte extractionThreshold, ImageProcessingExecutionOptions? executionOptions = null)
    {
        var options = executionOptions ?? ImageProcessingExecutionPolicy.Current;
        return CreatePortableService().GenerateMaskFromSourceShape(sourcePanel, sourceShape, faceWidth, faceHeight,
            lampWindows, faceDocumentId, sourcePanel2DDocumentId, projectDirectory, faceAssetName, outputPath,
            extractionThreshold, new FacePipeline.ImageProcessingExecutionOptions(options.MaxDegreeOfParallelism, options.CancellationToken));
    }

    internal static SourceShapeMaskContribution CompositeSourceShapeLampMask(byte[] maskPixels, int faceWidth,
        int faceHeight, PanelFaceSourceShapeModel sourceShape, PanelElementModel sourceLamp, SKBitmap lampBitmap,
        FaceLampWindowElement lampWindow, byte extractionThreshold, ImageProcessingExecutionOptions options)
    {
        var contribution = FacePipeline.FaceGenerationService.CompositeSourceShapeLampMask(maskPixels, faceWidth,
            faceHeight, sourceShape, sourceLamp, lampBitmap, lampWindow, extractionThreshold,
            new FacePipeline.ImageProcessingExecutionOptions(options.MaxDegreeOfParallelism, options.CancellationToken));
        return new SourceShapeMaskContribution(contribution.Bounds, contribution.PixelCount);
    }

    private FacePipeline.FaceGenerationService CreatePortableService()
        => new(_resolver is null ? null : new PortableResolver(_resolver));

    private sealed class PortableResolver(IMachineObjectReferenceResolver source) : FacePipeline.IMachineObjectReferenceResolver
    {
        public bool TryGetReference(PanelElementModel element, out MachineObjectReference reference)
            => source.TryGetReference(element, out reference);
        public bool TryGetReference(InputDefinitionModel inputDefinition, out MachineObjectReference reference)
            => source.TryGetReference(inputDefinition, out reference);
    }

    internal readonly record struct SourceShapeMaskContribution(FaceSourceRegionModel? Bounds, int PixelCount);
}
