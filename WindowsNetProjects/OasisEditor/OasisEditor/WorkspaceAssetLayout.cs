using System.Text.Json;
using System.Text.Json.Serialization;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm.Controls;

namespace OasisEditor.Avalonia;

/// <summary>View state for one opened asset document in a persisted workspace.</summary>
public sealed record AssetDocumentViewState(string Kind, string Path, string? LibraryRoot, string Id,
    WorkspaceViewportState? Viewport = null, string[]? Selection = null, string? SourceSelection = null,
    WorkspaceSceneState? Scene = null, bool MachineGraph = false);

/// <summary>Version 2 workspace layout with one dock document per opened asset.</summary>
public sealed record AssetWorkspaceLayout(int Version, WorkspaceDockNode Layout,
    AssetDocumentViewState[] Documents, string? ActiveId);

/// <summary>Strict persistence and preflight validation for multi-document workspace layouts.</summary>
public static class AssetWorkspaceLayoutState
{
    private const int CurrentVersion = 2;
    private const int MaxDocuments = 128;
    private const int MaxSelectionItems = 10_000;
    private const int MaxJsonBytes = 512 * 1024;
    private static readonly HashSet<string> DocumentKinds = new(StringComparer.Ordinal)
        { "Panel2D", "Face", "Machine", "Cabinet3D", "Prefab", "Reel" };
    private static readonly HashSet<string> LibraryKinds = new(StringComparer.Ordinal)
        { "Cabinet3D", "Prefab", "Reel" };
    private static readonly JsonSerializerOptions Json = new()
    { WriteIndented = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, MaxDepth = 64 };

    public static string Serialize(AssetWorkspaceLayout state)
    {
        Validate(state);
        var json = JsonSerializer.Serialize(state, Json);
        if (System.Text.Encoding.UTF8.GetByteCount(json) > MaxJsonBytes)
            throw new InvalidDataException("Workspace settings exceed the size limit.");
        return json;
    }

    public static AssetWorkspaceLayout Deserialize(string json, string? projectDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(json);
        if (System.Text.Encoding.UTF8.GetByteCount(json) > MaxJsonBytes)
            throw new InvalidDataException("Workspace settings exceed the size limit.");
        try
        {
            var state = JsonSerializer.Deserialize<AssetWorkspaceLayout>(json, Json)
                ?? throw new InvalidDataException("Workspace settings are empty.");
            Validate(state, projectDirectory);
            return state;
        }
        catch (JsonException e) { throw new InvalidDataException("Invalid workspace settings: " + e.Message, e); }
    }

    /// <summary>Validates data and the complete dock graph before any workspace UI is changed.</summary>
    public static void Validate(AssetWorkspaceLayout state, string? projectDirectory = null)
    {
        if (state is null || state.Version != CurrentVersion || state.Layout is null || state.Documents is null)
            throw new InvalidDataException("Workspace asset layout is incomplete or has an unsupported version.");
        if (state.Documents.Length > MaxDocuments) throw new InvalidDataException("Too many workspace documents.");

        var idComparer = StringComparer.Ordinal;
        var pathComparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var ids = new HashSet<string>(idComparer);
        var paths = new HashSet<string>(pathComparer);
        foreach (var document in state.Documents)
        {
            if (document is null || !DocumentKinds.Contains(document.Kind) ||
                string.IsNullOrWhiteSpace(document.Id) || document.Id.Length > 256 ||
                document.Id.Any(char.IsControl) || string.IsNullOrWhiteSpace(document.Path))
                throw new InvalidDataException("Invalid workspace document.");
            if (!ids.Add(document.Id)) throw new InvalidDataException("Workspace document IDs are repeated.");

            string normalized;
            try { normalized = OasisEditor.WorkspacePathPolicy.NormalizeRelativePath(document.Path).Replace('\\', '/'); }
            catch (ArgumentException e) { throw new InvalidDataException("Workspace document path is invalid.", e); }
            catch (InvalidDataException e) { throw new InvalidDataException("Workspace document path is invalid.", e); }
            var documentRelativePath = normalized;

            if (document.LibraryRoot is { } libraryRoot)
            {
                if (!LibraryKinds.Contains(document.Kind) || string.IsNullOrWhiteSpace(libraryRoot))
                    throw new InvalidDataException("Workspace document library root is invalid for this asset kind.");
                string fullRoot;
                try { fullRoot = Path.GetFullPath(libraryRoot); }
                catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
                    { throw new InvalidDataException("Workspace document library root is invalid.", e); }
                OasisEditor.WorkspacePathPolicy.RejectRootReparsePoints(fullRoot, requireExists: true);
                if (projectDirectory is not null)
                    _ = OasisEditor.WorkspacePathPolicy.ResolveConfined(fullRoot, normalized, requireExists: false);
                else
                    _ = OasisEditor.WorkspacePathPolicy.NormalizeRelativePath(normalized);
                normalized = fullRoot + "|" + normalized;
            }
            else if (projectDirectory is not null)
                _ = OasisEditor.WorkspacePathPolicy.ResolveConfined(projectDirectory, normalized, requireExists: false);

            if (!paths.Add(document.Kind + "|" + normalized))
                throw new InvalidDataException("Duplicate workspace document path.");

            document.Viewport?.Validate();
            if (document.Selection is { Length: > MaxSelectionItems } ||
                document.Selection?.Any(value => value is null || value.Length > 1024 || value.Any(char.IsControl)) == true)
                throw new InvalidDataException("Workspace document selection is invalid.");
            if (document.SourceSelection is { Length: > 4096 } || document.SourceSelection?.Any(char.IsControl) == true)
                throw new InvalidDataException("Workspace source selection is invalid.");
            document.Scene?.Validate();
            if (document.MachineGraph && document.Kind != "Machine")
                throw new InvalidDataException("Only a machine document can contain machine graph state.");
            var isSceneDocument = document.Kind is "Machine" or "Cabinet3D" or "Prefab" or "Reel";
            if (isSceneDocument != (document.Scene is not null))
                throw new InvalidDataException("Workspace scene state does not match its document kind.");
            if (document.Scene is { } scene)
            {
                if (scene.Mode != document.Kind || scene.Scope != (document.LibraryRoot is null ? "Project" : "Library"))
                    throw new InvalidDataException("Workspace scene mode or scope does not match its document.");
                var scenePath = scene.Mode switch
                {
                    "Cabinet3D" => scene.Cabinet,
                    "Prefab" => scene.Prefab,
                    "Reel" => scene.Reel,
                    _ => null
                };
                if (scenePath is not null &&
                    !string.Equals(OasisEditor.WorkspacePathPolicy.NormalizeRelativePath(scenePath).Replace('\\', '/'), documentRelativePath,
                        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                    throw new InvalidDataException("Workspace scene path does not match its document path.");
            }
        }

        if (state.ActiveId is not null && !ids.Contains(state.ActiveId))
            throw new InvalidDataException("The active workspace document is unknown.");
        if (state.Documents.Length == 0 && state.ActiveId is not null)
            throw new InvalidDataException("An empty workspace cannot have an active asset document.");

        var leaves = CollectLeaves(state.Layout);
        var expected = new Dictionary<string, IDockable>(StringComparer.Ordinal)
        {
            ["Hierarchy"] = new Tool { Id = "Hierarchy" },
            ["Project"] = new Tool { Id = "Project" },
            ["Inspector"] = new Tool { Id = "Inspector" },
            ["Output"] = new Tool { Id = "Output" }
        };
        foreach (var document in state.Documents) expected.Add(document.Id, new Document { Id = document.Id });
        if (state.Documents.Length == 0 && leaves.TryGetValue("Authoring", out var authoringKind))
        {
            if (authoringKind != "Document") throw new InvalidDataException("The empty authoring placeholder has an invalid pane type.");
            expected.Add("Authoring", new Document { Id = "Authoring" });
        }
        if (!leaves.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(expected.Keys))
            throw new InvalidDataException("Workspace layout panes do not match the saved documents.");
        foreach (var pair in leaves)
            if ((expected[pair.Key] is ITool) != (pair.Value == "Tool"))
                throw new InvalidDataException("Workspace layout pane type is invalid.");

        // Reuse the existing layout parser to validate structure, bounds, indexes and duplicate nodes.
        _ = WorkspaceLayoutState.Restore(state.Layout, expected);
    }

    private static Dictionary<string, string> CollectLeaves(WorkspaceDockNode root)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var count = 0;
        void Visit(WorkspaceDockNode? node, int depth)
        {
            if (node is null || depth > 24 || ++count > 256)
                throw new InvalidDataException("Workspace layout is incomplete or too complex.");
            if (node.Kind is "Tool" or "Document")
            {
                if (string.IsNullOrWhiteSpace(node.Id) || !result.TryAdd(node.Id, node.Kind))
                    throw new InvalidDataException("Workspace layout contains a missing or repeated pane ID.");
            }
            foreach (var child in node.Children ?? []) Visit(child, depth + 1);
            foreach (var child in node.Hidden ?? []) Visit(child, depth + 1);
            foreach (var child in node.Left ?? []) Visit(child, depth + 1);
            foreach (var child in node.Right ?? []) Visit(child, depth + 1);
            foreach (var child in node.Top ?? []) Visit(child, depth + 1);
            foreach (var child in node.Bottom ?? []) Visit(child, depth + 1);
            foreach (var window in node.Windows ?? [])
            {
                if (window is null || window.Bounds is null) throw new InvalidDataException("Workspace floating window is incomplete.");
                Visit(window.Layout, depth + 1);
            }
        }
        Visit(root, 0);
        return result;
    }
}
