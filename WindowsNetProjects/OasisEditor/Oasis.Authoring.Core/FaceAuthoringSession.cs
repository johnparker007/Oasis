using System.Security.Cryptography;
using System.Text;

namespace OasisEditor;

public sealed record FaceElementSnapshot(
    string Id,
    string Kind,
    string Name,
    double X,
    double Y,
    double Width,
    double Height,
    bool Visible,
    bool Locked,
    string? AssetPath,
    string? OnColorHex,
    string? OffColorHex,
    int? DigitCount,
    int? LampId);

/// <summary>Portable editing session for one authored Face package in an open Oasis project.</summary>
public sealed partial class FaceAuthoringSession
{
    private readonly AuthoringSession _project;
    private readonly ProjectAssetPathService _assetPaths = new();
    private readonly List<FaceDocumentFile> _history = [];
    private string? _projectFileAtOpen;
    private string? _projectRootAtOpen;
    private string? _packageDirectory;
    private string? _facePath;
    private string? _savedPayload;
    private Dictionary<string, string> _savedPackageFiles = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, string> _savedGeneratedFiles = new(StringComparer.OrdinalIgnoreCase);
    private int _historyIndex;
    private string? _selectedId;

    public FaceAuthoringSession(AuthoringSession project)
        => _project = project ?? throw new ArgumentNullException(nameof(project));

    public string? FacePath => _facePath;
    public string FaceTitle => Current?.Title ?? string.Empty;
    public FaceArtworkModel? CurrentArtwork => Current is null ? null : FaceDocumentStorage.ToModel(Current).Artwork;
    public FaceDocumentModel? CurrentDocument => Current is null ? null : FaceDocumentStorage.ToModel(Current);
    /// <summary>Full read-only native model for renderers; authored session state remains the source of truth.</summary>
    public FaceDocumentModel? Document => Current is null ? null : FaceDocumentStorage.ToModel(Current);
    internal int RenderRevision => Current is null ? 0 : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(Current);
    public string? SelectedId => _selectedId;
    public bool IsDirty => Current is not null && FaceDocumentStorage.SerializePayload(Current) != _savedPayload;
    public bool CanUndo => Current is not null && _historyIndex > 0;
    public bool CanRedo => Current is not null && _historyIndex + 1 < _history.Count;
    public bool IsProjectCurrent => _facePath is null || SamePath(_projectFileAtOpen, _project.ProjectFilePath);
    public IReadOnlyList<FaceElementSnapshot> Elements => (Current?.Elements ?? [])
        .Select(ProjectElement)
        .Concat((Current?.LampEmitters ?? []).Select(ProjectEmitter))
        .ToArray();
    public FaceSourceRegionModel? SourceRegion => Current?.SourceRegion is { } region
        ? FaceSourceRegionModel.FromBounds(region.X, region.Y, region.Width, region.Height)
        : null;
    public double LogicalWidth => Current?.SourceRegion?.Width is > 0
        ? Current.SourceRegion.Width
        : Current?.Artwork?.OutputWidth is > 0 ? Current.Artwork.OutputWidth : FaceDocumentStorage.DefaultNativeLogicalWidth;
    public double LogicalHeight => Current?.SourceRegion?.Height is > 0
        ? Current.SourceRegion.Height
        : Current?.Artwork?.OutputHeight is > 0 ? Current.Artwork.OutputHeight : FaceDocumentStorage.DefaultNativeLogicalHeight;
    public int ArtworkWidth => Current?.Artwork?.OutputWidth is > 0 ? Current.Artwork.OutputWidth : Current?.Artwork?.Source.PixelWidth ?? 0;
    public int ArtworkHeight => Current?.Artwork?.OutputHeight is > 0 ? Current.Artwork.OutputHeight : Current?.Artwork?.Source.PixelHeight ?? 0;
    public string? ArtworkPath => Current is null ? null
        : Current.Artwork?.OutputAssetPath
            ?? Current.Artwork?.BaseAssetPath
            ?? Current.Artwork?.Source.AssetPath
            ?? Current.Elements?.FirstOrDefault(element => string.Equals(element.Kind, "artwork", StringComparison.OrdinalIgnoreCase))?.AssetPath;

    public event Action? Changed;

    private FaceDocumentFile? Current => _history.Count == 0 ? null : _history[_historyIndex];

    /// <summary>Opens an existing Face manifest within the current project's Assets/Faces tree.</summary>
    public void OpenFace(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        EnsureCanReplace();
        var (projectFile, projectRoot) = GetProjectContext();
        var absolutePath = Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(projectRoot, path));
        var relativePath = Path.GetRelativePath(projectRoot, absolutePath);
        var resolvedPath = WorkspacePathPolicy.ResolveConfined(projectRoot, relativePath, requireExists: true);
        var faceName = ProjectAssetPathService.GetPackageAssetNameFromManifestPath(resolvedPath, EditorAssetType.Face)
            ?? throw new InvalidDataException("Choose an asset.face manifest inside Assets/Faces.");
        var expectedRelativePath = Path.Combine("Assets", "Faces", faceName, ProjectAssetPathService.FaceManifestFileName);
        if (!string.Equals(ProjectAssetPathService.NormalizeProjectRelativePath(relativePath),
                ProjectAssetPathService.NormalizeProjectRelativePath(expectedRelativePath), PathComparison))
            throw new InvalidDataException("Choose an asset.face manifest inside Assets/Faces.");

        if (RestoreRetainedFace(resolvedPath)) return;
        RetainCurrentFace();
        var content = File.ReadAllText(resolvedPath);
        if (!FaceDocumentStorage.TryReadValidated(content, out var file, out var error))
            throw new InvalidDataException(error);

        ValidatePackageTree(projectRoot, Path.GetDirectoryName(resolvedPath)!);
        _projectFileAtOpen = projectFile;
        _projectRootAtOpen = projectRoot;
        _facePath = resolvedPath;
        _packageDirectory = Path.GetDirectoryName(resolvedPath)!;
        _savedPackageFiles = FingerprintPackage(projectRoot, _packageDirectory);
        _savedGeneratedFiles = FingerprintGeneratedFaceFiles(projectRoot, faceName);
        _savedPayload = FaceDocumentStorage.SerializePayload(file);
        ResetHistory(file);
        _selectedId = null;
        Changed?.Invoke();
    }

    /// <summary>Creates a native blank Face package with the standard logical canvas.</summary>
    public string CreateFace(string name, string? sourceImage = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (sourceImage is not null)
            throw new NotSupportedException("Portable Face creation currently supports a blank native Face. Import artwork through the existing Editor artwork workflow.");
        EnsureCanReplace();
        var (projectFile, projectRoot) = GetProjectContext();
        var safeName = _assetPaths.SanitizePathSegment(name);
        var packageDirectory = WorkspacePathPolicy.ResolveConfined(projectRoot, Path.Combine("Assets", "Faces", safeName));
        var generatedDirectory = WorkspacePathPolicy.ResolveConfined(projectRoot, Path.Combine("Generated", "Faces", safeName));
        if (Directory.Exists(packageDirectory) || File.Exists(packageDirectory)
            || Directory.Exists(generatedDirectory) || File.Exists(generatedDirectory))
            throw new IOException($"Face package or generated output '{safeName}' already exists.");

        var manifestPath = Path.Combine(packageDirectory, ProjectAssetPathService.FaceManifestFileName);
        var file = FaceDocumentStorage.CreateEmpty(safeName);
        var content = FaceDocumentStorage.Serialize(file);
        Directory.CreateDirectory(packageDirectory);
        WriteAtomic(manifestPath, content, overwrite: false);

        RetainCurrentFace();
        _projectFileAtOpen = projectFile;
        _projectRootAtOpen = projectRoot;
        _facePath = manifestPath;
        _packageDirectory = packageDirectory;
        _savedPackageFiles = FingerprintPackage(projectRoot, packageDirectory);
        _savedGeneratedFiles = FingerprintGeneratedFaceFiles(projectRoot, safeName);
        _savedPayload = FaceDocumentStorage.SerializePayload(file);
        ResetHistory(file);
        _selectedId = null;
        Changed?.Invoke();
        return manifestPath;
    }

    public void Close()
    {
        if (IsDirty) throw new InvalidOperationException("Save or undo Face edits before closing.");
        if (_facePath is {} path) _retainedFaces.Remove(path);
        ClearDocument();
        Changed?.Invoke();
    }

    public void Select(string? id)
    {
        var current = RequireDocument();
        if (id is not null && !(current.Elements ?? []).Any(element => string.Equals(element.ObjectId, id, StringComparison.Ordinal))
            && !(current.LampEmitters ?? []).Any(element => string.Equals(element.ObjectId, id, StringComparison.Ordinal)))
            throw new KeyNotFoundException($"Face element '{id}' was not found.");
        if (string.Equals(_selectedId, id, StringComparison.Ordinal)) return;
        _selectedId = id;
        Changed?.Invoke();
    }

    public bool UpdateElement(string id, string? name = null, double? x = null, double? y = null,
        double? width = null, double? height = null, bool? visible = null, bool? locked = null, int? lampIndex = null)
    {
        var document = RequireDocument();
        if (name is not null && string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Element name cannot be blank.", nameof(name));
        ValidateGeometry(x, y, width, height);
        if (lampIndex is int requestedLampIndex && !FaceLampIdTextureEncoding.IsSupportedLogicalId(requestedLampIndex))
            throw new ArgumentOutOfRangeException(nameof(lampIndex), requestedLampIndex,
                $"Face lamp indices must be from {FaceLampIdTextureEncoding.MinimumLogicalId} to {FaceLampIdTextureEncoding.MaximumLogicalId}.");

        var elements = document.Elements?.ToArray() ?? [];
        var index = Array.FindIndex(elements, element => string.Equals(element.ObjectId, id, StringComparison.Ordinal));
        if (index >= 0)
        {
            var current = elements[index];
            if (lampIndex is not null && !string.Equals(current.Kind, "lampWindow", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("A lamp index can only be assigned to a Face lamp window or emitter.");
            var updated = current with
            {
                Name = name is null ? current.Name : name.Trim(),
                X = x ?? current.X,
                Y = y ?? current.Y,
                Width = width ?? current.Width,
                Height = height ?? current.Height,
                IsVisible = visible ?? current.IsVisible,
                LockTransform = locked ?? current.LockTransform,
                LinkedMachineObjectReference = lampIndex is int newLampIndex
                    ? MachineObjectReference.Lamp(newLampIndex).ToString()
                    : current.LinkedMachineObjectReference
            };
            if (updated == current) return false;
            ValidateUnlockedGeometry(current.LockTransform, current.X, current.Y, current.Width, current.Height,
                updated.X, updated.Y, updated.Width, updated.Height);
            elements[index] = updated;
            Commit(document with { Elements = elements });
            return true;
        }

        var emitters = document.LampEmitters?.ToArray() ?? [];
        var emitterIndex = Array.FindIndex(emitters, element => string.Equals(element.ObjectId, id, StringComparison.Ordinal));
        if (emitterIndex < 0) throw new KeyNotFoundException($"Face element '{id}' was not found.");
        var currentEmitter = emitters[emitterIndex];
        var updatedEmitter = currentEmitter with
        {
            Name = name is null ? currentEmitter.Name : name.Trim(),
            X = x ?? currentEmitter.X,
            Y = y ?? currentEmitter.Y,
            Width = width ?? currentEmitter.Width,
            Height = height ?? currentEmitter.Height,
            IsVisible = visible ?? currentEmitter.IsVisible,
            LockTransform = locked ?? currentEmitter.LockTransform,
            LinkedMachineObjectReference = lampIndex is int emitterLampIndex
                ? MachineObjectReference.Lamp(emitterLampIndex).ToString()
                : currentEmitter.LinkedMachineObjectReference,
            LampId = lampIndex ?? currentEmitter.LampId
        };
        if (updatedEmitter == currentEmitter) return false;
        ValidateUnlockedGeometry(currentEmitter.LockTransform, currentEmitter.X, currentEmitter.Y, currentEmitter.Width, currentEmitter.Height,
            updatedEmitter.X, updatedEmitter.Y, updatedEmitter.Width, updatedEmitter.Height);
        emitters[emitterIndex] = updatedEmitter;
        Commit(document with { LampEmitters = emitters });
        return true;
    }

    public string AddLamp(string name, double x, double y, double width, double height, int lampIndex = 0)
    {
        var document = RequireDocument();
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Lamp name cannot be blank.", nameof(name));
        if (!FaceLampIdTextureEncoding.IsSupportedLogicalId(lampIndex))
            throw new ArgumentOutOfRangeException(nameof(lampIndex), lampIndex,
                $"Face lamp indices must be from {FaceLampIdTextureEncoding.MinimumLogicalId} to {FaceLampIdTextureEncoding.MaximumLogicalId}.");
        ValidateGeometry(x, y, width, height);
        var id = $"lamp-window-{Guid.NewGuid():N}";
        var lamp = new FaceElementFile
        {
            ObjectId = id,
            Name = name.Trim(),
            Kind = "lampWindow",
            X = x,
            Y = y,
            Width = width,
            Height = height,
            IsVisible = true,
            LinkedMachineObjectReference = MachineObjectReference.Lamp(lampIndex).ToString()
        };
        var elements = (document.Elements ?? []).Append(lamp).ToArray();
        Commit(document with { Elements = elements }, id);
        return id;
    }

    public bool DeleteSelected()
    {
        var document = RequireDocument();
        if (_selectedId is null) return false;
        var elements = (document.Elements ?? []).Where(element => !string.Equals(element.ObjectId, _selectedId, StringComparison.Ordinal)).ToArray();
        var emitters = (document.LampEmitters ?? []).Where(element => !string.Equals(element.ObjectId, _selectedId, StringComparison.Ordinal)).ToArray();
        if (elements.Length == (document.Elements?.Count ?? 0) && emitters.Length == (document.LampEmitters?.Count ?? 0)) return false;
        Commit(document with { Elements = elements, LampEmitters = emitters }, selectedId: null);
        return true;
    }

    public void Undo()
    {
        if (!CanUndo) return;
        var previous = Current!;
        _historyIndex--;
        try { if(ArtworkRecipeChanged(previous,Current!))RebuildCurrentArtworkForHistory(); }
        catch { _historyIndex++; throw; }
        KeepSelection();
        Changed?.Invoke();
    }

    public void Redo()
    {
        if (!CanRedo) return;
        var previous = Current!;
        _historyIndex++;
        try { if(ArtworkRecipeChanged(previous,Current!))RebuildCurrentArtworkForHistory(); }
        catch { _historyIndex--; throw; }
        KeepSelection();
        Changed?.Invoke();
    }

    private static bool ArtworkRecipeChanged(FaceDocumentFile previous,FaceDocumentFile next)
        => System.Text.Json.JsonSerializer.Serialize(previous.Artwork)!=System.Text.Json.JsonSerializer.Serialize(next.Artwork);

    public bool Save()
    {
        var document = RequireDocument();
        if (!IsDirty) return false;
        EnsureCurrentProject();
        var packageDirectory = _packageDirectory ?? throw new InvalidOperationException("No Face package is open.");
        var projectRoot = _projectRootAtOpen ?? throw new InvalidOperationException("No Face package is open.");
        var currentFiles = FingerprintPackage(projectRoot, packageDirectory);
        if (!SameFiles(currentFiles, _savedPackageFiles))
            throw new IOException("Face package changed externally; refusing to overwrite authored files.");

        var savedFile = document with { SavedAtUtc = DateTime.UtcNow };
        var content = FaceDocumentStorage.Serialize(savedFile);
        ValidateForSave(savedFile, content);
        WriteAtomic(_facePath!, content, overwrite: true);
        _history[_historyIndex] = savedFile;
        _savedPayload = FaceDocumentStorage.SerializePayload(savedFile);
        _savedPackageFiles = FingerprintPackage(projectRoot, packageDirectory);
        _savedGeneratedFiles = FingerprintGeneratedFaceFiles(projectRoot, Path.GetFileName(packageDirectory));
        Changed?.Invoke();
        return true;
    }

    private void EnsureCanReplace()
    {
        if (!RetainDocuments && IsDirty) throw new InvalidOperationException("Save or undo Face changes before opening, creating, or closing another Face.");
    }

    private void EnsureCurrentProject()
    {
        if (_facePath is null || !IsProjectCurrent)
            throw new InvalidOperationException("The project changed while a Face was open. Reopen the Face in the current project before saving.");
    }

    private (string ProjectFile, string ProjectRoot) GetProjectContext()
    {
        var projectFile = _project.ProjectFilePath ?? throw new InvalidOperationException("Open an Oasis project first.");
        var relative = Path.GetRelativePath(_project.WorkspacePath, projectFile);
        var resolvedProjectFile = WorkspacePathPolicy.ResolveConfined(_project.WorkspacePath, relative, requireExists: true);
        var root = Path.GetDirectoryName(resolvedProjectFile) ?? throw new InvalidOperationException("The Oasis project file has no parent directory.");
        WorkspacePathPolicy.RejectRootReparsePoints(root, requireExists: true);
        return (resolvedProjectFile, root);
    }

    private FaceDocumentFile RequireDocument()
    {
        if (Current is null) throw new InvalidOperationException("Open or create a Face first.");
        EnsureCurrentProject();
        return Current;
    }

    private void Commit(FaceDocumentFile next, string? selectedId = null, bool generated = false)
    {
        if (FaceDocumentStorage.SerializePayload(next) == FaceDocumentStorage.SerializePayload(Current!)) return;
        if (!generated && Current is {} previous)
        {
            var state = CloneBuildState(next.BuildState);
            if (System.Text.Json.JsonSerializer.Serialize(previous.Elements) != System.Text.Json.JsonSerializer.Serialize(next.Elements))
            {
                InvalidateConfigured(state, FaceGeneratedProduct.RuntimeAssets);
                var oldLamps = (previous.Elements ?? []).Where(e => e.Kind == "lampWindow");
                var newLamps = (next.Elements ?? []).Where(e => e.Kind == "lampWindow");
                if (System.Text.Json.JsonSerializer.Serialize(oldLamps) != System.Text.Json.JsonSerializer.Serialize(newLamps))
                    InvalidateConfigured(state, FaceGeneratedProduct.LampMask, FaceGeneratedProduct.Trays);
            }
            if (ArtworkRecipeChanged(previous, next)) InvalidateConfigured(state, FaceGeneratedProduct.ArtworkCorrectionInput, FaceGeneratedProduct.BaseArtwork,
                FaceGeneratedProduct.ArtworkOutput, FaceGeneratedProduct.LampMask, FaceGeneratedProduct.Trays, FaceGeneratedProduct.RuntimeAssets);
            next = next with { BuildState = state };
        }
        if (_historyIndex + 1 < _history.Count) _history.RemoveRange(_historyIndex + 1, _history.Count - _historyIndex - 1);
        _history.Add(next);
        _historyIndex = _history.Count - 1;
        if (selectedId is not null) _selectedId = selectedId;
        KeepSelection();
        Changed?.Invoke();
    }

    private void ResetHistory(FaceDocumentFile document)
    {
        _history.Clear();
        _history.Add(document);
        _historyIndex = 0;
    }

    private void KeepSelection()
    {
        if (_selectedId is not null
            && !(Current?.Elements ?? []).Any(element => string.Equals(element.ObjectId, _selectedId, StringComparison.Ordinal))
            && !(Current?.LampEmitters ?? []).Any(element => string.Equals(element.ObjectId, _selectedId, StringComparison.Ordinal)))
            _selectedId = null;
    }

    private void ClearDocument()
    {
        _history.Clear();
        _historyIndex = 0;
        _selectedId = null;
        _facePath = null;
        _packageDirectory = null;
        _projectFileAtOpen = null;
        _projectRootAtOpen = null;
        _savedPayload = null;
        _savedPackageFiles = new(StringComparer.OrdinalIgnoreCase);
        _savedGeneratedFiles = new(StringComparer.OrdinalIgnoreCase);
    }

    private static FaceElementSnapshot ProjectElement(FaceElementFile element)
    {
        int? lampId = null;
        if (MachineObjectReference.TryParse(element.LinkedMachineObjectReference, out var reference)
            && reference.Kind == MachineObjectKind.Lamp && int.TryParse(reference.Id, out var parsed))
            lampId = parsed;
        return new FaceElementSnapshot(
            element.ObjectId ?? string.Empty,
            element.Kind ?? string.Empty,
            element.Name ?? string.Empty,
            element.X, element.Y, element.Width, element.Height,
            element.IsVisible, element.LockTransform,
            element.AssetPath ?? element.BulbMaskAssetPath ?? element.ReelLampTransmissionMaskAssetPath,
            element.OnColorHex, element.OffColorHex, element.DigitCount, lampId);
    }

    private static FaceElementSnapshot ProjectEmitter(FaceLampEmitterFile element)
        => new(element.ObjectId ?? string.Empty, "lampEmitter", element.Name ?? string.Empty,
            element.X, element.Y, element.Width, element.Height, element.IsVisible, element.LockTransform,
            null, null, null, null, element.LampId);

    private static void ValidateUnlockedGeometry(bool locked, double oldX, double oldY, double oldWidth, double oldHeight,
        double newX, double newY, double newWidth, double newHeight)
    {
        if (locked && (oldX != newX || oldY != newY || oldWidth != newWidth || oldHeight != newHeight))
            throw new InvalidOperationException("This Face element has locked geometry.");
    }

    private static void ValidateGeometry(double? x, double? y, double? width, double? height)
    {
        if (x is double xx && (!double.IsFinite(xx) || Math.Abs(xx) > 1_000_000)
            || y is double yy && (!double.IsFinite(yy) || Math.Abs(yy) > 1_000_000)
            || width is double ww && (!double.IsFinite(ww) || ww <= 0 || ww > 1_000_000)
            || height is double hh && (!double.IsFinite(hh) || hh <= 0 || hh > 1_000_000))
            throw new ArgumentOutOfRangeException(nameof(width), "Face geometry must be finite, positive in size, and within supported coordinate bounds.");
    }

    private static void ValidateForSave(FaceDocumentFile document, string content)
    {
        if (!FaceDocumentStorage.TryReadValidated(content, out _, out var error))
            throw new InvalidDataException(error);

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var element in document.Elements ?? [])
        {
            if (string.IsNullOrWhiteSpace(element.ObjectId) || !ids.Add(element.ObjectId))
                throw new InvalidDataException("Face element IDs must be present and unique.");
            ValidateGeometry(element.X, element.Y, element.Width, element.Height);
            if (string.Equals(element.Kind, "lampWindow", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(element.LinkedMachineObjectReference))
            {
                if (!MachineObjectReference.TryParse(element.LinkedMachineObjectReference, out var reference)
                    || reference.Kind != MachineObjectKind.Lamp
                    || !int.TryParse(reference.Id, out var lampId)
                    || !FaceLampIdTextureEncoding.IsSupportedLogicalId(lampId))
                    throw new InvalidDataException($"Face lamp window '{element.ObjectId}' has an invalid logical lamp index.");
            }
        }

        foreach (var emitter in document.LampEmitters ?? [])
        {
            if (string.IsNullOrWhiteSpace(emitter.ObjectId) || !ids.Add(emitter.ObjectId))
                throw new InvalidDataException("Face element IDs must be present and unique.");
            ValidateGeometry(emitter.X, emitter.Y, emitter.Width, emitter.Height);
            if (emitter.LampId is int lampId && !FaceLampIdTextureEncoding.IsSupportedLogicalId(lampId))
                throw new InvalidDataException($"Face lamp emitter '{emitter.ObjectId}' has an invalid logical lamp index.");
        }
    }

    private static Dictionary<string, string> FingerprintPackage(string projectRoot, string packageDirectory)
    {
        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(packageDirectory)) return files;
        ValidatePackageTree(projectRoot, packageDirectory);
        foreach (var file in Directory.EnumerateFiles(packageDirectory, "*", SearchOption.AllDirectories))
        {
            var relative = ProjectAssetPathService.NormalizeProjectRelativePath(Path.GetRelativePath(packageDirectory, file));
            files[relative] = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)));
        }
        return files;
    }

    private static void ValidatePackageTree(string projectRoot, string packageDirectory)
    {
        var stack = new Stack<string>();
        stack.Push(packageDirectory);
        while (stack.TryPop(out var directory))
        {
            var relativeDirectory = Path.GetRelativePath(projectRoot, directory);
            WorkspacePathPolicy.RejectReparsePoints(projectRoot, relativeDirectory, requireExists: true);
            foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
            {
                var relative = Path.GetRelativePath(projectRoot, entry);
                WorkspacePathPolicy.RejectReparsePoints(projectRoot, relative, requireExists: true);
                if (Directory.Exists(entry)) stack.Push(entry);
            }
        }
    }

    private static bool SameFiles(IReadOnlyDictionary<string, string> left, IReadOnlyDictionary<string, string> right)
        => left.Count == right.Count && left.All(pair => right.TryGetValue(pair.Key, out var hash) && hash == pair.Value);

    private static void WriteAtomic(string path, string content, bool overwrite)
    {
        var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporaryPath, content, new UTF8Encoding(false));
            File.Move(temporaryPath, path, overwrite);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    private static bool SamePath(string? left, string? right)
    {
        if (left is null || right is null) return left is null && right is null;
        return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), PathComparison);
    }

    private static StringComparison PathComparison => OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
}
