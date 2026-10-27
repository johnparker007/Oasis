namespace OasisEditor;

/// <summary>Validates portable project-relative paths before resolving them under a workspace.</summary>
public static class WorkspacePathPolicy
{
    public static string NormalizeRelativePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var normalized = path.Replace('\\', '/');
        if (normalized.StartsWith('/') || Path.IsPathRooted(normalized) || normalized.Contains(':'))
            throw new InvalidDataException("A project-relative path cannot be rooted or contain a drive designator.");

        var segments = normalized.Split('/');
        if (segments.Any(segment => segment.Length == 0 || segment is "." or ".."))
            throw new InvalidDataException("A project-relative path cannot contain empty, current, or parent-directory segments.");
        return string.Join(Path.DirectorySeparatorChar, segments);
    }

    public static string ResolveConfined(string root, string relativePath, bool requireExists = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        var fullRoot = Path.GetFullPath(root);
        var relative = NormalizeRelativePath(relativePath);
        var candidate = Path.GetFullPath(Path.Combine(fullRoot, relative));
        var prefix = Path.EndsInDirectorySeparator(fullRoot) ? fullRoot : fullRoot + Path.DirectorySeparatorChar;
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!candidate.StartsWith(prefix, comparison))
            throw new InvalidDataException("The path resolves outside the project.");

        RejectReparsePoints(fullRoot, relative, requireExists);
        return candidate;
    }

    public static void RejectReparsePoints(string root, string relativePath, bool requireExists = false)
    {
        var fullRoot = Path.GetFullPath(root);
        RejectRootReparsePoints(fullRoot, requireExists);

        var current = fullRoot;
        foreach (var segment in NormalizeRelativePath(relativePath).Split(Path.DirectorySeparatorChar))
        {
            current = Path.Combine(current, segment);
            FileSystemInfo info = Directory.Exists(current) ? new DirectoryInfo(current) : new FileInfo(current);
            if (info.Exists && (info.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Project paths cannot pass through a symbolic link or reparse point.");
            if (requireExists && !info.Exists) throw new FileNotFoundException("The project path does not exist.", current);
        }
    }

    public static void RejectRootReparsePoints(string root, bool requireExists = false)
    {
        var fullRoot = Path.GetFullPath(root);
        var rootInfo = new DirectoryInfo(fullRoot);
        var ancestors = new Stack<DirectoryInfo>();
        for (var current = rootInfo; current is not null; current = current.Parent) ancestors.Push(current);
        foreach (var ancestor in ancestors)
        {
            if (ancestor.Exists && (ancestor.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Project paths cannot pass through a symbolic link or reparse point.");
        }
        if (requireExists && !rootInfo.Exists) throw new DirectoryNotFoundException(fullRoot);
    }
}
