using Avalonia.Controls;

namespace OasisEditor.Avalonia;

public sealed partial class WorkspaceView
{
    private ProjectCleanupLease? _cleanupLease;
    private string? _cleanupLeaseError;
    private string[] CleanupProtectedRoots() => _nativeDocuments.Values.Select(d => d.Asset.LibraryRoot)
        .Append(SceneLibraryRoot).Where(p => !string.IsNullOrWhiteSpace(p)).Cast<string>().Distinct().ToArray();
    private void UpdateCleanupLease()
    {
        if (_isAssetWindow || _isDocumentEditor) return;
        if (_cleanupLease is not null && SameScenePath(_cleanupLease.ProjectFile, _session.ProjectFilePath)) return;
        _cleanupLease?.Dispose(); _cleanupLease = null; _cleanupLeaseError = null;
        if (_session.ProjectFilePath is not { } project || _disposed) return;
        try { _cleanupLease = ProjectCleanupLease.Acquire(project); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        { _cleanupLeaseError = ex.Message; }
    }

    private AssetReferenceSnapshot[] CaptureCleanupHistory(string project)
    {
        if (_disposed) return [];
        var sessions = _nativeDocuments.Values.Select(d => d.Asset.Sessions).ToList();
        if (SameScenePath(_session.ProjectFilePath, project)) sessions.Add(LocalAssetSessions);
        return sessions.Where(s => SameScenePath(s.Project.ProjectFilePath, project)).DistinctBy(s => s.Project)
            .SelectMany(s => s.Project.CaptureAssetReferenceHistory()
                .Concat(s.Faces.CaptureAssetReferenceHistory()).Concat(s.Machines.CaptureAssetReferenceHistory())
                .Concat(s.Cabinets.CaptureAssetReferenceHistory()).Concat(s.Prefabs.CaptureAssetReferenceHistory())
                .Concat(s.Reels.CaptureAssetReferenceHistory())).ToArray();
    }

    public async Task<AssetCleanupPlan> InspectUnreferencedAssetsAsync()
    {
        if (_documentOwner is { } host) return await host.InspectUnreferencedAssetsAsync();
        await SynchronizeAssetDocumentsAsync(); EnsureAssetWindowsIdle();
        if (_closeWorkflowActive) throw new InvalidOperationException("Finish the close operation before cleaning assets.");
        UpdateCleanupLease();
        var project = _session.ProjectFilePath ?? throw new InvalidOperationException("Open a project first.");
        if (_cleanupLease is null) throw new IOException("Cleanup cannot protect this project's session history: " + _cleanupLeaseError);
        var history = CaptureCleanupHistory(project); var lease = _cleanupLease; var libraries = CleanupProtectedRoots();
        var plan = await Task.Run(() => UnreferencedAssetService.Scan(project, history, owner: lease, protectedRoots: libraries));
        if (!SameScenePath(_session.ProjectFilePath, project)) throw new InvalidOperationException("The project changed. Scan again.");
        return plan;
    }

    public AssetCleanupResult DeleteUnreferencedAssets(AssetCleanupPlan plan, string[] selectedPaths)
    {
        if (_documentOwner is { } host) return host.DeleteUnreferencedAssets(plan, selectedPaths);
        EnsureAssetWindowsIdle();
        if (_closeWorkflowActive || !SameScenePath(_session.ProjectFilePath, plan.ProjectFile))
            throw new InvalidOperationException("The project or close operation changed. Scan again.");
        // Synchronous on the UI dispatcher: no editing/MCP operation can run
        // between capturing current history and the final scan/delete pass.
        var result = UnreferencedAssetService.DeleteSelected(plan, selectedPaths, CaptureCleanupHistory(plan.ProjectFile), CleanupProtectedRoots());
        SetStatus($"Deleted {result.Deleted.Count} unreferenced assets. {result.Failures.Count} could not be deleted.");
        RefreshAssets(); return result;
    }

    private async Task ShowUnreferencedAssetsAsync()
    {
        if (_documentOwner is { } host) { await host.ShowUnreferencedAssetsAsync(); return; }
        try
        {
            var plan = await InspectUnreferencedAssetsAsync();
            if (TopLevel.GetTopLevel(this) is Window owner)
                await AssetCleanupDialog.ShowAsync(owner, plan, paths => DeleteUnreferencedAssets(plan, paths));
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or UnauthorizedAccessException or System.Text.Json.JsonException or System.Xml.XmlException or ArgumentException)
        { if (!_disposed) await ShowCloseErrorAsync(new InvalidOperationException("Asset cleanup was blocked: " + ex.Message, ex), mentionUnsavedDocuments: false); }
    }

    private void CleanupFmlAfterDiscard(string project, ProjectCleanupLease? lease, string[] libraries, string[] releasedImports)
    {
        if (lease is null || releasedImports.Length == 0) return;
        try
        {
            var history = CaptureCleanupHistory(project);
            var plan = UnreferencedAssetService.Scan(project, history, fmlOnly: true, owner: lease.IsDisposed ? null : lease,
                protectedRoots: libraries.Concat(CleanupProtectedRoots()));
            var root = Path.GetDirectoryName(project)!;
            var selected = plan.Candidates.Where(c => releasedImports.Any(folder =>
            {
                var relative = Path.GetRelativePath(folder, Path.GetFullPath(c.Path, root));
                return !Path.IsPathRooted(relative) && relative != ".." && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal);
            })).Select(c => c.Path).ToArray();
            if (selected.Length == 0) return;
            var result = UnreferencedAssetService.DeleteSelected(plan, selected, history);
            if (!_disposed && (result.Deleted.Count > 0 || result.Failures.Count > 0))
                SetStatus($"Discarded edits; cleaned {result.Deleted.Count} unused FML assets. {result.Failures.Count} cleanup failures.");
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or UnauthorizedAccessException or System.Text.Json.JsonException or System.Xml.XmlException or ArgumentException)
        { if (!_disposed) SetStatus("Edits were discarded. FML cleanup was skipped: " + ex.Message); }
    }
}
