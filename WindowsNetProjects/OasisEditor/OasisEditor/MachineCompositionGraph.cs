using System.ComponentModel;
using System.IO;
using OasisEditor.Features.CabinetEditor.Models;
using OasisEditor.Features.CabinetEditor.Services;

namespace OasisEditor;

public enum MachineCompositionNodeKind { Machine, Runtime, Cabinet, Face, Panel2D, Reel, MissingReelAssignment }
public enum MachineCompositionEdgeKind { Composition, Provenance }
public enum MachineCompositionDiagnosticSeverity { Warning, Error }

public sealed record MachineCompositionNode(
    string Id, MachineCompositionNodeKind Kind, string Title, string Metadata,
    AssetReferenceScope? Scope = null, string? ManifestPath = null, bool IsMissing = false,
    double X = 0, double Y = 0, double Width = 190, double Height = 92, int LayoutOrder = int.MaxValue);

public sealed record MachineCompositionEdge(string FromNodeId, string ToNodeId, string Label, MachineCompositionEdgeKind Kind,
    string[]? LogicalReelRoleIds = null, string? RelationshipId = null);
public sealed record MachineCompositionRoute(MachineCompositionEdge Edge, MachineCompositionPoint SourcePort,
    MachineCompositionPoint DestinationPort, IReadOnlyList<MachineCompositionPoint> Points,
    MachineCompositionPoint LabelPosition, double LabelMaxWidth);
public readonly record struct MachineCompositionPoint(double X, double Y);
public sealed record MachineCompositionDiagnostic(MachineCompositionDiagnosticSeverity Severity, string Message, string? NodeId = null);
public sealed record MachineCompositionGraph(IReadOnlyList<MachineCompositionNode> Nodes, IReadOnlyList<MachineCompositionEdge> Edges,
    IReadOnlyList<MachineCompositionRoute> Routes, IReadOnlyList<MachineCompositionDiagnostic> Diagnostics)
{
    public static MachineCompositionGraph Empty { get; } = new([], [], [], []);
}

/// <summary>Builds a transient explanation of a Machine by following only its explicit references.</summary>
public sealed class MachineCompositionGraphBuilder
{
    private readonly AssetReferenceResolver _resolver = new();
    private readonly ProjectAssetPathService _paths = new();
    private readonly ICabinetFaceTargetDetector _cabinetTargetDetector;

    public MachineCompositionGraphBuilder(ICabinetFaceTargetDetector? cabinetTargetDetector = null) =>
        _cabinetTargetDetector = cabinetTargetDetector ?? new GlbCabinetFaceTargetDetector();

    public MachineCompositionGraph Build(MachineDocument machine, EditorProject project, string libraryRoot,
        IReadOnlyList<DocumentTabViewModel>? openDocuments = null)
    {
        ArgumentNullException.ThrowIfNull(machine);
        ArgumentNullException.ThrowIfNull(project);
        var nodes = new Dictionary<string, MachineCompositionNode>(StringComparer.OrdinalIgnoreCase);
        var edges = new List<MachineCompositionEdge>();
        var diagnostics = new List<MachineCompositionDiagnostic>();
        var machineId = $"machine:{machine.Id}";
        nodes[machineId] = new(machineId, MachineCompositionNodeKind.Machine, machine.DisplayName,
            $"{machine.Runtime.Platform} · {machine.InputDefinitions.Count} inputs");
        const string runtimeId = "runtime";
        nodes[runtimeId] = new(runtimeId, MachineCompositionNodeKind.Runtime, machine.Runtime.Platform.ToString(),
            $"Emulation · {machine.InputDefinitions.Count} inputs");
        edges.Add(new(machineId, runtimeId, string.Empty, MachineCompositionEdgeKind.Composition));

        string? cabinetId = null;
        CabinetDocument? cabinet = null;
        IReadOnlyList<CabinetFaceTarget> cabinetTargets = [];
        if (machine.CabinetAsset is null)
            diagnostics.Add(new(MachineCompositionDiagnosticSeverity.Warning, "No Cabinet is assigned to this Machine.", machineId));
        else
        {
            cabinetId = "cabinet:" + machine.CabinetAsset;
            var result = ReadReference(project, libraryRoot, machine.CabinetAsset, json => CabinetDocumentStorage.TryRead(json, out cabinet));
            var title = result.Exists && cabinet is not null ? PackageName(result.Path) : "Missing Cabinet";
            var targetDiscovery = cabinet is null ? CabinetFaceTargetDiscoveryResult.Unavailable :
                CabinetFaceTargetDiscovery.Discover(result.Path, cabinet, _cabinetTargetDetector);
            cabinetTargets = targetDiscovery.Targets;
            var metadata = targetDiscovery.Succeeded
                ? $"{machine.CabinetAsset.Scope} · {cabinetTargets.Count} face targets"
                : machine.CabinetAsset.Scope.ToString();
            nodes[cabinetId] = new(cabinetId, MachineCompositionNodeKind.Cabinet, title,
                result.Exists && cabinet is not null ? metadata : $"{machine.CabinetAsset.Path} · {machine.CabinetAsset.Scope}",
                machine.CabinetAsset.Scope, result.Exists ? result.Path : null, !result.Valid);
            edges.Add(new(machineId, cabinetId, string.Empty, MachineCompositionEdgeKind.Composition));
            if (!result.Valid) diagnostics.Add(new(MachineCompositionDiagnosticSeverity.Error,
                result.Exists ? $"Cabinet reference is invalid: {machine.CabinetAsset.Path}" : $"Cabinet asset is missing: {machine.CabinetAsset.Path}", cabinetId));
        }

        var targetNames = cabinetTargets.ToDictionary(x => x.Id, x => x.DisplayName, StringComparer.Ordinal);
        var orderedAssignments = OrderSurfaceAssignments(machine.SurfaceAssignments, cabinetTargets);
        for (var assignmentIndex = 0; assignmentIndex < orderedAssignments.Count; assignmentIndex++)
        {
            var assignment = orderedAssignments[assignmentIndex];
            var normalized = ProjectAssetPathService.NormalizeProjectRelativePath(assignment.FaceAssetPath);
            var faceId = "face:" + normalized;
            var facePath = TryProjectPath(project, normalized);
            var openFace = openDocuments?.FirstOrDefault(x => x.Document.DocumentType == EditorDocumentType.Face && SamePath(x.FilePath, facePath));
            FaceDocumentModel? face = openFace?.GetFaceDocument();
            var exists = File.Exists(facePath);
            var valid = face is not null;
            string? error = null;
            if (!valid && exists)
            {
                try
                {
                    if (FaceDocumentStorage.TryReadValidated(File.ReadAllText(facePath), out var file, out var readError))
                    { face = FaceDocumentStorage.ToModel(file); valid = true; }
                    else error = readError;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { error = ex.Message; }
            }
            if (!nodes.ContainsKey(faceId))
                nodes[faceId] = new(faceId, MachineCompositionNodeKind.Face, valid ? (string.IsNullOrWhiteSpace(face!.Title) ? PackageName(facePath) : face.Title) : "Missing Face",
                    valid ? "Project" : $"{normalized} · Project", AssetReferenceScope.Project, valid ? facePath : null, !valid, LayoutOrder: assignmentIndex);
            else if (assignmentIndex < nodes[faceId].LayoutOrder) nodes[faceId] = nodes[faceId] with { LayoutOrder = assignmentIndex };
            var targetLabel = targetNames.GetValueOrDefault(assignment.TargetId, assignment.TargetId);
            edges.Add(new(cabinetId ?? machineId, faceId, targetLabel, MachineCompositionEdgeKind.Composition, RelationshipId: assignment.TargetId));
            if (!valid)
            {
                diagnostics.Add(new(MachineCompositionDiagnosticSeverity.Error, exists
                    ? $"Face assigned to '{assignment.TargetId}' is invalid: {error ?? normalized}"
                    : $"Face assigned to '{assignment.TargetId}' is missing: {normalized}", faceId));
                continue;
            }

            AddProvenance(face!, faceId, project, nodes, edges, diagnostics);
            AddFaceReels(face!, faceId, machine, project, libraryRoot, nodes, edges, diagnostics);
        }

        return Layout(nodes.Values, edges, diagnostics);
    }

    private void AddProvenance(FaceDocumentModel face, string faceId, EditorProject project,
        Dictionary<string, MachineCompositionNode> nodes, List<MachineCompositionEdge> edges, List<MachineCompositionDiagnostic> diagnostics)
    {
        var source = face.SourcePanel2DDocumentPath ?? face.Artwork?.Source.Panel2DDocumentPath;
        if (string.IsNullOrWhiteSpace(source)) return;
        string normalized;
        try { normalized = ProjectAssetPathService.NormalizeProjectRelativePath(source); }
        catch (ArgumentException)
        { diagnostics.Add(new(MachineCompositionDiagnosticSeverity.Warning, $"Face '{face.Title}' has invalid Panel2D provenance.", faceId)); return; }
        var id = "panel:" + normalized;
        var path = TryProjectPath(project, normalized);
        var valid = false;
        if (File.Exists(path))
        {
            try { valid = Panel2DDocumentStorage.TryReadValidated(File.ReadAllText(path), out _, out _); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
        if (!nodes.ContainsKey(id)) nodes[id] = new(id, MachineCompositionNodeKind.Panel2D, valid ? PackageName(path) : "Missing Panel2D",
            valid ? "Source · Project" : $"{normalized} · Project", AssetReferenceScope.Project, valid ? path : null, !valid);
        if (!edges.Any(edge => edge.Kind == MachineCompositionEdgeKind.Provenance
            && string.Equals(edge.FromNodeId, id, StringComparison.OrdinalIgnoreCase)
            && string.Equals(edge.ToNodeId, faceId, StringComparison.OrdinalIgnoreCase)))
            edges.Add(new(id, faceId, "source", MachineCompositionEdgeKind.Provenance));
        if (!valid) diagnostics.Add(new(MachineCompositionDiagnosticSeverity.Warning, $"Panel2D provenance is unresolved: {normalized}", id));
    }

    private void AddFaceReels(FaceDocumentModel face, string faceId, MachineDocument machine, EditorProject project, string libraryRoot,
        Dictionary<string, MachineCompositionNode> nodes, List<MachineCompositionEdge> edges, List<MachineCompositionDiagnostic> diagnostics)
    {
        foreach (var role in face.Elements.OfType<FaceReelMount>().Select(x => x.LinkedMachineObjectReference)
                     .Where(x => x is { Kind: MachineObjectKind.Reel }).Select(x => x!.Value).Distinct().OrderBy(x => x.Id))
        {
            var assignment = machine.ReelAssignments.FirstOrDefault(x => x.MachineReelReference == role);
            if (assignment is null)
            {
                var missingId = $"missing-reel:{role}";
                if (!nodes.ContainsKey(missingId)) nodes[missingId] = new(missingId, MachineCompositionNodeKind.MissingReelAssignment,
                    "Unassigned Reel", ReelLabel(role), null, null, true);
                edges.Add(new(faceId, missingId, ReelLabel(role), MachineCompositionEdgeKind.Composition, [role.Id]));
                diagnostics.Add(new(MachineCompositionDiagnosticSeverity.Error, $"{face.Title} requires {role}, but the Machine has no Reel assignment.", missingId));
                continue;
            }
            var id = "reel:" + assignment.ReelAsset;
            ReelDocument? reel = null;
            var result = ReadReference(project, libraryRoot, assignment.ReelAsset, json => ReelDocumentStorage.TryRead(json, out reel, out _));
            if (!nodes.ContainsKey(id)) nodes[id] = new(id, MachineCompositionNodeKind.Reel, result.Valid ? reel!.DisplayName : "Missing Reel",
                result.Valid ? $"{reel!.DiameterMm:0.#} × {reel.WidthMm:0.#} mm · {assignment.ReelAsset.Scope}" : $"{assignment.ReelAsset.Path} · {assignment.ReelAsset.Scope}",
                assignment.ReelAsset.Scope, result.Valid ? result.Path : null, !result.Valid);
            edges.Add(new(faceId, id, ReelLabel(role), MachineCompositionEdgeKind.Composition, [role.Id]));
            if (!result.Valid && diagnostics.All(x => x.NodeId != id)) diagnostics.Add(new(MachineCompositionDiagnosticSeverity.Error,
                result.Exists ? $"Reel asset is invalid: {assignment.ReelAsset.Path}" : $"Reel asset is missing: {assignment.ReelAsset.Path}", id));
        }
    }

    private (string Path, bool Exists, bool Valid) ReadReference(EditorProject project, string root, AssetReference reference, Func<string, bool> read)
    {
        try
        {
            var path = _resolver.Resolve(project, root, reference);
            if (!File.Exists(path)) return (path, false, false);
            try { return (path, true, read(File.ReadAllText(path))); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return (path, true, false); }
        }
        catch (InvalidOperationException) { return (string.Empty, false, false); }
    }

    private MachineCompositionGraph Layout(IEnumerable<MachineCompositionNode> source, List<MachineCompositionEdge> edges, List<MachineCompositionDiagnostic> diagnostics)
    {
        var nodes = source.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
        var faces = nodes.Values.Where(x => x.Kind == MachineCompositionNodeKind.Face)
            .OrderBy(x => x.LayoutOrder).ThenBy(x => x.Id, StringComparer.OrdinalIgnoreCase).ToArray();
        var reels = Nodes(MachineCompositionNodeKind.Reel).Concat(Nodes(MachineCompositionNodeKind.MissingReelAssignment))
            .OrderBy(node => FirstSourceRow(node.Id, faces, edges)).ThenBy(node => node.Id, StringComparer.OrdinalIgnoreCase).ToArray();
        Place(Nodes(MachineCompositionNodeKind.Machine), 30, 190, 125);
        Place(Nodes(MachineCompositionNodeKind.Runtime), 290, 35, 125);
        Place(Nodes(MachineCompositionNodeKind.Cabinet), 290, 190, 125);
        Place(faces, 610, 75, 135);
        Place(reels, 940, 75, 135);
        var faceBottom = faces.Length == 0 ? 260 : faces.Max(x => nodes[x.Id].Y + x.Height);
        Place(Nodes(MachineCompositionNodeKind.Panel2D), 290, faceBottom + 145, 125);

        var aggregated = AggregateReelEdges(edges).OrderBy(x => x.FromNodeId).ThenBy(x => x.ToNodeId).ThenBy(x => x.Label).ToArray();
        var routes = RouteAll(aggregated, nodes);
        return new(nodes.Values.OrderBy(x => x.X).ThenBy(x => x.Y).ThenBy(x => x.Id, StringComparer.OrdinalIgnoreCase).ToArray(), aggregated, routes, diagnostics.ToArray());

        MachineCompositionNode[] Nodes(MachineCompositionNodeKind kind) => nodes.Values.Where(x => x.Kind == kind).OrderBy(x => x.Id, StringComparer.OrdinalIgnoreCase).ToArray();
        void Place(IEnumerable<MachineCompositionNode> items, double x, double y, double step)
        {
            var index = 0;
            foreach (var item in items) nodes[item.Id] = item with { X = x, Y = y + index++ * step };
        }
    }

    internal static IReadOnlyList<MachineSurfaceAssignment> OrderSurfaceAssignments(IReadOnlyList<MachineSurfaceAssignment> assignments,
        IReadOnlyList<CabinetFaceTarget> targets)
    {
        var order = targets.Select((target, index) => (target.Id, index)).ToDictionary(x => x.Id, x => x.index, StringComparer.Ordinal);
        return assignments.OrderBy(x => order.TryGetValue(x.TargetId, out var index) ? index : int.MaxValue)
            .ThenBy(x => order.ContainsKey(x.TargetId) ? string.Empty : x.TargetId, StringComparer.Ordinal)
            .ThenBy(x => x.FaceAssetPath, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static int FirstSourceRow(string reelId, IReadOnlyList<MachineCompositionNode> faces, IReadOnlyList<MachineCompositionEdge> edges)
    {
        var source = edges.Where(x => x.ToNodeId == reelId).Select(x => x.FromNodeId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var row = Array.FindIndex(faces.ToArray(), x => source.Contains(x.Id));
        return row < 0 ? int.MaxValue : row;
    }

    private static IEnumerable<MachineCompositionEdge> AggregateReelEdges(IEnumerable<MachineCompositionEdge> edges)
    {
        foreach (var group in edges.GroupBy(x => (x.FromNodeId, x.ToNodeId, x.Kind)))
        {
            var roles = group.SelectMany(x => x.LogicalReelRoleIds ?? []).Distinct(StringComparer.Ordinal)
                .OrderBy(x => int.TryParse(x, out var value) ? value : int.MaxValue).ThenBy(x => x, StringComparer.Ordinal).ToArray();
            if (roles.Length == 0) { foreach (var edge in group) yield return edge; continue; }
            var label = roles.Length == 1 ? $"Reel {roles[0]}" : $"Reels {string.Join(", ", roles)}";
            yield return new(group.Key.FromNodeId, group.Key.ToNodeId, label, group.Key.Kind, roles);
        }
    }

    private static MachineCompositionRoute[] RouteAll(IReadOnlyList<MachineCompositionEdge> edges,
        IReadOnlyDictionary<string, MachineCompositionNode> nodes)
    {
        var sourcePorts = AllocatePorts(edges, nodes, incoming: false);
        var destinationPorts = AllocatePorts(edges, nodes, incoming: true);
        return edges.Select(edge => Route(edge, nodes, sourcePorts[edge], destinationPorts[edge])).ToArray();
    }

    private static Dictionary<MachineCompositionEdge, MachineCompositionPoint> AllocatePorts(
        IReadOnlyList<MachineCompositionEdge> edges, IReadOnlyDictionary<string, MachineCompositionNode> nodes, bool incoming)
    {
        var result = new Dictionary<MachineCompositionEdge, MachineCompositionPoint>();
        foreach (var group in edges.GroupBy(edge => incoming ? edge.ToNodeId : edge.FromNodeId, StringComparer.OrdinalIgnoreCase))
        {
            var node = nodes[group.Key];
            var ordered = group.OrderBy(edge => edge.Kind == MachineCompositionEdgeKind.Composition ? 0 : 1)
                .ThenBy(edge => RelatedNodeOrder(edge, nodes, incoming))
                .ThenBy(edge => edge.RelationshipId ?? edge.Label, StringComparer.Ordinal)
                .ThenBy(edge => incoming ? edge.FromNodeId : edge.ToNodeId, StringComparer.OrdinalIgnoreCase).ToArray();
            for (var index = 0; index < ordered.Length; index++)
            {
                var y = node.Y + node.Height * (index + 1) / (ordered.Length + 1);
                result[ordered[index]] = new(incoming ? node.X : node.X + node.Width, y);
            }
        }
        return result;
    }

    private static (int LayoutOrder, double Y, double X) RelatedNodeOrder(MachineCompositionEdge edge,
        IReadOnlyDictionary<string, MachineCompositionNode> nodes, bool incoming)
    {
        var node = nodes[incoming ? edge.FromNodeId : edge.ToNodeId];
        return (node.LayoutOrder, node.Y, node.X);
    }

    private static MachineCompositionRoute Route(MachineCompositionEdge edge, IReadOnlyDictionary<string, MachineCompositionNode> nodes,
        MachineCompositionPoint start, MachineCompositionPoint end)
    {
        var from = nodes[edge.FromNodeId]; var to = nodes[edge.ToNodeId];
        var gutter = from.Kind == MachineCompositionNodeKind.Cabinet && to.Kind == MachineCompositionNodeKind.Face
            ? start.X + 10
            : edge.Kind == MachineCompositionEdgeKind.Provenance || from.Kind == MachineCompositionNodeKind.Machine && to.Kind == MachineCompositionNodeKind.Face
                ? to.X - 25
                : (start.X + end.X) / 2;
        var points = new[] { start, new MachineCompositionPoint(gutter, start.Y), new MachineCompositionPoint(gutter, end.Y), end };
        // Labels occupy the reserved horizontal lane immediately after the source port,
        // never the geometric midpoint where an unrelated card may be present.
        var labelX = from.Kind == MachineCompositionNodeKind.Cabinet && to.Kind == MachineCompositionNodeKind.Face
            ? end.X - 104
            : start.X + 8;
        var label = new MachineCompositionPoint(labelX, end.Y - 30);
        return new(edge, start, end, points, label, 104);
    }

    private string TryProjectPath(EditorProject project, string relative)
    { try { return _paths.ResolveProjectRelativePath(project, relative); } catch (ArgumentException) { return string.Empty; } }
    private static string PackageName(string path) => Path.GetFileName(Path.GetDirectoryName(path)) ?? Path.GetFileName(path);
    private static string ReelLabel(MachineObjectReference role) => $"Reel:{role.Id}";
    private static bool SamePath(string left, string right) { try { return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase); } catch { return false; } }
}

public sealed class MachineCompositionGraphViewModel : INotifyPropertyChanged
{
    private readonly DocumentTabViewModel _owner;
    private MachineCompositionGraph _graph = MachineCompositionGraph.Empty;
    private string? _selectedNodeId;
    public MachineCompositionGraphViewModel(DocumentTabViewModel owner) => _owner = owner;
    public event PropertyChangedEventHandler? PropertyChanged;
    public MachineCompositionGraph Graph { get => _graph; private set { _graph = value; PropertyChanged?.Invoke(this, new(nameof(Graph))); PropertyChanged?.Invoke(this, new(nameof(DiagnosticsSummary))); } }
    public string DiagnosticsSummary => Graph.Diagnostics.Count == 0 ? "Composition diagnostics: no issues" : $"Composition diagnostics: {Graph.Diagnostics.Count} issue{(Graph.Diagnostics.Count == 1 ? "" : "s")}";
    public string? SelectedNodeId { get => _selectedNodeId; set { if (_selectedNodeId == value) return; _selectedNodeId = value; PropertyChanged?.Invoke(this, new(nameof(SelectedNodeId))); } }
    internal void Refresh(EditorProject? project, string libraryRoot, IReadOnlyList<DocumentTabViewModel>? openDocuments)
    { Graph = project is null ? MachineCompositionGraph.Empty : new MachineCompositionGraphBuilder().Build(_owner.GetMachineDocument(), project, libraryRoot, openDocuments); }
    public void Open(MachineCompositionNode node) { if (!node.IsMissing && node.ManifestPath is not null) _owner.OpenMachineGraphAsset(node.ManifestPath); }
}
