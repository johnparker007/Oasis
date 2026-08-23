namespace OasisEditor;

public sealed record FaceRuntimeAssetsCapability(bool IsConfigured, FaceCabinetContext? CabinetContext, string? Reason);

/// <summary>
/// Determines whether this Face can build its complete runtime render package standalone.
/// Machine builds are separate: they supply their own Cabinet context directly to the exporter.
/// </summary>
public sealed class FaceRuntimeAssetsConfigurationService
{
    private readonly FaceCabinetContextResolver _cabinetResolver = new();
    private readonly FaceRuntimeExportService _runtimeExporter = new();

    public FaceRuntimeAssetsCapability Evaluate(FaceDocumentModel face, EditorProject? project,
        IEnumerable<DocumentTabViewModel> openDocuments)
    {
        ArgumentNullException.ThrowIfNull(face);
        if (project is null)
        {
            return new(false, null, "No project is open.");
        }
        var cabinet = _cabinetResolver.ResolveForFace(project, openDocuments, face);
        if (!cabinet.HasCabinet)
        {
            return new(false, cabinet, cabinet.DiagnosticMessage ?? "Face has no resolvable standalone Cabinet context.");
        }
        try
        {
            _runtimeExporter.ValidateStandaloneBuildContext(face, cabinet);
            return new(true, cabinet, null);
        }
        catch (Exception exception)
        {
            return new(false, cabinet, exception.Message);
        }
    }

    public void Reconcile(FaceDocumentModel face, FaceRuntimeAssetsCapability capability)
    {
        var node = face.BuildState.Get(FaceGeneratedProduct.RuntimeAssets);
        if (!capability.IsConfigured)
        {
            node.Status = FaceBuildStatus.NotConfigured;
            node.ErrorMessage = null;
            return;
        }
        if (node.Status == FaceBuildStatus.NotConfigured)
        {
            node.Status = face.RuntimeRenderAssets is null ? FaceBuildStatus.Stale : FaceBuildStatus.Current;
            node.ErrorMessage = null;
        }
    }
}
