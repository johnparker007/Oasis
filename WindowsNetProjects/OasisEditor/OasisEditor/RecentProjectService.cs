using System.Text.Json;

namespace OasisEditor.Avalonia;

/// <summary>A project from the saved recent list, checked against the active workspace before it can be opened.</summary>
public sealed record RecentProjectEntry(string Path, bool CanOpen, string? Diagnostic = null)
{
    public string DisplayName => string.IsNullOrWhiteSpace(Path) ? "Recent projects unavailable" : System.IO.Path.GetFileNameWithoutExtension(Path);
}

public sealed record RecentProjectSnapshot(IReadOnlyList<RecentProjectEntry> Projects, IReadOnlyList<string> Diagnostics);

/// <summary>
/// Stores recents only at the explicitly supplied settings path. It has no ambient AppData access, so callers can
/// avoid reading or writing personal settings in headless/MCP sessions.
/// </summary>
public sealed class RecentProjectService
{
    private const int MaximumProjects = 10;
    private readonly string _storagePath;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public RecentProjectService(string storagePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storagePath);
        _storagePath = System.IO.Path.GetFullPath(storagePath);
    }

    public RecentProjectSnapshot Load(string workspaceRoot)
    {
        var root = NormalizeWorkspace(workspaceRoot);
        List<string> paths;
        try
        {
            if (!File.Exists(_storagePath)) return new([], []);
            paths = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(_storagePath)) ?? [];
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            return new([], [$"Recent projects could not be read: {error.Message}"]);
        }

        var seen = new HashSet<string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        var entries = new List<RecentProjectEntry>();
        foreach (var value in paths.Where(path => !string.IsNullOrWhiteSpace(path)))
        {
            RecentProjectEntry entry;
            try { entry = Inspect(root, value); }
            catch (Exception error) when (error is ArgumentException or InvalidDataException or IOException or UnauthorizedAccessException)
            { entry = new(value, false, error.Message); }
            var key = entry.Path;
            if (seen.Add(key)) entries.Add(entry);
            if (entries.Count == MaximumProjects) break;
        }
        return new(entries, []);
    }

    /// <summary>Records a project only after confirming its file is inside the supplied workspace and exists.</summary>
    public IReadOnlyList<RecentProjectEntry> Record(string workspaceRoot, string projectFilePath)
    {
        var root = NormalizeWorkspace(workspaceRoot);
        var entry = Inspect(root, projectFilePath);
        if (!entry.CanOpen) throw new InvalidOperationException(entry.Diagnostic ?? "The project cannot be opened.");

        var current = LoadRaw();
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        current.RemoveAll(path =>
        {
            try { return string.Equals(System.IO.Path.GetFullPath(path, root), entry.Path, comparison); }
            catch (Exception error) when (error is ArgumentException or NotSupportedException or PathTooLongException) { return false; }
        });
        current.Insert(0, entry.Path);
        if (current.Count > MaximumProjects) current.RemoveRange(MaximumProjects, current.Count - MaximumProjects);

        var directory = System.IO.Path.GetDirectoryName(_storagePath)!;
        Directory.CreateDirectory(directory);
        var temporary = _storagePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(current, JsonOptions));
            File.Move(temporary, _storagePath, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }

        return Load(root).Projects;
    }

    private List<string> LoadRaw()
    {
        if (!File.Exists(_storagePath)) return [];
        try { return JsonSerializer.Deserialize<List<string>>(File.ReadAllText(_storagePath)) ?? []; }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        { throw new InvalidDataException($"Recent projects could not be updated: {error.Message}", error); }
    }

    private static RecentProjectEntry Inspect(string root, string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return new(path, false, "Project path is empty.");
        var full = System.IO.Path.GetFullPath(path, root);
        var relative = System.IO.Path.GetRelativePath(root, full);
        if (System.IO.Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith(".." + System.IO.Path.DirectorySeparatorChar, StringComparison.Ordinal))
            return new(full, false, "Project is outside the active workspace. Change workspace to open it.");
        if (!string.Equals(System.IO.Path.GetExtension(full), ".oasisproj", StringComparison.OrdinalIgnoreCase))
            return new(full, false, "Recent entry is not an .oasisproj file.");
        var confined = WorkspacePathPolicy.ResolveConfined(root, relative, requireExists: false);
        if (!File.Exists(confined)) return new(confined, false, "Project file is missing from the active workspace.");
        WorkspacePathPolicy.RejectReparsePoints(root, relative, requireExists: true);
        return new(confined, true);
    }

    private static string NormalizeWorkspace(string workspaceRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        return System.IO.Path.GetFullPath(workspaceRoot);
    }
}
