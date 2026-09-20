using System.IO;
using OasisEditor.Features.CabinetEditor.Models;
using OasisEditor.Features.MachineEditor.Models;

namespace OasisEditor;

public sealed class MachineCompositionContextResolver
{
    public MachineCompositionContext ResolveForGeneration(
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

    public MachineCompositionContext ResolveForFace(EditorProject? project, IEnumerable<DocumentTabViewModel> openDocuments, FaceDocumentModel faceDocument)
    {
        ArgumentNullException.ThrowIfNull(faceDocument);
        return MissingAssignment();
    }

    public MachineCompositionContext ResolveForMachine(EditorProject? project, MachineDocument machineDocument, string? machineAssetPath = null)
    {
        ArgumentNullException.ThrowIfNull(machineDocument);
        if (string.IsNullOrWhiteSpace(machineDocument.CabinetAssetPath))
        {
            return new MachineCompositionContext(machineDocument, null, machineAssetPath, null, "Machine.Cabinet.NotAssigned", "Machine has no assigned Cabinet asset.");
        }

        return ResolveByCabinetAssetPath(project, machineDocument, machineDocument.CabinetAssetPath, machineAssetPath);
    }

    private static MachineCompositionContext ResolveByTarget(EditorProject? project, IEnumerable<DocumentTabViewModel> openDocuments, string targetId)
    {
        var machineMatch = openDocuments
            .Where(document => document.Document.DocumentType == EditorDocumentType.Machine)
            .FirstOrDefault(document => document.GetMachineDocument().SurfaceAssignments?.Any(assignment => string.Equals(assignment.TargetId, targetId, StringComparison.Ordinal)) == true);
        if (machineMatch is not null)
        {
            var machine = machineMatch.GetMachineDocument();
            return ResolveByCabinetAssetPath(project, machine, machine.CabinetAssetPath, ToAssetRelativePath(project, machineMatch.FilePath));
        }

        var cabinetMatches = openDocuments
            .Where(document => document.Document.DocumentType == EditorDocumentType.Cabinet3D)
            .Where(document => document.CabinetViewer?.FaceTargets.Any(target => string.Equals(target.Id, targetId, StringComparison.Ordinal)) == true)
            .ToArray();
        if (cabinetMatches.Length == 1)
        {
            var document = cabinetMatches[0];
            return new MachineCompositionContext(null, document.GetCabinetDocument(), null, ToAssetRelativePath(project, document.FilePath), null, null);
        }

        if (cabinetMatches.Length > 1)
        {
            return new MachineCompositionContext(null, null, null, null, "Face.Cabinet.AmbiguousTarget", $"Cabinet Face target '{targetId}' exists in multiple open Cabinet documents.");
        }

        return new MachineCompositionContext(null, null, null, null, "Face.Cabinet.TargetNotFound", $"Cabinet Face target '{targetId}' was not found in open documents.");
    }

    private static MachineCompositionContext ResolveByCabinetAssetPath(EditorProject? project, MachineDocument machineDocument, string? cabinetAssetPath, string? machineAssetPath)
    {
        var normalizedAssetPath = NormalizeProjectPath(cabinetAssetPath);
        if (project is null)
        {
            return new MachineCompositionContext(machineDocument, null, machineAssetPath, normalizedAssetPath, "Face.Cabinet.ProjectUnavailable", $"Assigned Cabinet asset '{normalizedAssetPath}' cannot be resolved because no project is loaded.");
        }

        var fullPath = Path.IsPathRooted(normalizedAssetPath)
            ? normalizedAssetPath
            : Path.Combine(project.ProjectDirectory, normalizedAssetPath.Replace('/', Path.DirectorySeparatorChar));
        var manifestPath = Directory.Exists(fullPath) ? Path.Combine(fullPath, ProjectAssetPathService.Cabinet3DManifestFileName) : fullPath;
        if (!File.Exists(manifestPath))
        {
            return new MachineCompositionContext(machineDocument, null, machineAssetPath, normalizedAssetPath, "Face.Cabinet.AssetMissing", $"Assigned Cabinet asset '{normalizedAssetPath}' could not be found.");
        }

        return CabinetDocumentStorage.TryRead(File.ReadAllText(manifestPath), out var cabinetDocument)
            ? new MachineCompositionContext(machineDocument, cabinetDocument, machineAssetPath, normalizedAssetPath, null, null)
            : new MachineCompositionContext(machineDocument, null, machineAssetPath, normalizedAssetPath, "Face.Cabinet.AssetInvalid", $"Assigned Cabinet asset '{normalizedAssetPath}' could not be read.");
    }

    private static MachineCompositionContext MissingAssignment() => new(null, null, null, null, "Face.Cabinet.NotAssigned", "Face is not assigned to a Machine/Cabinet composition context.");

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
