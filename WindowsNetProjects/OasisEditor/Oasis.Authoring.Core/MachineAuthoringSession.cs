using System.Security.Cryptography;
using System.Text;

namespace OasisEditor;

/// <summary>Portable, document-scoped authoring and persistence for a complete schema-11 Machine.</summary>
public sealed class MachineAuthoringSession
{
    private const string ManifestName = ProjectAssetPathService.MachineManifestFileName;
    private readonly string workspacePath;
    private readonly AuthoringSession? projectSession;
    private readonly List<MachineDocument> history = [];
    private readonly List<string> behaviorHistory = [];
    private string? projectDirectory;
    private string? machinePath;
    private string? diskHash;
    private string savedPayload = string.Empty;
    private string behaviorSource = string.Empty;
    private string savedBehaviorSource = string.Empty;
    private string? behaviorDiskHash;
    private MachineDocument? document;
    private int historyIndex;

    public MachineAuthoringSession(string workspacePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspacePath);
        this.workspacePath = Path.GetFullPath(workspacePath);
    }

    public MachineAuthoringSession(AuthoringSession projectSession)
        : this(projectSession?.WorkspacePath ?? throw new ArgumentNullException(nameof(projectSession)))
    {
        this.projectSession = projectSession;
    }

    public string WorkspacePath => workspacePath;
    public string? ProjectDirectory => projectDirectory;
    public string? MachinePath => machinePath;
    public MachineDocument? Document => document is null ? null : CloneDocument(document);
    public string MachineTitle => document?.DisplayName ?? string.Empty;
    public string BehaviorSource => behaviorSource;
    public bool IsDirty => document is not null && (Payload(document) != savedPayload || behaviorSource != savedBehaviorSource);
    public bool CanUndo => historyIndex > 0;
    public bool CanRedo => historyIndex + 1 < history.Count;
    public event Action? Changed;

    public void OpenProject(string path)
    {
        EnsureCanReplace();
        projectSession?.OpenProject(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        WorkspacePathPolicy.RejectRootReparsePoints(workspacePath, requireExists: true);
        var fullPath = Path.GetFullPath(path, workspacePath);
        var relative = Path.GetRelativePath(workspacePath, fullPath);
        var projectFile = WorkspacePathPolicy.ResolveConfined(workspacePath, relative, requireExists: true);
        if (Directory.Exists(projectFile))
        {
            var projects = Directory.EnumerateFiles(projectFile, "*.oasisproj", SearchOption.TopDirectoryOnly).ToArray();
            if (projects.Length != 1) throw new InvalidDataException("Choose a project folder with exactly one Oasis project file.");
            projectFile = projects[0];
        }
        if (!File.Exists(projectFile) || !string.Equals(Path.GetExtension(projectFile), ".oasisproj", StringComparison.OrdinalIgnoreCase))
            throw new FileNotFoundException("Choose an Oasis project file or project folder.", projectFile);
        var projectRoot = Path.GetDirectoryName(projectFile)!;
        _ = WorkspacePathPolicy.ResolveConfined(projectRoot, "Assets/Machines", requireExists: false);
        projectDirectory = projectSession?.ProjectFilePath is { } sharedProject
            ? Path.GetDirectoryName(sharedProject) : projectRoot;
        machinePath = null; document = null; diskHash = null; savedPayload = string.Empty; behaviorSource = savedBehaviorSource = string.Empty; behaviorDiskHash = null;
        ResetHistory();
        Changed?.Invoke();
    }

    public IReadOnlyList<AssetSummary> ListMachines()
    {
        var root = RequireProject();
        var assetRoot = WorkspacePathPolicy.ResolveConfined(root, "Assets/Machines");
        if (!Directory.Exists(assetRoot)) return [];
        var results = new List<AssetSummary>();
        foreach (var directory in Directory.EnumerateDirectories(assetRoot))
        {
            var relative = Path.Combine("Assets", "Machines", Path.GetFileName(directory), ManifestName);
            var path = WorkspacePathPolicy.ResolveConfined(root, relative);
            WorkspacePathPolicy.RejectReparsePoints(root, relative);
            if (File.Exists(path)) results.Add(new("Machine", Path.GetFileName(directory), ProjectAssetPathService.NormalizeProjectRelativePath(relative)));
        }
        return results.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public string CreateMachine(string name)
    {
        EnsureCanReplace();
        var root = RequireProject();
        var safeName = SanitizeSegment(name);
        var relative = Path.Combine("Assets", "Machines", safeName, ManifestName);
        var path = WorkspacePathPolicy.ResolveConfined(root, relative);
        if (Directory.Exists(Path.GetDirectoryName(path)!) || File.Exists(path)) throw new IOException($"A Machine package named '{safeName}' already exists.");
        var package = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(package);
        var reservation = Path.Combine(package, ".machine-create-" + Guid.NewGuid().ToString("N") + ".lock");
        using (new FileStream(reservation, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { }
        var created = MachineDocument.Create(safeName);
        // Emulation is the native default and cannot declare a behaviour script.
        var content = MachineDocumentStorage.Serialize(created);
        try { WriteNewAtomic(path, content); }
        catch
        {
            File.Delete(reservation);
            if (Directory.Exists(package) && !Directory.EnumerateFileSystemEntries(package).Any()) Directory.Delete(package);
            throw;
        }
        File.Delete(reservation);
        SetOpened(path, created, content);
        Changed?.Invoke();
        return path;
    }

    public void OpenMachine(string path)
    {
        EnsureCanReplace();
        var root = RequireProject();
        var resolved = ResolveManifest(path, root);
        if (SamePath(resolved, machinePath)) return;
        var content = File.ReadAllText(resolved, Encoding.UTF8);
        if (!MachineDocumentStorage.TryRead(content, out var loaded, out var error)) throw new InvalidDataException(error);
        SetOpened(resolved, loaded, content);
        Changed?.Invoke();
    }

    /// <summary>Apply one complete, atomic, validated and undoable Machine edit.</summary>
    public bool UpdateDocument(Func<MachineDocument, MachineDocument> update)
    {
        ArgumentNullException.ThrowIfNull(update);
        var current = RequireDocument();
        var before = MachineDocumentStorage.Serialize(current);
        if (!MachineDocumentStorage.TryRead(before, out var editableCopy, out var copyError)) throw new InvalidDataException(copyError);
        var updated = update(editableCopy) ?? throw new InvalidOperationException("Machine edit returned no document.");
        var after = MachineDocumentStorage.Serialize(updated);
        if (before == after) return false;
        if (!MachineDocumentStorage.TryRead(after, out var validated, out var updateError)) throw new InvalidDataException(updateError);
        Commit(validated);
        return true;
    }

    public bool SetDisplayName(string name) => UpdateDocument(current => current with { DisplayName = name.Trim() });

    public bool SetRuntime(RuntimeDefinition runtime, MachineBehaviorDefinition? behavior = null)
        => UpdateDocument(current => current with { Runtime = runtime, Behavior = runtime is OasisRuntimeDefinition ? behavior ?? MachineBehaviorDefinition.OasisScript() : null });

    public bool SetInteraction(MachineInteractionDefinition interaction)
        => UpdateDocument(current => current with { Interaction = interaction });

    public bool SetInputs(IEnumerable<InputDefinitionModel> inputs)
        => UpdateDocument(current => current with { InputDefinitions = inputs.Select(CloneInput).ToList() });

    public bool SetOasisBehaviorSource(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        ValidateBehaviorSource(source);
        var current = RequireDocument();
        if (current.Runtime is not OasisRuntimeDefinition) throw new InvalidOperationException("Switch the Machine runtime to Oasis before editing its behaviour source.");
        var updated = current with { Behavior = MachineBehaviorDefinition.OasisScript() };
        if (Payload(updated) == Payload(current) && behaviorSource == source) return false;
        Commit(updated, source);
        return true;
    }

    public bool SetOasisRuntime(string displayName, string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        ValidateBehaviorSource(source);
        var current = RequireDocument();
        var updated = current with { DisplayName = displayName.Trim(), Runtime = new OasisRuntimeDefinition(), Behavior = MachineBehaviorDefinition.OasisScript() };
        if (Payload(updated) == Payload(current) && behaviorSource == source) return false;
        Commit(updated, source);
        return true;
    }

    /// <summary>Persist FML-recovered inputs only after an intentional authoring action.</summary>
    public bool ApplyRecoveredInputs(IEnumerable<InputDefinitionModel> inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        var recovered = inputs.Select(CloneInput).ToArray();
        if (recovered.Any(input => string.IsNullOrWhiteSpace(input.Id) || string.IsNullOrWhiteSpace(input.Name)))
            throw new InvalidDataException("Recovered inputs require stable IDs and names.");
        return SetInputs(recovered);
    }

    public void Undo()
    {
        if (!CanUndo) return;
        document = history[--historyIndex];
        behaviorSource = behaviorHistory[historyIndex];
        Changed?.Invoke();
    }

    public void Redo()
    {
        if (!CanRedo) return;
        document = history[++historyIndex];
        behaviorSource = behaviorHistory[historyIndex];
        Changed?.Invoke();
    }

    public void Save()
    {
        var path = machinePath ?? throw new InvalidOperationException("Open or create a Machine before saving.");
        var root = RequireProject();
        var relative = Path.GetRelativePath(root, path);
        var securePath = WorkspacePathPolicy.ResolveConfined(root, relative, requireExists: true);
        EnsureManifestLocation(relative);
        WorkspacePathPolicy.RejectReparsePoints(root, relative, requireExists: true);
        if (!string.Equals(HashFile(path), diskHash, StringComparison.Ordinal))
            throw new IOException("The Machine manifest changed outside this session. Reopen it before saving your edits.");
        if (document!.Behavior is not null)
        {
            ValidateBehaviorSource(behaviorSource);
            var behaviorPath = WorkspacePathPolicy.ResolveConfined(Path.GetDirectoryName(path)!, document.Behavior.Source);
            if (!string.Equals(HashFile(behaviorPath), behaviorDiskHash, StringComparison.Ordinal))
                throw new IOException("The Machine behavior script changed outside this session. Reopen it before saving your edits.");
            WriteAtomic(behaviorPath, behaviorSource);
            behaviorDiskHash = Hash(behaviorSource);
            savedBehaviorSource = behaviorSource;
        }
        var content = MachineDocumentStorage.Serialize(RequireDocument());
        WriteAtomic(securePath, content);
        diskHash = Hash(content);
        savedPayload = Payload(document!);
        history[historyIndex] = document!;
        Changed?.Invoke();
    }

    public string ResolveAsset(string projectRelativePath)
    {
        var root = RequireProject();
        var resolved = WorkspacePathPolicy.ResolveConfined(root, projectRelativePath, requireExists: true);
        WorkspacePathPolicy.RejectReparsePoints(root, projectRelativePath, requireExists: true);
        return resolved;
    }

    private void Commit(MachineDocument updated, string? updatedBehaviorSource = null)
    {
        if (document?.Behavior is null && updated.Behavior is not null && machinePath is not null && projectDirectory is not null)
        {
            var scriptPath = WorkspacePathPolicy.ResolveConfined(Path.GetDirectoryName(machinePath)!, updated.Behavior.Source);
            behaviorDiskHash = HashFile(scriptPath);
        }
        document = updated;
        if (historyIndex + 1 < history.Count) history.RemoveRange(historyIndex + 1, history.Count - historyIndex - 1);
        if (historyIndex + 1 < behaviorHistory.Count) behaviorHistory.RemoveRange(historyIndex + 1, behaviorHistory.Count - historyIndex - 1);
        behaviorSource = updatedBehaviorSource ?? (updated.Behavior is null ? string.Empty : behaviorSource);
        history.Add(updated);
        behaviorHistory.Add(behaviorSource);
        historyIndex = history.Count - 1;
        Changed?.Invoke();
    }

    private void SetOpened(string path, MachineDocument loaded, string content)
    {
        machinePath = path; document = loaded; diskHash = Hash(content); savedPayload = Payload(loaded);
        behaviorSource = loaded.Behavior is null ? string.Empty : ReadBehavior(path, loaded.Behavior.Source);
        savedBehaviorSource = behaviorSource;
        behaviorDiskHash = loaded.Behavior is null ? null : HashFile(WorkspacePathPolicy.ResolveConfined(Path.GetDirectoryName(path)!, loaded.Behavior.Source));
        history.Clear(); behaviorHistory.Clear(); history.Add(loaded); behaviorHistory.Add(behaviorSource); historyIndex = 0;
    }

    private void EnsureCanReplace()
    {
        if (IsDirty) throw new InvalidOperationException("Save or undo the current Machine edits before replacing the open document.");
    }

    private string RequireProject()
    {
        if (projectSession?.ProjectFilePath is { } sharedProject)
        {
            var sharedRoot = Path.GetDirectoryName(sharedProject)!;
            if (!SamePath(sharedRoot, projectDirectory))
            {
                EnsureCanReplace();
                projectDirectory = sharedRoot;
                machinePath = null; document = null; diskHash = null; savedPayload = string.Empty; behaviorSource = savedBehaviorSource = string.Empty; behaviorDiskHash = null;
                ResetHistory();
            }
        }
        return projectDirectory ?? throw new InvalidOperationException("Open an Oasis project first.");
    }
    private MachineDocument RequireDocument() => document ?? throw new InvalidOperationException("Open or create a Machine first.");

    private static string ResolveManifest(string path, string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var full = Path.IsPathRooted(path) ? Path.GetFullPath(path) : WorkspacePathPolicy.ResolveConfined(root, path);
        var relative = Path.GetRelativePath(root, full);
        var resolved = WorkspacePathPolicy.ResolveConfined(root, relative, requireExists: true);
        EnsureManifestLocation(relative);
        WorkspacePathPolicy.RejectReparsePoints(root, relative, requireExists: true);
        return resolved;
    }

    private static void EnsureManifestLocation(string relative)
    {
        var segments = WorkspacePathPolicy.NormalizeRelativePath(relative).Split(Path.DirectorySeparatorChar);
        if (segments.Length != 4 || !segments[0].Equals("Assets", StringComparison.OrdinalIgnoreCase)
            || !segments[1].Equals("Machines", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(segments[2])
            || !segments[3].Equals(ManifestName, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Choose a Machine manifest at Assets/Machines/<name>/asset.machine.");
    }

    private static string SanitizeSegment(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var invalid = Path.GetInvalidFileNameChars().Concat("<>:\"/\\|?*".ToCharArray()).ToHashSet();
        var safe = new string(name.Trim().Select(c => invalid.Contains(c) || char.IsControl(c) ? '-' : c).ToArray()).Trim('.', ' ');
        if (safe.Length == 0 || safe is "." or "..") throw new ArgumentException("Machine name does not contain safe path characters.", nameof(name));
        return safe;
    }

    private static InputDefinitionModel CloneInput(InputDefinitionModel input) => new()
    {
        Id = input.Id, Name = input.Name, Kind = input.Kind, ButtonNumber = input.ButtonNumber, CoinInput = input.CoinInput,
        CoinChannel = input.CoinChannel, CoinValue = input.CoinValue, Inverted = input.Inverted,
        RawMfmeShortcut = input.RawMfmeShortcut, KeyboardShortcut = input.KeyboardShortcut,
        LinkedVisualElementId = input.LinkedVisualElementId, Notes = input.Notes
    };

    private static bool SamePath(string left, string? right) => right is not null && string.Equals(left, right,
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    private static string Payload(MachineDocument value) => MachineDocumentStorage.Serialize(value);
    private static MachineDocument CloneDocument(MachineDocument value)
    {
        var content = MachineDocumentStorage.Serialize(value);
        if (!MachineDocumentStorage.TryRead(content, out var copy, out var error)) throw new InvalidDataException(error);
        return copy;
    }
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static string HashFile(string path) => File.Exists(path) ? Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))) : string.Empty;
    private static void WriteAtomic(string path, string content)
    {
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temporary, content, new UTF8Encoding(false)); File.Move(temporary, path, overwrite: true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static void WriteNewAtomic(string path, string content)
    {
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temporary, content, new UTF8Encoding(false)); File.Move(temporary, path, overwrite: false); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    private static string ReadBehavior(string manifestPath, string relative)
    {
        if (!string.Equals(relative, MachineBehaviorDefinition.CanonicalSourcePath, StringComparison.Ordinal))
            throw new InvalidDataException("Machine behavior must use the canonical behavior.oasis source path.");
        var path = WorkspacePathPolicy.ResolveConfined(Path.GetDirectoryName(manifestPath)!, relative);
        WorkspacePathPolicy.RejectReparsePoints(Path.GetDirectoryName(manifestPath)!, relative);
        return File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : string.Empty;
    }
    private static void ValidateBehaviorSource(string source)
    {
        var result = Oasis.Scripting.OasisScriptCompiler.Compile(source, MachineBehaviorDefinition.CanonicalSourcePath);
        if (!result.Success) throw new InvalidDataException(string.Join(Environment.NewLine, result.Diagnostics.Select(diagnostic => diagnostic.ToString())));
    }
    private void ResetHistory() { history.Clear(); behaviorHistory.Clear(); historyIndex = 0; }
}
