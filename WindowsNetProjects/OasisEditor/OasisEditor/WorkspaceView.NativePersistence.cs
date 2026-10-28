using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
using Avalonia.Threading;
using Dock.Model.Core;
using Dock.Model.Mvvm.Controls;

namespace OasisEditor.Avalonia;

public sealed partial class WorkspaceView
{
    private Task _nativeRestoreTask = Task.CompletedTask;

    private WorkspaceState InspectNativeWorkspaceState()
    {
        var projectFile = _session.ProjectFilePath;
        var projectRoot = projectFile is null ? null : Path.GetDirectoryName(projectFile)!;
        var theme = Application.Current!.RequestedThemeVariant;
        var window = TopLevel.GetTopLevel(this) as Window;
        var bounds = window is null ? null : new WorkspaceWindowState(window.Position.X, window.Position.Y,
            Math.Max(100, window.ClientSize.Width), Math.Max(100, window.ClientSize.Height), window.WindowState.ToString());
        var documents = _nativeDocuments.Values.Select(entry =>
        {
            var captured = entry.Editor.CaptureAssetView(new(entry.Asset.Kind, entry.Asset.Path,
                entry.Asset.LibraryRoot is null ? "Project" : "Library", entry.Asset.IsDirty,
                ReferenceEquals(entry, _focusedAsset), entry.Asset.LibraryRoot));
            var relativePath = Path.GetRelativePath(entry.Asset.LibraryRoot ?? projectRoot!, entry.Asset.Path).Replace('\\', '/');
            string[]? selection = entry.Asset.Kind switch
            {
                "Panel2D" => entry.Editor._session.SelectedIds.ToArray(),
                "Face" when entry.Editor.Faces.SelectedId is { } selected => [selected],
                _ => null
            };
            return new AssetDocumentViewState(entry.Asset.Kind, relativePath, entry.Asset.LibraryRoot, entry.Asset.Id,
                captured.Viewport, selection, captured.SourceSelection, captured.Scene, captured.MachineGraph);
        }).ToArray();
        var layout = CapturePresentedLayout();
        var projectRelative = projectFile is null ? null : Path.GetRelativePath(_session.WorkspacePath, projectFile).Replace('\\', '/');
        var state = new WorkspaceState(2, theme == ThemeVariant.Dark ? "Dark" : theme == ThemeVariant.Light ? "Light" : "System", layout,
            Project: projectRelative, ActiveDocument: _focusedAsset?.Asset.Kind is "Panel2D" or "Face" or "Machine" ? _focusedAsset.Asset.Kind : "Panel2D", AssetSearch: _assetSearch.Text, MainWindow: bounds,
            Emulation: NativePreviewPreferences,
            LibraryRoot: string.IsNullOrWhiteSpace(SceneLibraryRoot) ? null : SceneLibraryRoot,
            LibraryBrowser: _browseLibrary, Documents: CaptureDocumentList(), FloatingDocuments: InspectFloatingDocuments().ToArray(),
            AssetLayout: new AssetWorkspaceLayout(2, layout, documents, _focusedAsset?.Asset.Id));
        return state;
    }

    private void ApplyNativeWorkspaceState(WorkspaceState state)
    {
        EnsureAssetWindowsIdle();
        if (state.AssetLayout is not { } assetLayout)
            throw new InvalidDataException("The saved asset workspace layout is missing.");
        if (state.Version != 2 || state.Layout is null || state.Theme is not ("System" or "Dark" or "Light"))
            throw new InvalidDataException("Invalid workspace version, layout or theme.");
        if (state.MainWindow is not null) WorkspaceLayoutState.ValidateBounds(state.MainWindow);
        if (state.Emulation?.AudioBufferLengthMilliseconds is < 10 or > 2000)
            throw new InvalidDataException("Invalid emulation audio buffer.");
        if ((state.AssetSearch?.Length ?? 0) > 256)
            throw new InvalidDataException("Workspace document state exceeds the limit.");
        if (_nativeDocuments.Values.Any(entry => entry.Asset.IsDirty || entry.Editor.HasUnsavedChanges) ||
            HasLocalUnsavedChanges)
            throw new InvalidOperationException("Save or undo all document changes before restoring a workspace.");
        if (Preview.Backend is not null || _nativeDocuments.Values.Any(entry => entry.Editor.Preview.Backend is not null))
            throw new InvalidOperationException("Stop preview before restoring a workspace.");

        string? projectPath = null;
        AssetDocumentRegistry registry = new();
        string? projectRoot = null;
        if (state.Project is not null)
        {
            projectPath = OasisEditor.WorkspacePathPolicy.ResolveConfined(_session.WorkspacePath, state.Project, requireExists: true);
            var probe = new AuthoringSession(_session.WorkspacePath);
            probe.OpenProject(projectPath);
            projectRoot = Path.GetDirectoryName(projectPath)!;
            AssetWorkspaceLayoutState.Validate(assetLayout, projectRoot);
            foreach (var saved in assetLayout.Documents)
            {
                var asset = registry.Open(probe.WorkspacePath, projectPath, saved.Kind, saved.Path, saved.LibraryRoot);
                if (!string.Equals(asset.Id, saved.Id, StringComparison.Ordinal))
                    throw new InvalidDataException("A workspace document ID does not match its project asset.");
            }
        }
        else
        {
            if (assetLayout.Documents.Length != 0)
                throw new InvalidDataException("Saved asset documents require a project.");
            AssetWorkspaceLayoutState.Validate(assetLayout);
        }

        if (assetLayout.ActiveId is not null && !registry.Documents.Any(document => document.Id == assetLayout.ActiveId))
            throw new InvalidDataException("The active workspace document is unavailable.");
        if (state.LibraryRoot is { } libraryRoot)
            OasisEditor.WorkspacePathPolicy.RejectRootReparsePoints(Path.GetFullPath(libraryRoot), requireExists: false);

        // All document files and the complete dock graph have been opened and validated in temporary sessions.
        _nativeRestoreTask = RestoreNativeWorkspaceAsync(state, assetLayout, registry, projectPath);
    }

    private async Task RestoreLegacyNativeDocumentsAsync(Task primary, WorkspaceState legacy)
    {
        // Let the legacy restore finish its synchronous state swap before the root starts adopting its documents.
        await Task.Yield();
        await primary;
        await SynchronizeAssetDocumentsAsync();

        var activeKind = legacy.Scene3D?.Mode ?? legacy.ActiveDocument;
        var activeRelativePath = activeKind switch
        {
            "Panel2D" => legacy.Panel,
            "Face" => legacy.Face,
            "Machine" => legacy.Machine,
            "Cabinet3D" => legacy.Scene3D?.Cabinet,
            "Prefab" => legacy.Scene3D?.Prefab,
            "Reel" => legacy.Scene3D?.Reel,
            _ => null
        };
        var activeLibraryRoot = legacy.Scene3D?.Scope == "Library" ? legacy.LibraryRoot : null;
        NativeAssetEditor? active = null;
        if (activeRelativePath is not null)
        {
            var activePath = activeLibraryRoot is null
                ? OasisEditor.WorkspacePathPolicy.ResolveConfined(_session.RequireProjectDirectory(), activeRelativePath, requireExists: true)
                : OasisEditor.WorkspacePathPolicy.ResolveConfined(activeLibraryRoot, activeRelativePath, requireExists: true);
            active = _nativeDocuments.Values.FirstOrDefault(entry => entry.Asset.Kind == activeKind && SameScenePath(entry.Asset.Path, activePath));
            if (active is not null)
            {
                if (activeKind == "Panel2D")
                {
                    var available = active.Editor._session.Elements.Select(element => element.Id).ToHashSet(StringComparer.Ordinal);
                    active.Editor._session.SelectElements((legacy.PanelSelection ?? []).Where(available.Contains).ToArray());
                }
                else if (activeKind == "Face")
                    active.Editor._faces.Select(active.Editor._faces.Elements.Any(element => element.Id == legacy.FaceSelection)
                        ? legacy.FaceSelection : null);

                var relative = Path.GetRelativePath(active.Asset.LibraryRoot ?? active.Editor._session.RequireProjectDirectory(), active.Asset.Path).Replace('\\', '/');
                var viewState = new FloatingAssetState(active.Asset.Kind, relative, active.Asset.LibraryRoot,
                    new WorkspaceWindowState(80, 80, 1200, 800), legacy.Viewport, legacy.SourceSelection,
                    activeKind == "Panel2D" ? legacy.PanelSelection : null,
                    activeKind == "Face" ? legacy.FaceSelection : null,
                    legacy.Scene3D, legacy.MachineGraph);
                await active.Editor.DisplayTransferredAssetAsync(activeKind, viewState);
            }
        }

        foreach (var saved in legacy.FloatingDocuments ?? [])
        {
            var fullPath = Path.GetFullPath(saved.Path, saved.LibraryRoot ?? _session.RequireProjectDirectory());
            var entry = _nativeDocuments.Values.FirstOrDefault(document => document.Asset.Kind == saved.Kind && SameScenePath(document.Asset.Path, fullPath));
            if (entry is null) continue; // The legacy preflight already guarantees membership in its document list.

            await entry.Editor.DisplayTransferredAssetAsync(saved.Kind, saved);
            if (saved.Kind == "Panel2D")
            {
                var available = entry.Editor._session.Elements.Select(element => element.Id).ToHashSet(StringComparer.Ordinal);
                entry.Editor._session.SelectElements((saved.PanelSelection ?? []).Where(available.Contains).ToArray());
            }
            else if (saved.Kind == "Face")
                entry.Editor._faces.Select(entry.Editor._faces.Elements.Any(element => element.Id == saved.FaceSelection) ? saved.FaceSelection : null);
            if (saved.Layout is { } childLayout)
                entry.Editor.ReplaceLayout(WorkspaceLayoutState.Restore(childLayout, WorkspaceLayoutState.LeafMap(entry.Editor._factory.CreateLayout())));

            _nativeLayoutOperation = true;
            try
            {
                _factory.FloatDockable(entry.Document);
                Dispatcher.UIThread.RunJobs();
                if (NativeWindowFor(entry) is { } window) RestoreWindow(window, saved.Bounds);
                _factory.SetActiveDockable(entry.Document);
                FocusNativeEditor(entry);
            }
            finally { _nativeLayoutOperation = false; }
        }

        if (active is not null && _nativeDocuments.ContainsKey(active.Asset.Id))
        {
            _factory.SetActiveDockable(active.Document);
            FocusNativeEditor(active);
        }
    }

    private static void PreflightLegacyFloatingDocuments(WorkspaceState state, AuthoringSession project)
    {
        var documents = state.FloatingDocuments ?? [];
        if (documents.Length > 64) throw new InvalidDataException("Too many floating asset windows.");
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var seen = new HashSet<string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        foreach (var saved in documents)
        {
            if (saved is null || saved.Kind is not ("Panel2D" or "Face" or "Machine" or "Cabinet3D" or "Prefab" or "Reel") ||
                string.IsNullOrWhiteSpace(saved.Path))
                throw new InvalidDataException("Invalid floating workspace document.");
            WorkspaceLayoutState.ValidateBounds(saved.Bounds);
            saved.Viewport?.Validate();
            saved.Scene?.Validate();
            if ((saved.Scene is not null && (saved.Scene.Mode != saved.Kind || saved.Scene.Scope != (saved.LibraryRoot is null ? "Project" : "Library"))) ||
                (saved.MachineGraph && saved.Kind != "Machine") || (saved.PanelSelection?.Length ?? 0) > 10_000)
                throw new InvalidDataException("Invalid floating asset view.");

            (string Path, string? LibraryRoot) resolved;
            try { resolved = WorkspaceAssetDocument.ResolvePath(project.ProjectFilePath!, saved.Path, saved.LibraryRoot); }
            catch (Exception error) when (error is ArgumentException or InvalidDataException or IOException or UnauthorizedAccessException)
                { throw new InvalidDataException("Floating workspace document path is invalid.", error); }
            var key = saved.Kind + "|" + (resolved.LibraryRoot ?? string.Empty) + "|" + resolved.Path;
            if (!seen.Add(key)) throw new InvalidDataException("Duplicate floating workspace document.");
            var belongsToProjectList = (state.Documents ?? []).Any(document =>
            {
                if (document is null || document.Kind != saved.Kind || !string.Equals(document.LibraryRoot ?? string.Empty, saved.LibraryRoot ?? string.Empty, comparison))
                    return false;
                try
                {
                    var candidate = WorkspaceAssetDocument.ResolvePath(project.ProjectFilePath!, document.Path, document.LibraryRoot);
                    return string.Equals(candidate.Path, resolved.Path, comparison) &&
                        string.Equals(candidate.LibraryRoot ?? string.Empty, resolved.LibraryRoot ?? string.Empty, comparison);
                }
                catch (Exception error) when (error is ArgumentException or InvalidDataException or IOException) { return false; }
            });
            if (!belongsToProjectList)
                throw new InvalidDataException("A floating asset must belong to the saved document list.");

            if (saved.Layout is { } layout)
            {
                var known = new Dictionary<string, IDockable>(StringComparer.Ordinal)
                {
                    ["Authoring"] = new Document { Id = "Authoring" },
                    ["Hierarchy"] = new Tool { Id = "Hierarchy" },
                    ["Project"] = new Tool { Id = "Project" },
                    ["Inspector"] = new Tool { Id = "Inspector" },
                    ["Output"] = new Tool { Id = "Output" }
                };
                _ = WorkspaceLayoutState.Restore(layout, known);
            }

            ReadDocumentList(state with { Documents = [new(saved.Kind, saved.Path, saved.LibraryRoot)] }, project,
                new(project), new(project), new(project), new(project), new(project));
        }
    }

    private async Task RestoreNativeWorkspaceAsync(WorkspaceState state, AssetWorkspaceLayout assetLayout,
        AssetDocumentRegistry registry, string? projectPath)
    {
        _nativeLayoutOperation = true;
        try
        {
            if (projectPath is not null) OpenProjectCore(projectPath);
            else
            {
                DisposeNativeDocuments();

                _faces.CloseAllDocuments();
                _session.CloseProject();
                _machines.CloseProject();
                Clear3DForProjectChange();
            }

            _assetRegistry = registry;
            foreach (var saved in assetLayout.Documents)
            {
                var asset = registry.Documents.Single(document => document.Id == saved.Id);
                var viewState = new FloatingAssetState(saved.Kind, saved.Path, saved.LibraryRoot,
                    new WorkspaceWindowState(80, 80, 1200, 800), saved.Viewport, saved.SourceSelection,
                    saved.Kind == "Panel2D" ? saved.Selection : null,
                    saved.Kind == "Face" ? saved.Selection?.FirstOrDefault() : null,
                    saved.Scene, saved.MachineGraph);
                var entry = await AddNativeEditorAsync(asset, viewState);
                if (saved.Kind == "Panel2D")
                {
                    var available = entry.Editor._session.Elements.Select(element => element.Id).ToHashSet(StringComparer.Ordinal);
                    entry.Editor._session.SelectElements((saved.Selection ?? []).Where(available.Contains).ToArray());
                }
                else if (saved.Kind == "Face")
                    entry.Editor.Faces.Select(entry.Editor.Faces.Elements.Any(element => element.Id == saved.Selection?.FirstOrDefault())
                        ? saved.Selection?.FirstOrDefault() : null);
            }

            var known = WorkspaceLayoutState.LeafMap(_factory.CreateLayout()).ToDictionary(pair => pair.Key,
                pair => pair.Value, StringComparer.Ordinal);
            if (assetLayout.Documents.Length != 0 || !ContainsLeaf(assetLayout.Layout, "Authoring"))
                known.Remove("Authoring");
            foreach (var entry in _nativeDocuments.Values) known.Add(entry.Document.Id, entry.Document);
            var restored = WorkspaceLayoutState.Restore(assetLayout.Layout, known);
            ReplaceLayout(restored);

            SceneLibraryRoot = state.LibraryRoot ?? string.Empty;
            RestartLibraryWatcher();
            _libraryTask = RefreshLibraryAsync();
            SetAssetBrowserSource(state.LibraryBrowser);
            _assetSearch.Text = state.AssetSearch;
            ConfigureNativePreview(state.Emulation ?? new());
            Application.Current!.RequestedThemeVariant = state.Theme == "Dark" ? ThemeVariant.Dark
                : state.Theme == "Light" ? ThemeVariant.Light : ThemeVariant.Default;
            if (state.MainWindow is not null && TopLevel.GetTopLevel(this) is Window mainWindow)
                RestoreWindow(mainWindow, state.MainWindow);

            _focusedAsset = null;
            if (assetLayout.ActiveId is { } activeId && _nativeDocuments.TryGetValue(activeId, out var active))
            {
                _factory.SetActiveDockable(active.Document);
                FocusNativeEditor(active);
            }
            else if (_nativeDocuments.Values.LastOrDefault() is { } first)
            {
                _factory.SetActiveDockable(first.Document);
                FocusNativeEditor(first);
            }
            Refresh();
        }
        finally { _nativeLayoutOperation = false; }
    }

    private static bool ContainsLeaf(WorkspaceDockNode node, string id)
    {
        if ((node.Kind is "Tool" or "Document") && node.Id == id) return true;
        return (node.Children ?? []).Any(child => ContainsLeaf(child, id)) ||
            (node.Hidden ?? []).Any(child => ContainsLeaf(child, id)) ||
            (node.Left ?? []).Any(child => ContainsLeaf(child, id)) ||
            (node.Right ?? []).Any(child => ContainsLeaf(child, id)) ||
            (node.Top ?? []).Any(child => ContainsLeaf(child, id)) ||
            (node.Bottom ?? []).Any(child => ContainsLeaf(child, id)) ||
            (node.Windows ?? []).Any(window => window is not null && ContainsLeaf(window.Layout, id));
    }
}
