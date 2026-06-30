using System.IO;

namespace OasisEditor;

public enum EditorAssetType
{
    Panel2D,
    Face,
    Cabinet3D
}

public sealed class ProjectAssetPathService
{
    public const string Panel2DManifestFileName = "asset.panel2d";
    public const string FaceManifestFileName = "asset.face";
    public const string Cabinet3DManifestFileName = "asset.cabinet3d";
    public const string FaceArtworkFileName = "artwork.png";
    public const string FaceMaskFileName = "mask.png";

    public string SanitizePathSegment(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Asset name is required.", nameof(name));
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Trim().Select(ch => invalid.Contains(ch) || char.IsControl(ch) ? '-' : ch).ToArray());
        sanitized = string.Join(" ", sanitized.Split(' ', StringSplitOptions.RemoveEmptyEntries)).Trim('.', ' ');
        if (string.IsNullOrWhiteSpace(sanitized) || sanitized is "." or "..") throw new ArgumentException("Asset name does not contain any safe path characters.", nameof(name));
        return sanitized;
    }

    public string ToProjectRelativePath(EditorProject project, string absolutePath)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(absolutePath);
        return NormalizeProjectRelativePath(Path.GetRelativePath(project.ProjectDirectory, Path.GetFullPath(absolutePath)));
    }

    public string ResolveProjectRelativePath(EditorProject project, string relativePath)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        return Path.IsPathRooted(relativePath) ? Path.GetFullPath(relativePath) : Path.GetFullPath(Path.Combine(project.ProjectDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar)));
    }

    public string GetAssetTypeDirectory(EditorProject project, EditorAssetType assetType) => Path.Combine(project.AssetsDirectory, GetAssetTypeFolderName(assetType));
    public string GetAssetPackageDirectory(EditorProject project, EditorAssetType assetType, string assetName) => Path.Combine(GetAssetTypeDirectory(project, assetType), SanitizePathSegment(assetName));
    public string GetAssetManifestPath(EditorProject project, EditorAssetType assetType, string assetName) => Path.Combine(GetAssetPackageDirectory(project, assetType, assetName), GetManifestFileName(assetType));
    public string GetPanel2DManifestPath(EditorProject project, string assetName) => GetAssetManifestPath(project, EditorAssetType.Panel2D, assetName);
    public string GetFaceManifestPath(EditorProject project, string assetName) => GetAssetManifestPath(project, EditorAssetType.Face, assetName);
    public string GetCabinet3DManifestPath(EditorProject project, string assetName) => GetAssetManifestPath(project, EditorAssetType.Cabinet3D, assetName);
    public string GetFaceArtworkPath(EditorProject project, string assetName) => Path.Combine(GetAssetPackageDirectory(project, EditorAssetType.Face, assetName), FaceArtworkFileName);
    public string GetFaceMaskPath(EditorProject project, string assetName) => Path.Combine(GetAssetPackageDirectory(project, EditorAssetType.Face, assetName), FaceMaskFileName);
    public string GetFaceRuntimeDirectory(EditorProject project, string assetName) => Path.Combine(project.GeneratedDirectory, "Faces", SanitizePathSegment(assetName), FaceRuntimeExportService.RuntimeDirectoryName);

    public static string? GetPackageAssetNameFromManifestPath(string manifestPath, EditorAssetType assetType)
    {
        if (string.IsNullOrWhiteSpace(manifestPath)) return null;
        var fullPath = Path.GetFullPath(manifestPath);
        if (!string.Equals(Path.GetFileName(fullPath), GetManifestFileName(assetType), StringComparison.OrdinalIgnoreCase)) return null;
        var packageDirectory = Path.GetDirectoryName(fullPath);
        var typeDirectory = packageDirectory is null ? null : Path.GetDirectoryName(packageDirectory);
        if (string.IsNullOrWhiteSpace(packageDirectory) || string.IsNullOrWhiteSpace(typeDirectory)) return null;
        if (!string.Equals(Path.GetFileName(typeDirectory), GetAssetTypeFolderName(assetType), StringComparison.OrdinalIgnoreCase)) return null;
        return Path.GetFileName(packageDirectory);
    }

    public string EnsureUniqueAssetName(EditorProject project, EditorAssetType assetType, string requestedName)
    {
        var safe = SanitizePathSegment(requestedName);
        var candidate = safe;
        for (var suffix = 2; Directory.Exists(GetAssetPackageDirectory(project, assetType, candidate)); suffix++) candidate = $"{safe} {suffix}";
        return candidate;
    }

    public DirectoryInfo CreateAssetPackageDirectory(EditorProject project, EditorAssetType assetType, string assetName) => Directory.CreateDirectory(GetAssetPackageDirectory(project, assetType, assetName));
    public static string NormalizeProjectRelativePath(string path) => path.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
    private static string GetAssetTypeFolderName(EditorAssetType assetType) => assetType switch { EditorAssetType.Panel2D => "Panel2D", EditorAssetType.Face => "Faces", EditorAssetType.Cabinet3D => "Cabinet3D", _ => throw new ArgumentOutOfRangeException(nameof(assetType), assetType, null) };
    private static string GetManifestFileName(EditorAssetType assetType) => assetType switch { EditorAssetType.Panel2D => Panel2DManifestFileName, EditorAssetType.Face => FaceManifestFileName, EditorAssetType.Cabinet3D => Cabinet3DManifestFileName, _ => throw new ArgumentOutOfRangeException(nameof(assetType), assetType, null) };
}
