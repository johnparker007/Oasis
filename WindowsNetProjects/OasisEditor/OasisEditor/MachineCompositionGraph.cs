using System.ComponentModel;
using System.IO;
using OasisEditor.Features.CabinetEditor.Models;

namespace OasisEditor;

public enum MachineCompositionNodeKind { Machine, Runtime, Cabinet, Face, Panel2D, Reel, MissingReelAssignment }
public enum MachineCompositionEdgeKind { Composition, Provenance }
public enum MachineCompositionDiagnosticSeverity { Warning, Error }

public sealed record MachineCompositionNode(
    string Id, MachineCompositionNodeKind Kind, string Title, string Metadata,
    AssetReferenceScope? Scope = null, string? ManifestPath = null, bool IsMissing = false,
    double X = 0, double Y = 0, double Width = 190, double Height = 92);

public sealed record MachineCompositionEdge(string FromNodeId, string ToNodeId, string Label, MachineCompositionEdgeKind Kind);
public sealed record MachineCompositionDiagnostic(MachineCompositionDiagnosticSeverity Severity, string Message, string? NodeId = null);
public sealed record MachineCompositionGraph(IReadOnlyList<MachineCompositionNode> Nodes, IReadOnlyList<MachineCompositionEdge> Edges, IReadOnlyList<MachineCompositionDiagnostic> Diagnostics)
{
    public static MachineCompositionGraph Empty { get; } = new([], [], []);
}

/// <summary>Builds a transient explanation of a Machine by following only its explicit references.</summary>
public sealed class MachineCompositionGraphBuilder
{
    private readonly AssetReferenceResolver _resolver = new();
    private readonly ProjectAssetPathService _paths = new();

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
        edges.Add(new(machineId, runtimeId, "runtime", MachineCompositionEdgeKind.Composition));

        string? cabinetId = null;
        CabinetDocument? cabinet = null;
        if (machine.CabinetAsset is null)
            diagnostics.Add(new(MachineCompositionDiagnosticSeverity.Warning, "No Cabinet is assigned to this Machine.", machineId));
        else
        {
            cabinetId = "cabinet:" + machine.CabinetAsset;
            var result = ReadReference(project, libraryRoot, machine.CabinetAsset, json => CabinetDocumentStorage.TryRead(json, out cabinet));
            var title = result.Exists && cabinet is not null ? PackageName(result.Path) : "Missing Cabinet";
            nodes[cabinetId] = new(cabinetId, MachineCompositionNodeKind.Cabinet, title,
                result.Exists && cabinet is not null ? $"{machine.CabinetAsset.Scope} · {cabinet.SurfaceTargetSettings.Length} configured targets" : $"{machine.CabinetAsset.Path} · {machine.CabinetAsset.Scope}",
                machine.CabinetAsset.Scope, result.Exists ? result.Path : null, !result.Valid);
            edges.Add(new(machineId, cabinetId, "cabinet", MachineCompositionEdgeKind.Composition));
            if (!result.Valid) diagnostics.Add(new(MachineCompositionDiagnosticSeverity.Error,
                result.Exists ? $"Cabinet reference is invalid: {machine.CabinetAsset.Path}" : $"Cabinet asset is missing: {machine.CabinetAsset.Path}", cabinetId));
        }

        foreach (var assignment in machine.SurfaceAssignments.OrderBy(x => x.TargetId, StringComparer.Ordinal))
        {
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
                    valid ? "Project" : $"{normalized} · Project", AssetReferenceScope.Project, valid ? facePath : null, !valid);
            edges.Add(new(cabinetId ?? machineId, faceId, assignment.TargetId, MachineCompositionEdgeKind.Composition));
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
                edges.Add(new(faceId, missingId, ReelLabel(role), MachineCompositionEdgeKind.Composition));
                diagnostics.Add(new(MachineCompositionDiagnosticSeverity.Error, $"{face.Title} requires {role}, but the Machine has no Reel assignment.", missingId));
                continue;
            }
            var id = "reel:" + assignment.ReelAsset;
            ReelDocument? reel = null;
            var result = ReadReference(project, libraryRoot, assignment.ReelAsset, json => ReelDocumentStorage.TryRead(json, out reel, out _));
            if (!nodes.ContainsKey(id)) nodes[id] = new(id, MachineCompositionNodeKind.Reel, result.Valid ? reel!.DisplayName : "Missing Reel",
                result.Valid ? $"{reel!.DiameterMm:0.#} × {reel.WidthMm:0.#} mm · {assignment.ReelAsset.Scope}" : $"{assignment.ReelAsset.Path} · {assignment.ReelAsset.Scope}",
                assignment.ReelAsset.Scope, result.Valid ? result.Path : null, !result.Valid);
            edges.Add(new(faceId, id, ReelLabel(role), MachineCompositionEdgeKind.Composition));
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
        var order = new[] { MachineCompositionNodeKind.Panel2D, MachineCompositionNodeKind.Face, MachineCompositionNodeKind.Machine,
            MachineCompositionNodeKind.Cabinet, MachineCompositionNodeKind.Runtime, MachineCompositionNodeKind.Reel, MachineCompositionNodeKind.MissingReelAssignment };
        var columns = new Dictionary<MachineCompositionNodeKind, int>
        { [MachineCompositionNodeKind.Panel2D]=0, [MachineCompositionNodeKind.Face]=1, [MachineCompositionNodeKind.Machine]=2,
          [MachineCompositionNodeKind.Cabinet]=3, [MachineCompositionNodeKind.Runtime]=3, [MachineCompositionNodeKind.Reel]=4,
          [MachineCompositionNodeKind.MissingReelAssignment]=4 };
        var laidOut = new List<MachineCompositionNode>();
        foreach (var kind in order)
        {
            var group = source.Where(x => x.Kind == kind).OrderBy(x => x.Id, StringComparer.OrdinalIgnoreCase).ToArray();
            for (var i = 0; i < group.Length; i++) laidOut.Add(group[i] with { X = 30 + columns[kind] * 250, Y = 35 + i * 125 });
        }
        return new(laidOut, edges.OrderBy(x => x.FromNodeId).ThenBy(x => x.ToNodeId).ThenBy(x => x.Label).ToArray(), diagnostics.ToArray());
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
