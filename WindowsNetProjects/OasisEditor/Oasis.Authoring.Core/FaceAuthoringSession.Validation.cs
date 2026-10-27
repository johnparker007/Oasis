namespace OasisEditor;

public sealed partial class FaceAuthoringSession
{
    /// <summary>Reports portable authored Face consistency and source-panel diagnostics.</summary>
    public IReadOnlyList<FaceValidationDiagnostic> Validate()
    {
        var face = Document ?? throw new InvalidOperationException("Open a Face document before validation.");
        var diagnostics = new List<FaceValidationDiagnostic>();
        diagnostics.AddRange(new FacePipeline.FaceTrayAutoAuthoringService().Validate(face));

        var sourcePath = face.Artwork?.Source.Panel2DDocumentPath ?? face.SourcePanel2DDocumentPath;
        var sourceShapeId = face.Artwork?.Source.FaceSourceShapeId ?? face.SourceFaceShapeId;
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            diagnostics.Add(new(FaceValidationSeverity.Warning, "Face.SourcePanel2D.Missing",
                "Face does not contain a source Panel2D document path."));
            return diagnostics;
        }
        if (string.IsNullOrWhiteSpace(sourceShapeId) || face.SourceRegion is not { IsValid: true })
            diagnostics.Add(new(FaceValidationSeverity.Warning, "Face.SourceRegion.Missing",
                "Face does not contain a source shape ID and valid source region."));

        try
        {
            var root = _project.ProjectFilePath is { } projectFile
                ? Path.GetDirectoryName(projectFile)!
                : throw new InvalidOperationException("Open the Face's project to validate its source Panel2D.");
            var relativePath = Path.IsPathRooted(sourcePath) ? Path.GetRelativePath(root, sourcePath) : sourcePath;
            var fullPath = WorkspacePathPolicy.ResolveConfined(root, relativePath, requireExists: true);
            WorkspacePathPolicy.RejectReparsePoints(root, relativePath, requireExists: true);
            if (!Panel2DDocumentStorage.TryReadValidated(File.ReadAllText(fullPath), out var panel, out var error))
            {
                diagnostics.Add(new(FaceValidationSeverity.Error, "Face.SourcePanel2D.Invalid", error));
                return diagnostics;
            }
            if (!string.IsNullOrWhiteSpace(sourceShapeId)
                && !panel.FaceSourceShapes.Any(shape => string.Equals(shape.Id, sourceShapeId, StringComparison.Ordinal)))
                diagnostics.Add(new(FaceValidationSeverity.Warning, "Face.SourceShape.Missing",
                    $"Source Face region '{sourceShapeId}' was not found in the saved Panel2D document."));
        }
        catch (Exception error) when (error is IOException or InvalidOperationException or UnauthorizedAccessException or ArgumentException)
        {
            diagnostics.Add(new(FaceValidationSeverity.Warning, "Face.SourcePanel2D.Unavailable", error.Message));
        }
        return diagnostics;
    }
}
