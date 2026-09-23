namespace OasisEditor;

public sealed record FaceRuntimeAssetsCapability(bool IsConfigured, string? Reason);

/// <summary>
/// Determines whether this Face can build its complete runtime render package standalone.
/// Machine builds are separate: they supply their own composition context directly to the exporter.
/// </summary>
public sealed class FaceRuntimeAssetsConfigurationService
{
    private readonly FaceRuntimeExportService _runtimeExporter = new();

    public FaceRuntimeAssetsCapability Evaluate(FaceDocumentModel face, EditorProject? project,
        IEnumerable<DocumentTabViewModel> openDocuments)
    {
        ArgumentNullException.ThrowIfNull(face);
        if (project is null)
        {
            return new(false, "No project is open.");
        }
        if (face.Artwork is null)
        {
            return new(false, "Face artwork is not configured.");
        }
        if (face.MaskLayer is null || string.IsNullOrWhiteSpace(face.MaskLayer.AssetPath))
        {
            return new(false, "Face mask output is not configured.");
        }
        try
        {
            _runtimeExporter.ValidateStandaloneBuildContext(face);
            return new(true, null);
        }
        catch (Exception exception)
        {
            return new(false, exception.Message);
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
