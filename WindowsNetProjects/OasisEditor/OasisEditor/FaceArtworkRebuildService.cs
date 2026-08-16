namespace OasisEditor;

/// <summary>Builds the disposable flattened texture from Face-owned artwork authoring state.</summary>
internal sealed class FaceArtworkRebuildService
{
    public string? Rebuild(
        FaceArtworkModel artwork,
        Panel2DDocumentModel panel,
        PanelFaceSourceShapeModel shape,
        string? projectDirectory,
        string outputPath)
    {
        ArgumentNullException.ThrowIfNull(artwork);
        ArgumentNullException.ThrowIfNull(panel);
        ArgumentNullException.ThrowIfNull(shape);

        if (artwork.Source.Kind != FaceArtworkSourceKind.Panel2DFaceSourceShape)
        {
            throw new NotSupportedException("Independent artwork sources are authored state but are not rebuildable yet.");
        }

        // The initial pipeline is deliberately empty. Operations are serialized now so later
        // processors can be applied here, rather than in the viewport or Player.
        if (artwork.ProcessingPipeline.Operations.Any(operation => operation.Enabled))
        {
            throw new NotSupportedException("Face artwork processing operations are not implemented yet.");
        }

        return FaceSourceShapeTransformService.TryGenerateBackground(
            panel, shape, artwork.OutputWidth, artwork.OutputHeight, projectDirectory, outputPath);
    }
}
