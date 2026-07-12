namespace OasisEditor;

internal static class ProjectAssetPathResolver
{
    [ThreadStatic]
    private static bool _hasThreadProjectDirectoryPath;

    [ThreadStatic]
    private static string? _threadProjectDirectoryPath;

    private static string? _projectDirectoryPath;

    public static string? ProjectDirectoryPath
    {
        get => _hasThreadProjectDirectoryPath ? _threadProjectDirectoryPath : _projectDirectoryPath;
        set
        {
            var normalizedPath = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            _hasThreadProjectDirectoryPath = true;
            _threadProjectDirectoryPath = normalizedPath;
            _projectDirectoryPath = normalizedPath;
        }
    }

    public static bool TryResolveAssetPath(string? assetPath, out string resolvedPath)
    {
        resolvedPath = string.Empty;
        if (string.IsNullOrWhiteSpace(assetPath))
        {
            return false;
        }

        var candidate = assetPath.Trim();
        if (Path.IsPathRooted(candidate))
        {
            resolvedPath = candidate;
            return true;
        }

        if (string.IsNullOrWhiteSpace(ProjectDirectoryPath))
        {
            return false;
        }

        var relativePath = candidate
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);
        resolvedPath = Path.GetFullPath(Path.Combine(ProjectDirectoryPath, relativePath));
        return true;
    }
}
