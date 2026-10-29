using OasisEditor.Features.FmlImport;
using OasisEditor.Features.LayoutImport;

namespace OasisEditor;

public sealed record FmlImportSummary(
    int ImportedElementCount,
    IReadOnlyList<string> CopiedAssetRelativePaths,
    IReadOnlyList<InputDefinitionModel> InputDefinitions,
    IReadOnlyList<string> UnsupportedComponentTypes,
    IReadOnlyList<Features.LayoutImport.LayoutImportWarning> Warnings,
    IReadOnlyList<string> Errors)
{
    public bool Succeeded => Errors.Count == 0;
}

public sealed partial class AuthoringSession
{
    private readonly Dictionary<string, InputDefinitionModel[]> _importedInputDefinitions = new(
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<string>> _createdFmlImports = new(
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
    public IReadOnlyList<string> CaptureCreatedFmlImportDirectories() => _createdFmlImports.Values.SelectMany(v => v).Distinct().ToArray();

    /// <summary>Input bindings recovered from the most recent successful FML import.</summary>
    public IReadOnlyList<InputDefinitionModel> ImportedInputDefinitions => _panelPath is not null
        && _importedInputDefinitions.TryGetValue(_panelPath, out var definitions) ? definitions : [];

    /// <summary>Imports an FML layout and merges all mapped elements into one undoable change.</summary>
    public FmlImportSummary ImportFml(string fmlPath, bool copyAssets = true)
    {
        var document = RequireDocument();
        var projectRoot = RequireProject();
        ArgumentException.ThrowIfNullOrWhiteSpace(fmlPath);
        if (!copyAssets)
            throw new ArgumentException("Portable FML import must copy source assets into the project.", nameof(copyAssets));
        WorkspacePathPolicy.RejectRootReparsePoints(_workspacePath, requireExists: true);
        var sourceFullPath = Path.GetFullPath(fmlPath, _workspacePath);
        var sourceRelativePath = Path.GetRelativePath(_workspacePath, sourceFullPath);
        var secureSource = WorkspacePathPolicy.ResolveConfined(_workspacePath, sourceRelativePath, requireExists: true);
        if (!File.Exists(secureSource)) throw new FileNotFoundException("FML import source must be a file inside the workspace.", secureSource);

        var assetsRoot = WorkspacePathPolicy.ResolveConfined(projectRoot, "Assets", requireExists: true);
        var layoutName = Path.GetFileNameWithoutExtension(secureSource);
        string? destinationRelativePath = null;
        var ownsDestination = false;
        FileStream? importLease = null;
        string? importLockRelative = null;
        void ReleaseImportLease()
        {
            importLease?.Dispose(); importLease = null;
            var relative = importLockRelative; importLockRelative = null;
            if (relative is null) return;
            try { File.Delete(WorkspacePathPolicy.ResolveConfined(projectRoot, relative)); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
        try
        {
            destinationRelativePath = ReserveUniqueImportFolder(projectRoot, assetsRoot, layoutName);
            ownsDestination = true;
            var pendingLock = Path.Combine(destinationRelativePath, ".oasis-importing.lock");
            importLease = new FileStream(WorkspacePathPolicy.ResolveConfined(projectRoot, pendingLock), FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
            importLockRelative = pendingLock;
            var result = new FmlImportService().ImportFromFml(secureSource, projectRoot, assetsRoot, copyAssets: true,
                packageName: Path.GetFileName(destinationRelativePath));
            var summary = new FmlImportSummary(result.ImportedElements.Count, result.CopiedAssetRelativePaths,
                result.InputDefinitions, result.UnsupportedComponentTypes, result.Warnings, result.Errors);
            if (!result.Succeeded)
            {
                ReleaseImportLease();
                DeleteOwnedImportFolder(projectRoot, destinationRelativePath);
                ownsDestination = false;
                return summary;
            }

            if (_panelPath is not null)
            {
                _importedInputDefinitions[_panelPath] = result.InputDefinitions.ToArray();
            }
            if (result.ImportedElements.Count > 0)
            {
                var additions = result.ImportedElements
                    .Select(element => PanelElementModelCloner.Clone(element, objectId: Guid.NewGuid().ToString("N")))
                    .Select(Panel2DDocumentStorage.ToStorageElement).ToArray();
                var groups = new List<PanelGroup>(); var order = OrderedIds(document).ToList(); string? previous = null; PanelGroup? group = null;
                for(var i=0;i<additions.Length;i++)
                {
                    var element=additions[i];var button=result.InputDefinitions.Any(input=>Guid.TryParse(result.ImportedElements[i].ObjectId,out var visualId)&&input.LinkedVisualElementId==visualId);
                    var category=button?"Buttons":element.ElementKind switch
                    {
                        PanelElementKind.Lamp=>"Lamps",PanelElementKind.Reel=>"Reels",PanelElementKind.SevenSegment=>"7-segment displays",
                        PanelElementKind.Alpha=>"Alpha displays",PanelElementKind.Background=>"Backgrounds",PanelElementKind.Label=>"Labels",_=>"Other components"
                    };
                    if(category!=previous)
                    {
                        var existing=document.Groups.Concat(groups).Count(g=>g.Name==category||g.Name.StartsWith(category+" (",StringComparison.Ordinal));
                        group=new PanelGroup {Id=Guid.NewGuid().ToString("N"),Name=existing==0?category:$"{category} ({existing+1})",
                            Icon=button?"Button":element.ElementKind switch {PanelElementKind.Lamp=>"Lamp",PanelElementKind.Reel=>"Reel",PanelElementKind.SevenSegment=>"SevenSegment",_=>"Hierarchy"}};
                        groups.Add(group);order.Add(group.Id);previous=category;
                    }
                    foreach(var input in result.InputDefinitions)
                        if(Guid.TryParse(result.ImportedElements[i].ObjectId,out var sourceId)&&input.LinkedVisualElementId==sourceId)input.LinkedVisualElementId=Guid.Parse(element.ObjectId);
                    additions[i]=element with {ParentId=group!.Id};order.Add(element.ObjectId);
                }
                Commit(document with { Elements = [.. document.Elements, .. additions], Groups=[..document.Groups,..groups],HierarchyOrder=order.ToArray() }, notify: false);
            }
            if (_panelPath is not null)
            {
                if (!_createdFmlImports.TryGetValue(_panelPath, out var imports)) _createdFmlImports[_panelPath] = imports = [];
                imports.Add(Path.GetFullPath(destinationRelativePath, projectRoot));
            }
            ownsDestination = false;
            Changed?.Invoke();
            return summary;
        }
        catch
        {
            ReleaseImportLease();
            if (ownsDestination && destinationRelativePath is not null) DeleteOwnedImportFolder(projectRoot, destinationRelativePath);
            throw;
        }
        finally { ReleaseImportLease(); }
    }

    private static string ReserveUniqueImportFolder(string projectRoot, string assetsRoot, string layoutName)
    {
        var basePath = LayoutImportAssetCopier.GetDestinationRoot(assetsRoot, layoutName);
        for (var suffix = 1; suffix <= 10000; suffix++)
        {
            var path = suffix == 1 ? basePath : basePath + "_" + suffix;
            var relative = Path.GetRelativePath(projectRoot, path);
            if (TryReserveImportFolder(projectRoot, relative)) return relative;
        }
        throw new IOException("Unable to reserve a unique FML import directory.");
    }

    private static bool TryReserveImportFolder(string projectRoot, string destinationRelativePath)
    {
        var destinationPath = WorkspacePathPolicy.ResolveConfined(projectRoot, destinationRelativePath);
        if (Directory.Exists(destinationPath) || File.Exists(destinationPath)) return false;
        var fmlRootRelative = Path.GetRelativePath(projectRoot, Path.Combine(Path.GetDirectoryName(destinationPath)!, "."));
        var fmlRoot = WorkspacePathPolicy.ResolveConfined(projectRoot, fmlRootRelative);
        Directory.CreateDirectory(fmlRoot);

        for (var attempt = 0; attempt < 8; attempt++)
        {
            var token = Guid.NewGuid().ToString("N");
            var reservationName = ".oasis-import-reservation-" + token;
            var reservationRelativePath = Path.Combine(fmlRootRelative, reservationName);
            var reservationPath = WorkspacePathPolicy.ResolveConfined(projectRoot, reservationRelativePath);
            var lockRelativePath = Path.Combine(fmlRootRelative, reservationName + ".lock");
            var lockPath = WorkspacePathPolicy.ResolveConfined(projectRoot, lockRelativePath);
            FileStream reservationLock;
            try { reservationLock = new FileStream(lockPath, FileMode.CreateNew, FileAccess.Write, FileShare.None); }
            catch (IOException) { continue; }

            var reservationOwned = false;
            var destinationOwned = false;
            try
            {
                if (Directory.Exists(reservationPath) || File.Exists(reservationPath)) continue;
                Directory.CreateDirectory(reservationPath);
                var markerRelativePath = Path.Combine(reservationRelativePath, ".oasis-owned");
                var markerPath = WorkspacePathPolicy.ResolveConfined(projectRoot, markerRelativePath);
                using (new FileStream(markerPath, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { }
                reservationOwned = true;

                try
                {
                    Directory.Move(reservationPath, destinationPath);
                    destinationOwned = true;
                }
                catch (IOException) when (Directory.Exists(destinationPath) || File.Exists(destinationPath))
                {
                    // Another importer won this destination. Keep its files and
                    // retry with a suffix, cleaning only our reservation folder.
                    DeleteOwnedImportFolder(projectRoot, reservationRelativePath);
                    reservationOwned = false;
                    return false;
                }
                var destinationMarkerRelative = Path.Combine(destinationRelativePath, ".oasis-owned");
                var destinationMarker = WorkspacePathPolicy.ResolveConfined(projectRoot, destinationMarkerRelative, requireExists: true);
                File.Delete(destinationMarker);
                return true;
            }
            catch
            {
                if (destinationOwned && Directory.Exists(destinationPath)) DeleteOwnedImportFolder(projectRoot, destinationRelativePath);
                if (reservationOwned && Directory.Exists(reservationPath)) DeleteOwnedImportFolder(projectRoot, reservationRelativePath);
                throw;
            }
            finally
            {
                reservationLock.Dispose();
                var secureLock = WorkspacePathPolicy.ResolveConfined(projectRoot, lockRelativePath);
                if (File.Exists(secureLock)) File.Delete(secureLock);
            }
        }

        throw new IOException("Unable to reserve a unique FML import directory.");
    }

    private static void DeleteOwnedImportFolder(string projectRoot, string relativePath)
    {
        var fullPath = WorkspacePathPolicy.ResolveConfined(projectRoot, relativePath);
        if (!Directory.Exists(fullPath)) return;
        WorkspacePathPolicy.RejectReparsePoints(projectRoot, relativePath, requireExists: true);
        Directory.Delete(fullPath, recursive: true);
    }
}
