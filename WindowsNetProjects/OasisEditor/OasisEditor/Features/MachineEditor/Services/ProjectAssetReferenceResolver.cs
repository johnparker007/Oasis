using System.IO;
using OasisEditor.Features.CabinetEditor.Models;
using OasisEditor.Features.MachineEditor.Models;

namespace OasisEditor.Features.MachineEditor.Services;

public sealed class ProjectAssetReferenceResolver
{
    private readonly ProjectAssetPathService _paths;

    public ProjectAssetReferenceResolver(ProjectAssetPathService? paths = null)
    {
        _paths = paths ?? new ProjectAssetPathService();
    }

    public string ResolveManifestPath(EditorProject project, string relativePath, EditorAssetType assetType)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        var fullPath = _paths.ResolveProjectRelativePath(project, relativePath);
        if (Directory.Exists(fullPath))
        {
            return Path.Combine(fullPath, GetManifestFileName(assetType));
        }

        return fullPath;
    }

    public MachineDocument? ResolveMachineDocument(EditorProject project, string relativePath)
    {
        var manifestPath = ResolveMachineManifestPath(project, relativePath);
        return MachineDocumentStorage.TryRead(File.Exists(manifestPath) ? File.ReadAllText(manifestPath) : null, out var document)
            ? document
            : null;
    }

    public CabinetDocument? ResolveCabinetDocument(EditorProject project, string relativePath)
    {
        var manifestPath = ResolveManifestPath(project, relativePath, EditorAssetType.Cabinet3D);
        return CabinetDocumentStorage.TryRead(File.Exists(manifestPath) ? File.ReadAllText(manifestPath) : null, out var document)
            ? document
            : null;
    }

    private string ResolveMachineManifestPath(EditorProject project, string relativePath)
    {
        var fullPath = _paths.ResolveProjectRelativePath(project, relativePath);
        if (Directory.Exists(fullPath))
        {
            return Path.Combine(fullPath, MachineAssetDiscovery.MachineManifestFileName);
        }

        return fullPath;
    }

    private static string GetManifestFileName(EditorAssetType assetType) => assetType switch
    {
        EditorAssetType.Panel2D => ProjectAssetPathService.Panel2DManifestFileName,
        EditorAssetType.Face => ProjectAssetPathService.FaceManifestFileName,
        EditorAssetType.Cabinet3D => ProjectAssetPathService.Cabinet3DManifestFileName,
        _ => throw new ArgumentOutOfRangeException(nameof(assetType), assetType, "Unsupported asset type for manifest resolution.")
    };
}
