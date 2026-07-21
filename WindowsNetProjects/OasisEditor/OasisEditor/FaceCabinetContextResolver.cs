using System.IO;
using OasisEditor.Features.CabinetEditor.Models;

namespace OasisEditor;

public sealed record FaceCabinetContext(
    CabinetDocument? CabinetDocument,
    DocumentTabViewModel? CabinetDocumentTab,
    string? CabinetAssetPath,
    string? DiagnosticCode,
    string? DiagnosticMessage)
{
    public bool HasCabinet => CabinetDocument is not null;
}

public sealed class FaceCabinetContextResolver
{
    public FaceCabinetContext ResolveForGeneration(
        EditorProject? project,
        IEnumerable<DocumentTabViewModel> openDocuments,
        string? selectedCabinetFaceTargetId)
    {
        var targetId = Normalize(selectedCabinetFaceTargetId);
        if (string.IsNullOrWhiteSpace(targetId))
        {
            return MissingAssignment();
        }

        return ResolveByTarget(project, openDocuments, targetId);
    }

    public FaceCabinetContext ResolveForFace(
        EditorProject? project,
        IEnumerable<DocumentTabViewModel> openDocuments,
        FaceDocumentModel faceDocument)
    {
        ArgumentNullException.ThrowIfNull(faceDocument);
        var cabinetAssetPath = Normalize(faceDocument.AssignedCabinetAssetPath);
        if (!string.IsNullOrWhiteSpace(cabinetAssetPath))
        {
            return ResolveByAssetPath(project, openDocuments, cabinetAssetPath);
        }

        var targetId = Normalize(faceDocument.AssignedCabinetFaceTargetId);
        if (!string.IsNullOrWhiteSpace(targetId))
        {
            return ResolveByTarget(project, openDocuments, targetId);
        }

        return MissingAssignment();
    }

    private static FaceCabinetContext ResolveByTarget(EditorProject? project, IEnumerable<DocumentTabViewModel> openDocuments, string targetId)
    {
        var matches = openDocuments
            .Where(document => document.Document.DocumentType == EditorDocumentType.Cabinet3D)
            .Where(document => document.CabinetViewer?.FaceTargets.Any(target => string.Equals(target.Id, targetId, StringComparison.Ordinal)) == true)
            .ToArray();

        if (matches.Length == 1)
        {
            var document = matches[0];
            return new FaceCabinetContext(document.GetCabinetDocument(), document, ToAssetRelativePath(project, document.FilePath), null, null);
        }

        if (matches.Length > 1)
        {
            return new FaceCabinetContext(null, null, null, "Face.Cabinet.AmbiguousTarget", $"Cabinet Face target '{targetId}' exists in multiple open Cabinet documents. Assign a Cabinet asset to the Face.");
        }

        return new FaceCabinetContext(null, null, null, "Face.Cabinet.TargetNotFound", $"Cabinet Face target '{targetId}' was not found in open Cabinet documents.");
    }

    private static FaceCabinetContext ResolveByAssetPath(EditorProject? project, IEnumerable<DocumentTabViewModel> openDocuments, string cabinetAssetPath)
    {
        var normalizedAssetPath = NormalizeProjectPath(cabinetAssetPath);
        var openMatch = openDocuments.FirstOrDefault(document =>
            document.Document.DocumentType == EditorDocumentType.Cabinet3D
            && string.Equals(ToAssetRelativePath(project, document.FilePath), normalizedAssetPath, StringComparison.OrdinalIgnoreCase));
        if (openMatch is not null)
        {
            return new FaceCabinetContext(openMatch.GetCabinetDocument(), openMatch, normalizedAssetPath, null, null);
        }

        if (project is null)
        {
            return new FaceCabinetContext(null, null, normalizedAssetPath, "Face.Cabinet.ProjectUnavailable", $"Assigned Cabinet asset '{normalizedAssetPath}' cannot be resolved because no project is loaded.");
        }

        var fullPath = Path.IsPathRooted(normalizedAssetPath)
            ? normalizedAssetPath
            : Path.Combine(project.ProjectDirectory, normalizedAssetPath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(fullPath))
        {
            return new FaceCabinetContext(null, null, normalizedAssetPath, "Face.Cabinet.AssetMissing", $"Assigned Cabinet asset '{normalizedAssetPath}' could not be found.");
        }

        return CabinetDocumentStorage.TryRead(File.ReadAllText(fullPath), out var cabinetDocument)
            ? new FaceCabinetContext(cabinetDocument, null, normalizedAssetPath, null, null)
            : new FaceCabinetContext(null, null, normalizedAssetPath, "Face.Cabinet.AssetInvalid", $"Assigned Cabinet asset '{normalizedAssetPath}' could not be read.");
    }

    private static FaceCabinetContext MissingAssignment() => new(null, null, null, "Face.Cabinet.NotAssigned", "Face is not assigned to a Cabinet asset.");

    private static string? ToAssetRelativePath(EditorProject? project, string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return null;
        if (project is null || string.IsNullOrWhiteSpace(project.ProjectDirectory)) return NormalizeProjectPath(filePath);
        var fullPath = Path.GetFullPath(filePath);
        var projectRoot = Path.GetFullPath(project.ProjectDirectory);
        var relative = Path.GetRelativePath(projectRoot, fullPath);
        return NormalizeProjectPath(relative);
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string NormalizeProjectPath(string path) => path.Trim().Replace('\\', '/');
}
