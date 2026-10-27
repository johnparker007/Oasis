using OasisEditor.Features.CabinetEditor.Models;

namespace OasisEditor.Avalonia;

public sealed record MachineOverviewNode(string Id, string Kind, string Title, string Metadata,
    string? ManifestPath, bool Missing, double X, double Y, double Width = 220, double Height = 88);
public sealed record MachineOverviewEdge(string FromId, string ToId, string Label = "", bool Provenance = false);
public sealed record MachineOverview(MachineOverviewNode[] Nodes, MachineOverviewEdge[] Edges, string[] Diagnostics);

/// <summary>Builds a read-only graph from the Machine's explicit authored dependencies.</summary>
public sealed class MachineCompositionOverviewBuilder
{
    private readonly AssetReferenceResolver _references = new();

    public MachineOverview Build(MachineDocument machine, EditorProject project, string libraryRoot,
        string? activeFacePath = null, FaceDocumentModel? activeFace = null)
    {
        ArgumentNullException.ThrowIfNull(machine);
        ArgumentNullException.ThrowIfNull(project);
        var nodes = new List<MachineOverviewNode>();
        var edges = new List<MachineOverviewEdge>();
        var diagnostics = new List<string>();
        const double step = 126;
        nodes.Add(new($"machine:{machine.Id}", "Machine", machine.DisplayName,
            $"{machine.Runtime.Kind} · {machine.InputDefinitions.Count} inputs", null, false, 28, 255));
        nodes.Add(new("runtime", "Runtime", machine.Runtime switch
            { EmulationRuntimeDefinition e => $"Emulation · {e.Platform}", OasisRuntimeDefinition => "Oasis Script", _ => machine.Runtime.Kind },
            "Machine runtime", null, false, 278, 28));
        edges.Add(new($"machine:{machine.Id}", "runtime"));

        var cabinetId = "cabinet";
        if (machine.CabinetAsset is { } cabinetRef)
        {
            var (path, valid, title, detail) = ResolveCabinet(cabinetRef);
            nodes.Add(new(cabinetId, "Cabinet", title, detail, valid ? path : null, !valid, 278, 220));
            edges.Add(new($"machine:{machine.Id}", cabinetId));
            if (!valid) diagnostics.Add($"Cabinet is missing or invalid: {cabinetRef.Path} ({cabinetRef.Scope}).");
        }
        else
        {
            nodes.Add(new(cabinetId, "Cabinet", "No Cabinet assigned", "Machine has no Cabinet reference", null, true, 278, 220));
            diagnostics.Add("Machine has no Cabinet assigned.");
        }

        var faceNodes = new Dictionary<string, MachineOverviewNode>(StringComparer.OrdinalIgnoreCase);
        var faceIndex = 0;
        foreach (var assignment in machine.SurfaceAssignments)
        {
            var relative = assignment.FaceAssetPath;
            var id = "face:" + relative.Replace('\\', '/');
            if (!faceNodes.TryGetValue(id, out var faceNode))
            {
                var (path, valid, title, detail, face) = ResolveFace(relative, activeFacePath, activeFace);
                faceNode = new(id, "Face", title, detail, valid ? path : null, !valid, 552, 72 + faceIndex++ * step);
                faceNodes.Add(id, faceNode); nodes.Add(faceNode);
                if (!valid) diagnostics.Add($"Face assigned to '{assignment.TargetId}' is missing or invalid: {relative}.");
                if (valid && face is not null) AddFaceProvenance(face, id, project.ProjectDirectory, nodes, edges, diagnostics);
                if (valid && face is not null) AddFaceReels(face, id, machine, project, libraryRoot, nodes, edges, diagnostics);
            }
            edges.Add(new(cabinetId, id, assignment.TargetId));
        }

        var nextX = 278.0;
        var prefabIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var prefab in machine.PrefabDependencies)
            AddPrefab(prefab.PrefabAsset, prefab.Alias, prefab.Alias);
        foreach (var instance in machine.ObjectInstances)
            if (instance.ObjectAsset is { } asset) AddPrefab(asset, asset.Path, instance.DisplayName);

        foreach (var anchor in machine.Anchors)
        {
            var id = "anchor:" + anchor.Id;
            nodes.Add(new(id, "Anchor", anchor.DisplayName, $"{anchor.Position.X:0.##}, {anchor.Position.Y:0.##}, {anchor.Position.Z:0.##}", null, false, nextX, 440 + nodes.Count(x => x.Kind == "Anchor") * 100));
            edges.Add(new($"machine:{machine.Id}", id, "anchor"));
        }

        // Pack each dependency column so Cabinets, Prefabs and anchors never share a card row.
        var arranged = nodes.GroupBy(n=>n.X).SelectMany(column=>column.OrderBy(n=>n.Y)
            .Select((node,index)=>node.Kind=="Machine"?node:node with {Y=28+index*126})).ToArray();
        return new(arranged, edges.ToArray(), diagnostics.ToArray());

        (string Path, bool Valid, string Title, string Detail) ResolveCabinet(AssetReference reference)
        {
            try
            {
                var path = _references.Resolve(project, libraryRoot, reference);
                CabinetDocument? cabinet = null;
                var valid = File.Exists(path) && CabinetDocumentStorage.TryRead(File.ReadAllText(path), out cabinet);
                return valid
                    ? (path, true, Path.GetFileName(Path.GetDirectoryName(path)) ?? "Cabinet", $"{reference.Scope} · model scale {cabinet!.Model.Scale:0.###}")
                    : (path, false, "Missing Cabinet", $"{reference.Scope} · {reference.Path}");
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
            { return (string.Empty, false, "Missing Cabinet", $"{reference.Scope} · {reference.Path}: {error.Message}"); }
        }

        (string Path, bool Valid, string Title, string Detail, FaceDocumentModel? Face) ResolveFace(string relative,
            string? activePath, FaceDocumentModel? active)
        {
            try
            {
                var path = WorkspacePathPolicy.ResolveConfined(project.ProjectDirectory, relative, requireExists: true);
                WorkspacePathPolicy.RejectReparsePoints(project.ProjectDirectory, relative, requireExists: true);
                FaceDocumentModel? face = null;
                var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
                if (active is not null && activePath is not null && string.Equals(Path.GetFullPath(activePath), path, comparison)) face = active;
                else if (FaceDocumentStorage.TryReadValidated(File.ReadAllText(path), out var file, out _)) face = FaceDocumentStorage.ToModel(file);
                return face is null ? (path, false, "Invalid Face", relative, null)
                    : (path, true, face.Title, "Project Face", face);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
            { return (string.Empty, false, "Missing Face", error.Message, null); }
        }

        void AddFaceProvenance(FaceDocumentModel face, string faceId, string projectRoot, List<MachineOverviewNode> graphNodes,
            List<MachineOverviewEdge> graphEdges, List<string> graphDiagnostics)
        {
            var sourcePath = face.SourcePanel2DDocumentPath ?? face.Artwork?.Source.Panel2DDocumentPath;
            if (string.IsNullOrWhiteSpace(sourcePath)) return;
            var id = "panel:" + sourcePath.Replace('\\', '/');
            var valid = false;
            string? fullPath = null;
            try
            {
                fullPath = WorkspacePathPolicy.ResolveConfined(projectRoot, sourcePath, requireExists: true);
                WorkspacePathPolicy.RejectReparsePoints(projectRoot, sourcePath, requireExists: true);
                valid = Panel2DDocumentStorage.TryReadValidated(File.ReadAllText(fullPath), out _, out _);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
            { graphDiagnostics.Add($"Panel2D provenance is unavailable: {sourcePath} ({error.Message})."); }
            if (!graphNodes.Any(n => n.Id == id)) graphNodes.Add(new(id, "Panel2D", valid ? Path.GetFileName(Path.GetDirectoryName(fullPath!)) ?? "Source Panel2D" : "Missing Panel2D",
                "Face source · Project", valid ? fullPath : null, !valid, 278, 510 + graphNodes.Count(n => n.Kind == "Panel2D") * 100));
            graphEdges.Add(new(id, faceId, "source", Provenance: true));
            if (!valid && !graphDiagnostics.Any(m => m.Contains(sourcePath, StringComparison.Ordinal))) graphDiagnostics.Add($"Panel2D provenance is unresolved: {sourcePath}.");
        }

        void AddFaceReels(FaceDocumentModel face, string faceId, MachineDocument sourceMachine, EditorProject sourceProject,
            string sourceLibraryRoot, List<MachineOverviewNode> graphNodes, List<MachineOverviewEdge> graphEdges, List<string> graphDiagnostics)
        {
            var references = face.Elements.OfType<FaceReelMount>().Select(element => element.LinkedMachineObjectReference)
                .Where(reference => reference is { Kind: MachineObjectKind.Reel }).Select(reference => reference!.Value).Distinct().ToArray();
            foreach (var logical in references)
            {
                var assignment = sourceMachine.ReelAssignments.FirstOrDefault(value => value.MachineReelReference == logical);
                if (assignment is null)
                {
                    var id = "missing-reel:" + logical;
                    if (!graphNodes.Any(n => n.Id == id)) graphNodes.Add(new(id, "Reel", "Unassigned Reel", $"Logical reference {logical}", null, true, 838, 72 + graphNodes.Count(n => n.Kind == "Reel") * step));
                    graphEdges.Add(new(faceId, id, logical.ToString())); graphDiagnostics.Add($"Face '{face.Title}' uses {logical}, but the Machine has no Reel assignment.");
                    continue;
                }
                var idForReel = "reel:" + assignment.ReelAsset.Scope + ":" + assignment.ReelAsset.Path;
                if (!graphNodes.Any(n => n.Id == idForReel))
                {
                    var (path, valid, title, detail) = ResolveReel(assignment.ReelAsset, sourceProject, sourceLibraryRoot);
                    graphNodes.Add(new(idForReel, "Reel", title, detail, valid ? path : null, !valid, 838, 72 + graphNodes.Count(n => n.Kind == "Reel") * step));
                    if (!valid) graphDiagnostics.Add($"Reel asset is missing or invalid: {assignment.ReelAsset.Path} ({assignment.ReelAsset.Scope}).");
                }
                graphEdges.Add(new(faceId, idForReel, logical.ToString()));
            }
        }

        (string Path, bool Valid, string Title, string Detail) ResolveReel(AssetReference reference, EditorProject sourceProject, string sourceLibraryRoot)
        {
            try
            {
                var path = _references.Resolve(sourceProject, sourceLibraryRoot, reference);
                ReelDocument? reel = null;
                var valid = File.Exists(path) && ReelDocumentStorage.TryRead(File.ReadAllText(path), out reel, out _);
                return valid ? (path, true, reel!.DisplayName, $"{reference.Scope} · {reel.DiameterMm:0.#} × {reel.WidthMm:0.#} mm")
                    : (path, false, "Missing Reel", $"{reference.Scope} · {reference.Path}");
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
            { return (string.Empty, false, "Missing Reel", error.Message); }
        }

        void AddPrefab(AssetReference reference, string key, string label)
        {
            if (!prefabIds.TryGetValue(reference.Scope + ":" + reference.Path, out var id))
            {
                id = "prefab:" + reference.Scope + ":" + reference.Path;
                prefabIds.Add(reference.Scope + ":" + reference.Path, id);
                string path = string.Empty; var valid = false; var title = "Missing Prefab"; var detail = $"{reference.Scope} · {reference.Path}";
                try
                {
                    path = _references.Resolve(project, libraryRoot, reference);
                    if (File.Exists(path) && Object3DDocumentStorage.TryRead(File.ReadAllText(path), out var prefab, out _))
                    { valid = true; title = prefab.DisplayName; detail = $"{reference.Scope} · {prefab.Root.Children.Length} root children"; }
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
                { detail += " · " + error.Message; }
                nodes.Add(new(id, "Prefab", title, detail, valid ? path : null, !valid, nextX, 72 + prefabIds.Count * 100));
                if (!valid) diagnostics.Add($"Prefab is missing or invalid: {reference.Path} ({reference.Scope}).");
                nextX = 552;
            }
            edges.Add(new($"machine:{machine.Id}", id, label));
        }
    }
}
