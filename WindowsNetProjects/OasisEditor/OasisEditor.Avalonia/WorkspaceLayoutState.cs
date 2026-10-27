using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.ObjectModel;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm.Controls;
using Dock.Model.Mvvm.Core;

namespace OasisEditor.Avalonia;

public sealed record WorkspaceWindowState(double X, double Y, double Width, double Height, string State = "Normal");
public sealed record WorkspaceViewportState(double Zoom, double PanX, double PanY, bool Fit)
{
    public void Validate()
    {
        if (!double.IsFinite(Zoom) || Zoom is < .01 or > 64 || !double.IsFinite(PanX) || !double.IsFinite(PanY) || Math.Abs(PanX) > 10000000 || Math.Abs(PanY) > 10000000)
            throw new InvalidDataException("Workspace viewport state is invalid.");
    }
}
public sealed record WorkspaceSceneCameraState(double TargetX, double TargetY, double TargetZ, double Distance, double Yaw, double Pitch, double FieldOfView = 45)
{
    public void Validate()
    {
        if (new[] { TargetX, TargetY, TargetZ, Distance, Yaw, Pitch, FieldOfView }.Any(value => !double.IsFinite(value) || Math.Abs(value) > float.MaxValue)
            || Distance is < .0001 or > 1000000 || Math.Abs(Pitch) > 1.56 || FieldOfView is < 5 or > 120)
            throw new InvalidDataException("Workspace 3D camera state is invalid.");
    }
}
public sealed record WorkspaceSceneState(string? Mode = null, string? Cabinet = null, string? Prefab = null,
    string? Selection = null, WorkspaceSceneCameraState? Camera = null, bool ShowVisuals = true,
    bool ShowColliders = false, bool ShowTriggers = false, string? Reel = null)
{
    public void Validate()
    {
        if (Mode is not (null or "Machine" or "Cabinet3D" or "Prefab" or "Reel")) throw new InvalidDataException("Workspace 3D mode is invalid.");
        if ((Selection?.Length ?? 0) > 1024) throw new InvalidDataException("Workspace 3D selection is too long.");
        Camera?.Validate();
        if (Mode == "Cabinet3D" ? Cabinet is null || Prefab is not null || Reel is not null
            : Mode == "Prefab" ? Prefab is null || Cabinet is not null || Reel is not null
            : Mode == "Reel" ? Reel is null || Cabinet is not null || Prefab is not null
            : Cabinet is not null || Prefab is not null || Reel is not null)
            throw new InvalidDataException("Workspace 3D asset paths do not match the active mode.");
    }
}
public sealed record WorkspaceDockNode(string Kind, string? Id = null, double? Proportion = null,
    string? Orientation = null, string? Alignment = null, int Active = -1, int Default = -1,
    WorkspaceDockNode[]? Children = null, WorkspaceDockNode[]? Hidden = null,
    WorkspaceDockNode[]? Left = null, WorkspaceDockNode[]? Right = null,
    WorkspaceDockNode[]? Top = null, WorkspaceDockNode[]? Bottom = null,
    WorkspaceFloatingWindow[]? Windows = null);
public sealed record WorkspaceFloatingWindow(WorkspaceWindowState Bounds, WorkspaceDockNode Layout);
public sealed record WorkspaceState(int Version, string Theme, WorkspaceDockNode Layout,
    string? Project = null, string? Panel = null, string? Face = null, string ActiveDocument = "Panel2D",
    string[]? PanelSelection = null, string? FaceSelection = null, string? AssetSearch = null,
    WorkspaceWindowState? MainWindow = null, string? SourceSelection = null, WorkspaceViewportState? Viewport = null,
    string? Machine = null, NativeEmulationPreferences? Emulation = null, WorkspaceSceneState? Scene3D = null);

/// <summary>Allow-listed layout data. Never deserializes arbitrary CLR types, views, contexts or commands.</summary>
public static class WorkspaceLayoutState
{
    private static readonly JsonSerializerOptions Json = new()
    { WriteIndented = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, MaxDepth = 64 };

    public static string Serialize(WorkspaceState state) => JsonSerializer.Serialize(state, Json);
    public static WorkspaceState Deserialize(string json)
    {
        if (json.Length > 512 * 1024) throw new InvalidDataException("Workspace settings exceed the size limit.");
        try { return JsonSerializer.Deserialize<WorkspaceState>(json, Json) ?? throw new InvalidDataException("Workspace settings are empty."); }
        catch (JsonException e) { throw new InvalidDataException("Invalid workspace settings: " + e.Message, e); }
    }

    public static WorkspaceDockNode Capture(IDockable dockable)
    {
        var kind = dockable switch
        {
            IRootDock => "Root", IProportionalDock => "Split", IToolDock => "Tools", IDocumentDock => "Documents",
            ProportionalDockSplitter => "Splitter", ITool => "Tool", IDocument => "Document",
            _ => throw new InvalidDataException("Unsupported dock layout item: " + dockable.GetType().Name)
        };
        var dock = dockable as IDock;
        var children = dock?.VisibleDockables?.ToArray() ?? [];
        var root = dockable as IRootDock;
        WorkspaceDockNode[]? Nodes(IList<IDockable>? values) => values?.Select(Capture).ToArray();
        return new(kind, dockable.Id, double.IsFinite(dockable.Proportion) ? dockable.Proportion : null,
            (dockable as IProportionalDock)?.Orientation.ToString(), (dockable as IToolDock)?.Alignment.ToString(),
            Array.IndexOf(children, dock?.ActiveDockable), Array.IndexOf(children, dock?.DefaultDockable), children.Select(Capture).ToArray(),
            Nodes(root?.HiddenDockables), Nodes(root?.LeftPinnedDockables), Nodes(root?.RightPinnedDockables),
            Nodes(root?.TopPinnedDockables), Nodes(root?.BottomPinnedDockables),
            root?.Windows?.Select(window =>
            {
                if (window.Host is not null) window.Save();
                return new WorkspaceFloatingWindow(new(window.X, window.Y, window.Width, window.Height, window.WindowState.ToString()),
                    Capture(window.Layout ?? throw new InvalidDataException("Floating workspace window has no layout.")));
            }).ToArray());
    }

    public static IRootDock Restore(WorkspaceDockNode state, IReadOnlyDictionary<string, IDockable> known)
    {
        var used = new HashSet<string>(StringComparer.Ordinal);
        var count = 0;
        IDockable Build(WorkspaceDockNode node, int depth)
        {
            if (node is null) throw new InvalidDataException("Workspace layout contains an empty node.");
            if (++count > 256 || depth > 24) throw new InvalidDataException("Workspace layout is too complex.");
            if (node.Proportion.HasValue && (!double.IsFinite(node.Proportion.Value) || node.Proportion is < 0 or > 1000000))
                throw new InvalidDataException("Workspace split proportions are invalid.");
            IDockable result;
            if (node.Kind is "Tool" or "Document")
            {
                if (node.Id is null || !known.TryGetValue(node.Id, out result!) || !used.Add(node.Id))
                    throw new InvalidDataException("Workspace contains an unknown or repeated pane.");
                if ((node.Kind == "Tool") != (result is ITool) || (node.Children?.Length ?? 0) != 0)
                    throw new InvalidDataException("Workspace pane type is invalid.");
            }
            else result = node.Kind switch
            {
                "Root" => new RootDock(),
                "Split" => new ProportionalDock { Orientation = Parse<Dock.Model.Core.Orientation>(node.Orientation) },
                "Tools" => new ToolDock { Alignment = Parse<Alignment>(node.Alignment), CanClose = false },
                "Documents" => new DocumentDock { CanCloseLastDockable = false },
                "Splitter" => new ProportionalDockSplitter(),
                _ => throw new InvalidDataException("Workspace contains an unsupported layout type.")
            };
            if (node.Id is not null) result.Id = node.Id;
            result.Proportion = node.Proportion ?? double.NaN;
            if (result is IDock dock)
            {
                var children = (node.Children ?? []).Select(child => Build(child, depth + 1)).ToList();
                dock.VisibleDockables = new ObservableCollection<IDockable>(children);
                if (node.Active < -1 || node.Active >= children.Count || node.Default < -1 || node.Default >= children.Count)
                    throw new InvalidDataException("Workspace tab selection is invalid.");
                dock.ActiveDockable = node.Active < 0 ? children.FirstOrDefault() : children[node.Active];
                dock.DefaultDockable = node.Default < 0 ? dock.ActiveDockable : children[node.Default];
            }
            if (result is IRootDock root)
            {
                IList<IDockable>? List(WorkspaceDockNode[]? nodes) => nodes is null ? null : new ObservableCollection<IDockable>(nodes.Select(n => Build(n, depth + 1)));
                root.HiddenDockables = List(node.Hidden); root.LeftPinnedDockables = List(node.Left); root.RightPinnedDockables = List(node.Right);
                root.TopPinnedDockables = List(node.Top); root.BottomPinnedDockables = List(node.Bottom);
                if ((node.Windows?.Length ?? 0) > 16) throw new InvalidDataException("Too many floating workspace windows.");
                root.Windows = new ObservableCollection<IDockWindow>((node.Windows ?? []).Select((w, i) =>
                {
                    if (w?.Bounds is null || w.Layout is null) throw new InvalidDataException("Floating window data is incomplete.");
                    ValidateBounds(w.Bounds);
                    return (IDockWindow)new DockWindow { Id = "Window" + i, X = w.Bounds.X, Y = w.Bounds.Y,
                        Width = w.Bounds.Width, Height = w.Bounds.Height, WindowState = Parse<DockWindowState>(w.Bounds.State),
                        Layout = Build(w.Layout, depth + 1) as IRootDock ?? throw new InvalidDataException("Floating window root is invalid.") };
                }));
            }
            return result;
        }
        var root = Build(state, 0) as IRootDock ?? throw new InvalidDataException("Workspace must have a root layout.");
        if (!known.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(used)) throw new InvalidDataException("Workspace is missing required panes.");
        return root;
    }

    public static IReadOnlyDictionary<string, IDockable> LeafMap(IDockable layout)
    {
        var map = new Dictionary<string, IDockable>(StringComparer.Ordinal);
        void Visit(IDockable item)
        {
            if (item is ITool or IDocument)
            {
                var id = item.Id ?? throw new InvalidDataException("Pane has no ID.");
                if (map.TryGetValue(id, out var previous) && !ReferenceEquals(previous, item)) throw new InvalidDataException("Pane ID is repeated.");
                map[id] = item;
            }
            if (item is IDock dock) foreach (var child in dock.VisibleDockables ?? []) Visit(child);
            if (item is IRootDock root)
            {
                foreach (var list in new[] { root.HiddenDockables, root.LeftPinnedDockables, root.RightPinnedDockables, root.TopPinnedDockables, root.BottomPinnedDockables })
                    foreach (var child in list ?? []) Visit(child);
                foreach (var window in root.Windows ?? []) if (window.Layout is not null) Visit(window.Layout);
            }
        }
        Visit(layout); return map;
    }

    public static void ValidateBounds(WorkspaceWindowState bounds)
    {
        if (bounds is null) throw new InvalidDataException("Workspace window placement is missing.");
        if (!double.IsFinite(bounds.X) || !double.IsFinite(bounds.Y) || Math.Abs(bounds.X) > 100000 || Math.Abs(bounds.Y) > 100000
            || !double.IsFinite(bounds.Width) || !double.IsFinite(bounds.Height) || bounds.Width is < 100 or > 16384 || bounds.Height is < 100 or > 16384)
            throw new InvalidDataException("Workspace window placement is invalid.");
        if (bounds.State is not ("Normal" or "Minimized" or "Maximized" or "FullScreen")) throw new InvalidDataException("Workspace window state is invalid.");
    }
    private static T Parse<T>(string? value) where T : struct, Enum => value is not null && Enum.TryParse<T>(value, out var parsed) && Enum.IsDefined(parsed)
        ? parsed : throw new InvalidDataException("Workspace contains an invalid " + typeof(T).Name + ".");
}
