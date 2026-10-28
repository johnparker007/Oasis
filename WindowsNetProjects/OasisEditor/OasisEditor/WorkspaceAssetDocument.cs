using System.Security.Cryptography;
using System.Text;

namespace OasisEditor;

/// <summary>All authoring services bound to one open project.</summary>
public sealed class AssetEditorSessions
{
    public AuthoringSession Project { get; }
    public FaceAuthoringSession Faces { get; }
    public MachineAuthoringSession Machines { get; }
    public CabinetAuthoringSession Cabinets { get; }
    public PrefabAuthoringSession Prefabs { get; }
    public ReelAuthoringSession Reels { get; }

    public AssetEditorSessions(string workspacePath, string projectFilePath)
    {
        Project = new AuthoringSession(workspacePath);
        Project.OpenProject(projectFilePath);
        Faces = new FaceAuthoringSession(Project);
        Machines = new MachineAuthoringSession(Project);
        Cabinets = new CabinetAuthoringSession(Project);
        Prefabs = new PrefabAuthoringSession(Project);
        Reels = new ReelAuthoringSession(Project);
        EnableDocumentRetention();
    }

    public AssetEditorSessions(
        AuthoringSession project,
        FaceAuthoringSession faces,
        MachineAuthoringSession machines,
        CabinetAuthoringSession cabinets,
        PrefabAuthoringSession prefabs,
        ReelAuthoringSession reels)
    {
        Project = project ?? throw new ArgumentNullException(nameof(project));
        Faces = faces ?? throw new ArgumentNullException(nameof(faces));
        Machines = machines ?? throw new ArgumentNullException(nameof(machines));
        Cabinets = cabinets ?? throw new ArgumentNullException(nameof(cabinets));
        Prefabs = prefabs ?? throw new ArgumentNullException(nameof(prefabs));
        Reels = reels ?? throw new ArgumentNullException(nameof(reels));
        if (Project.ProjectFilePath is null)
            throw new InvalidOperationException("Open a project before creating asset editor sessions.");
        EnableDocumentRetention();
    }

    public void EnableDocumentRetention()
    {
        Project.EnableDocumentRetention();
        Faces.EnableDocumentRetention();
        Machines.EnableDocumentRetention();
        Cabinets.EnableDocumentRetention();
        Prefabs.EnableDocumentRetention();
        Reels.EnableDocumentRetention();
    }
}

/// <summary>A stable document identity and its project-bound authoring sessions.</summary>
public sealed class WorkspaceAssetDocument
{
    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    private WorkspaceAssetDocument(string kind, string path, string? libraryRoot, AssetEditorSessions sessions)
    {
        Kind = CanonicalKind(kind);
        Path = System.IO.Path.GetFullPath(path);
        LibraryRoot = libraryRoot is null ? null : System.IO.Path.GetFullPath(libraryRoot);
        Sessions = sessions;
        Id = CreateId(Kind, Path, LibraryRoot, sessions.Project.ProjectFilePath!);
    }

    public string Kind { get; }
    public string Path { get; }
    public string? LibraryRoot { get; }
    public string Id { get; }
    public AssetEditorSessions Sessions { get; }

    /// <summary>Returns the dirty state belonging to this asset, including retained session state.</summary>
    public bool IsDirty => Kind switch
    {
        "Panel2D" => FindDirty(Sessions.Project.OpenDocuments),
        "Face" => FindDirty(Sessions.Faces.OpenDocuments),
        "Machine" => FindDirty(Sessions.Machines.OpenDocuments),
        "Cabinet3D" => FindDirty(Sessions.Cabinets.OpenDocuments),
        "Prefab" => FindDirty(Sessions.Prefabs.OpenDocuments),
        "Reel" => FindDirty(Sessions.Reels.OpenDocuments),
        _ => false
    };

    public string Title
    {
        get
        {
            var activeTitle = Kind switch
            {
                "Panel2D" when PathComparer.Equals(Sessions.Project.PanelPath, Path) => Sessions.Project.PanelTitle,
                "Face" when PathComparer.Equals(Sessions.Faces.FacePath, Path) => Sessions.Faces.FaceTitle,
                "Machine" when PathComparer.Equals(Sessions.Machines.MachinePath, Path) => Sessions.Machines.MachineTitle,
                "Cabinet3D" when PathComparer.Equals(Sessions.Cabinets.FilePath, Path) => null,
                "Prefab" when PathComparer.Equals(Sessions.Prefabs.FilePath, Path) => Sessions.Prefabs.Document.DisplayName,
                "Reel" when PathComparer.Equals(Sessions.Reels.FilePath, Path) => Sessions.Reels.Document.DisplayName,
                _ => null
            };
            return string.IsNullOrWhiteSpace(activeTitle)
                ? System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(Path)) ?? Kind
                : activeTitle;
        }
    }

    public static WorkspaceAssetDocument Open(
        string workspacePath,
        string projectFilePath,
        string kind,
        string path,
        string? libraryRoot = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspacePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectFilePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var canonicalKind = CanonicalKind(kind);
        ValidateLibraryKind(canonicalKind, libraryRoot);
        var resolved = ResolvePath(projectFilePath, path, libraryRoot);
        var sessions = new AssetEditorSessions(workspacePath, projectFilePath);
        var document = new WorkspaceAssetDocument(canonicalKind, resolved.Path, resolved.LibraryRoot, sessions);
        document.Activate();
        return document;
    }

    /// <summary>Transfers one open document and its full history into sessions dedicated to that document.</summary>
    public static WorkspaceAssetDocument Adopt(string kind, string path, string? libraryRoot, AssetEditorSessions source)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var projectFile = source.Project.ProjectFilePath
            ?? throw new InvalidOperationException("The source sessions have no open project.");
        var canonicalKind = CanonicalKind(kind);
        ValidateLibraryKind(canonicalKind, libraryRoot);
        var resolved = ResolvePath(projectFile, path, libraryRoot);
        var destination = new AssetEditorSessions(source.Project.WorkspacePath, projectFile);
        var document = new WorkspaceAssetDocument(canonicalKind, resolved.Path, resolved.LibraryRoot, destination);

        switch (canonicalKind)
        {
            case "Panel2D": source.Project.MoveDocumentTo(document.Path, destination.Project); break;
            case "Face": source.Faces.MoveDocumentTo(document.Path, destination.Faces); break;
            case "Machine": source.Machines.MoveDocumentTo(document.Path, destination.Machines); break;
            case "Cabinet3D": source.Cabinets.MoveDocumentTo(document.Path, destination.Cabinets); break;
            case "Prefab": source.Prefabs.MoveDocumentTo(document.Path, destination.Prefabs); break;
            case "Reel": source.Reels.MoveDocumentTo(document.Path, destination.Reels); break;
            default: throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported asset kind.");
        }

        return document;
    }

    /// <summary>Activate this asset in its sessions before calling editor-specific controls.</summary>
    public void Activate()
    {
        var relative = System.IO.Path.GetRelativePath(LibraryRoot ?? ProjectRoot(Sessions.Project.ProjectFilePath!), Path);
        switch (Kind)
        {
            case "Panel2D": Sessions.Project.OpenPanel(Path); break;
            case "Face": Sessions.Faces.OpenFace(Path); break;
            case "Machine": Sessions.Machines.OpenMachine(Path); break;
            case "Cabinet3D":
                if (LibraryRoot is null) Sessions.Cabinets.Open(Path);
                else Sessions.Cabinets.OpenLibrary(LibraryRoot, relative);
                break;
            case "Prefab":
                if (LibraryRoot is null) Sessions.Prefabs.Open(Path);
                else Sessions.Prefabs.OpenLibrary(LibraryRoot, relative);
                break;
            case "Reel":
                if (LibraryRoot is null) Sessions.Reels.Open(Path);
                else Sessions.Reels.OpenLibrary(LibraryRoot, relative);
                break;
            default: throw new ArgumentOutOfRangeException(nameof(Kind), Kind, "Unsupported asset kind.");
        }
    }

    public void Save()
    {
        Activate();
        switch (Kind)
        {
            case "Panel2D": Sessions.Project.Save(); break;
            case "Face": Sessions.Faces.Save(); break;
            case "Machine": Sessions.Machines.Save(); break;
            case "Cabinet3D": Sessions.Cabinets.Save(); break;
            case "Prefab": Sessions.Prefabs.Save(); break;
            case "Reel": Sessions.Reels.Save(); break;
        }
    }

    public void Undo(bool redo = false)
    {
        Activate();
        switch (Kind)
        {
            case "Panel2D": if (redo) Sessions.Project.Redo(); else Sessions.Project.Undo(); break;
            case "Face": if (redo) Sessions.Faces.Redo(); else Sessions.Faces.Undo(); break;
            case "Machine": if (redo) Sessions.Machines.Redo(); else Sessions.Machines.Undo(); break;
            case "Cabinet3D": if (redo) Sessions.Cabinets.Redo(); else Sessions.Cabinets.Undo(); break;
            case "Prefab": if (redo) Sessions.Prefabs.Redo(); else Sessions.Prefabs.Undo(); break;
            case "Reel": if (redo) Sessions.Reels.Redo(); else Sessions.Reels.Undo(); break;
        }
    }

    internal static string CanonicalKind(string kind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        return kind.Trim().ToLowerInvariant() switch
        {
            "panel" or "panel2d" => "Panel2D",
            "face" => "Face",
            "machine" => "Machine",
            "cabinet" or "cabinet3d" => "Cabinet3D",
            "prefab" or "object3d" => "Prefab",
            "reel" => "Reel",
            _ => throw new ArgumentException($"Unsupported asset kind '{kind}'.", nameof(kind))
        };
    }

    internal static (string Path, string? LibraryRoot) ResolvePath(string projectFilePath, string path, string? libraryRoot)
    {
        var root = libraryRoot is null ? ProjectRoot(projectFilePath) : System.IO.Path.GetFullPath(libraryRoot);
        var fullPath = System.IO.Path.GetFullPath(path, root);
        var relative = System.IO.Path.GetRelativePath(root, fullPath);
        if (System.IO.Path.IsPathRooted(relative) || relative == ".."
            || relative.StartsWith(".." + System.IO.Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || relative.StartsWith(".." + System.IO.Path.AltDirectorySeparatorChar, StringComparison.Ordinal))
            throw new ArgumentException("The asset path must stay within its project or Library root.", nameof(path));

        return (fullPath, libraryRoot is null ? null : root);
    }

    internal static void ValidateLibraryKind(string kind, string? libraryRoot)
    {
        if (libraryRoot is not null && kind is not ("Cabinet3D" or "Prefab" or "Reel"))
            throw new ArgumentException("Library authoring currently supports only Cabinet3D, Prefab and Reel assets.", nameof(libraryRoot));
    }

    private bool FindDirty(IReadOnlyList<OpenAuthoringDocument> documents)
        => documents.FirstOrDefault(document => PathComparer.Equals(System.IO.Path.GetFullPath(document.Path), Path))?.IsDirty == true;

    private static string CreateId(string kind, string path, string? libraryRoot, string projectFilePath)
    {
        var scopeRoot = libraryRoot ?? ProjectRoot(projectFilePath);
        var relative = System.IO.Path.GetRelativePath(scopeRoot, path).Replace('\\', '/');
        var scope = libraryRoot is null ? "project" : "library:" + scopeRoot.Replace('\\', '/');
        var identity = $"{kind.ToUpperInvariant()}\n{scope}\n{relative}";
        if (OperatingSystem.IsWindows()) identity = identity.ToUpperInvariant();
        return "asset-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)))[..24].ToLowerInvariant();
    }

    private static string ProjectRoot(string projectFilePath)
        => System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(projectFilePath))
            ?? throw new ArgumentException("Project file has no directory.", nameof(projectFilePath));
}

/// <summary>Registry of open asset documents, keyed by stable document ID.</summary>
public sealed class AssetDocumentRegistry
{
    private readonly Dictionary<string, WorkspaceAssetDocument> _documents = new(StringComparer.Ordinal);

    public IReadOnlyCollection<WorkspaceAssetDocument> Documents => _documents.Values.ToArray();

    public WorkspaceAssetDocument? Find(string kind, string path, string? libraryRoot = null)
    {
        var canonicalKind = WorkspaceAssetDocument.CanonicalKind(kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        WorkspaceAssetDocument.ValidateLibraryKind(canonicalKind, libraryRoot);
        var matches = _documents.Values.Where(document =>
        {
            if (document.Kind != canonicalKind || libraryRoot is not null && !PathEquals(document.LibraryRoot, libraryRoot))
                return false;
            if (System.IO.Path.IsPathRooted(path)) return PathEquals(document.Path, path);

            var root = libraryRoot is not null
                ? System.IO.Path.GetFullPath(libraryRoot)
                : document.LibraryRoot ?? System.IO.Path.GetDirectoryName(document.Sessions.Project.ProjectFilePath!)!;
            var candidate = System.IO.Path.GetFullPath(path, root);
            return PathEquals(document.Path, candidate);
        }).ToArray();
        if (matches.Length > 1)
            throw new InvalidOperationException("More than one open document matches this asset path; specify its Library root.");
        return matches.SingleOrDefault();
    }

    public WorkspaceAssetDocument Add(WorkspaceAssetDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (!_documents.TryAdd(document.Id, document))
            throw new InvalidOperationException($"Asset document '{document.Id}' is already registered.");
        return document;
    }

    public WorkspaceAssetDocument Open(string workspacePath, string projectFilePath, string kind, string path, string? libraryRoot = null)
    {
        WorkspaceAssetDocument.ValidateLibraryKind(WorkspaceAssetDocument.CanonicalKind(kind), libraryRoot);
        var resolved = WorkspaceAssetDocument.ResolvePath(projectFilePath, path, libraryRoot);
        if (Find(kind, resolved.Path, resolved.LibraryRoot) is { } existing) return existing;
        return Add(WorkspaceAssetDocument.Open(workspacePath, projectFilePath, kind, resolved.Path, resolved.LibraryRoot));
    }

    public WorkspaceAssetDocument Adopt(string kind, string path, string? libraryRoot, AssetEditorSessions source)
    {
        WorkspaceAssetDocument.ValidateLibraryKind(WorkspaceAssetDocument.CanonicalKind(kind), libraryRoot);
        var resolved = WorkspaceAssetDocument.ResolvePath(
            source.Project.ProjectFilePath ?? throw new InvalidOperationException("The source sessions have no open project."),
            path,
            libraryRoot);
        if (Find(kind, resolved.Path, resolved.LibraryRoot) is not null)
            throw new InvalidOperationException("This asset already has a registered document; activate it instead of transferring its session twice.");
        return Add(WorkspaceAssetDocument.Adopt(kind, resolved.Path, resolved.LibraryRoot, source));
    }

    public WorkspaceAssetDocument RemoveClean(string kind, string path, string? libraryRoot = null)
    {
        var document = Find(kind, path, libraryRoot)
            ?? throw new KeyNotFoundException("Asset document is not registered.");
        if (document.IsDirty)
            throw new InvalidOperationException("Save or undo this asset's edits before closing it.");
        _documents.Remove(document.Id);
        return document;
    }

    private static bool PathEquals(string? left, string? right)
        => left is not null && right is not null && string.Equals(
            System.IO.Path.GetFullPath(left), System.IO.Path.GetFullPath(right),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}
