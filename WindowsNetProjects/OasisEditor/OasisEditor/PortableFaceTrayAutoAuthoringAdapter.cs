namespace OasisEditor;

internal sealed record FaceTrayAutoAuthoringResult(
    IReadOnlyList<FaceTrayModel> Trays,
    IReadOnlyList<FaceLampEmitterElement> Emitters);

/// <summary>WPF compatibility adapter for Core's portable tray/emitter authoring and diagnostics.</summary>
internal sealed class FaceTrayAutoAuthoringService
{
    public FaceTrayAutoAuthoringResult AutoAuthor(FaceDocumentModel faceDocument, string? projectDirectory = null)
    {
        var result = new FacePipeline.FaceTrayAutoAuthoringService().AutoAuthor(faceDocument, projectDirectory);
        return new FaceTrayAutoAuthoringResult(result.Trays, result.Emitters);
    }

    public IReadOnlyList<FaceValidationDiagnostic> Validate(FaceDocumentModel faceDocument)
        => new FacePipeline.FaceTrayAutoAuthoringService().Validate(faceDocument);
}
