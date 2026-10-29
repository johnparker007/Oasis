namespace OasisEditor;

/// <summary>Cleanup cannot inspect another Editor's history, so it must defer while that session is open.</summary>
public sealed class ProjectCleanupLease : IDisposable
{
    private const string Folder = "Generated/.oasis-editor-sessions";
    private readonly FileStream _stream;
    public string ProjectFile { get; }
    public string FilePath { get; }
    public bool IsDisposed { get; private set; }
    private ProjectCleanupLease(string projectFile, string path, FileStream stream)
    { ProjectFile = projectFile; FilePath = path; _stream = stream; }

    public static ProjectCleanupLease Acquire(string projectFile)
    {
        var full = Path.GetFullPath(projectFile); var root = Path.GetDirectoryName(full)!;
        var folder = WorkspacePathPolicy.ResolveConfined(root, Folder);
        Directory.CreateDirectory(folder);
        var path = WorkspacePathPolicy.ResolveConfined(root, Folder + "/" + Environment.ProcessId + "-" + Guid.NewGuid().ToString("N") + ".lock");
        return new(full, path, new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None));
    }

    internal static void EnsureCanClean(string projectFile, ProjectCleanupLease? owner)
    {
        if (owner is not null && (owner.IsDisposed || !SamePath(owner.ProjectFile, projectFile)))
            throw new InvalidOperationException("The project changed. Scan for unused assets again.");
        var root = Path.GetDirectoryName(Path.GetFullPath(projectFile))!;
        var folder = WorkspacePathPolicy.ResolveConfined(root, Folder);
        if (!Directory.Exists(folder)) return;
        foreach (var file in Directory.EnumerateFiles(folder, "*.lock"))
        {
            if (owner is not null && SamePath(file, owner.FilePath)) continue;
            var secure = WorkspacePathPolicy.ResolveConfined(root, Path.GetRelativePath(root, file), requireExists: true);
            try { using var probe = new FileStream(secure, FileMode.Open, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException ex) { throw new InvalidOperationException("Another Editor session has this project open. Close that session before cleaning assets so its undo/redo history is protected.", ex); }
        }
    }

    public void Dispose()
    {
        if (IsDisposed) return;
        IsDisposed = true; _stream.Dispose();
        try
        {
            var root = Path.GetDirectoryName(ProjectFile)!;
            var secure = WorkspacePathPolicy.ResolveConfined(root, Path.GetRelativePath(root, FilePath));
            File.Delete(secure);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException) { }
    }
    private static bool SamePath(string a, string b) => string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}
