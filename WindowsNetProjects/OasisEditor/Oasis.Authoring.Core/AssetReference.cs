
namespace OasisEditor;

public enum AssetReferenceScope { Project, Library }

/// <summary>A portable authoring-time reference rooted at either the current project or local Oasis Library.</summary>
public sealed record AssetReference
{
    public AssetReferenceScope Scope { get; init; }
    public string Path { get; init; }

    public AssetReference(AssetReferenceScope scope, string path)
    {
        if (scope is not AssetReferenceScope.Project and not AssetReferenceScope.Library)
            throw new ArgumentOutOfRangeException(nameof(scope), scope, "Asset reference scope must be Project or Library.");
        Scope = scope;
        Path = Normalize(path);
    }

    public static AssetReference Project(string path) => new(AssetReferenceScope.Project, path);
    public static AssetReference Library(string path) => new(AssetReferenceScope.Library, path);

    public static string Normalize(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (System.IO.Path.IsPathRooted(path) || System.IO.Path.IsPathFullyQualified(path)
            || path.StartsWith('/') || path.StartsWith('\\')
            || (path.Length >= 2 && char.IsLetter(path[0]) && path[1] == ':'))
            throw new ArgumentException("Asset references must be portable relative paths.", nameof(path));
        var normalized = path.Replace('\\', '/').Trim('/');
        if (normalized.Split('/').Any(part => part.Length == 0 || part is "." or ".."))
            throw new ArgumentException("Asset references cannot contain empty, current-directory, or parent-directory segments.", nameof(path));
        return normalized;
    }

    public override string ToString() => $"{Scope}: {Path}";
}

public sealed class AssetReferenceResolver
{
    public string Resolve(EditorProject project, string libraryRoot, AssetReference reference)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(reference);
        var root = reference.Scope switch
        {
            AssetReferenceScope.Project => project.ProjectDirectory,
            AssetReferenceScope.Library => libraryRoot,
            _ => throw new InvalidOperationException($"Unsupported asset reference scope '{reference.Scope}'.")
        };
        if (string.IsNullOrWhiteSpace(root)) throw new InvalidOperationException(reference.Scope == AssetReferenceScope.Library
            ? "The Oasis Library root is not configured."
            : "The project root is not configured.");
        var fullRoot = System.IO.Path.GetFullPath(root).TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
        var resolved = System.IO.Path.GetFullPath(System.IO.Path.Combine(fullRoot, reference.Path.Replace('/', System.IO.Path.DirectorySeparatorChar)));
        var prefix = fullRoot + System.IO.Path.DirectorySeparatorChar;
        if (!resolved.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException($"Asset reference escapes its {reference.Scope} root.");
        return resolved;
    }

    public AssetReference FromAbsolutePath(EditorProject project, string libraryRoot, AssetReferenceScope scope, string path)
    {
        var root = scope switch
        {
            AssetReferenceScope.Project => project.ProjectDirectory,
            AssetReferenceScope.Library => libraryRoot,
            _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "Asset reference scope must be Project or Library.")
        };
        var relative = System.IO.Path.GetRelativePath(root, System.IO.Path.GetFullPath(path));
        var reference = new AssetReference(scope, relative);
        _ = Resolve(project, libraryRoot, reference);
        return reference;
    }
}

public sealed record LibraryAssetCatalogEntry(EditorAssetType AssetType, string DisplayName, AssetReference Reference, string ManifestPath);
