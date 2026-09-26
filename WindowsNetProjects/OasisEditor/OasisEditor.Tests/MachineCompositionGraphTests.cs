using OasisEditor.Features.CabinetEditor.Models;
using OasisEditor.Features.CabinetEditor.Services;
using Xunit;
using CompositionGraph = OasisEditor.MachineCompositionGraph;

namespace OasisEditor.Tests;

public sealed class MachineCompositionGraphTests
{
    [Fact]
    public void Build_DerivesBasicCompositionAndDeduplicatesSharedSourcesAndReels()
    {
        using var fixture = new GraphFixture();
        var panel = fixture.WritePanel("Import");
        var top = fixture.WriteFace("Top", panel, 3);
        var bottom = fixture.WriteFace("Bottom", panel, 0, 1, 2);
        var cabinet = fixture.WriteCabinet("Vogue", AssetReferenceScope.Project);
        var standard = fixture.WriteReel("Standard", AssetReferenceScope.Library, 290, 70);
        var small = fixture.WriteReel("Small", AssetReferenceScope.Library, 230, 70);
        var machine = MachineDocument.Create("Impact Game") with
        {
            CabinetAsset = cabinet,
            SurfaceAssignments = [new("topGlass", top), new("bottomGlass", bottom)],
            ReelAssignments = [new(MachineObjectReference.Reel(0), standard), new(MachineObjectReference.Reel(1), standard),
                new(MachineObjectReference.Reel(2), standard), new(MachineObjectReference.Reel(3), small)],
            Runtime = MachineEmulationRuntime.Create(FruitMachinePlatformType.MPU5),
            InputDefinitions = [new InputDefinitionModel { Id = "start" }]
        };

        var graph = fixture.Build(machine);

        Assert.Single(graph.Nodes.Where(x => x.Kind == MachineCompositionNodeKind.Machine));
        Assert.Single(graph.Nodes.Where(x => x.Kind == MachineCompositionNodeKind.Runtime));
        Assert.Single(graph.Nodes.Where(x => x.Kind == MachineCompositionNodeKind.Cabinet));
        Assert.Equal(2, graph.Nodes.Count(x => x.Kind == MachineCompositionNodeKind.Face));
        Assert.Single(graph.Nodes.Where(x => x.Kind == MachineCompositionNodeKind.Panel2D));
        Assert.Equal(2, graph.Nodes.Count(x => x.Kind == MachineCompositionNodeKind.Reel));
        Assert.Equal(2, graph.Edges.Count(x => x.Kind == MachineCompositionEdgeKind.Provenance));
        Assert.Contains(graph.Edges, x => x.RelationshipId == "topGlass" && x.Label == "Top Glass");
        Assert.Contains(graph.Edges, x => x.RelationshipId == "bottomGlass" && x.Label == "Bottom Glass");
        var reelEdges = graph.Edges.Where(x => x.LogicalReelRoleIds is { Length: > 0 }).ToArray();
        Assert.Equal(2, reelEdges.Length);
        Assert.Contains(reelEdges, x => x.Label == "Reels 0, 1, 2" && x.LogicalReelRoleIds!.SequenceEqual(["0", "1", "2"]));
        Assert.Contains(reelEdges, x => x.Label == "Reel 3" && x.LogicalReelRoleIds!.SequenceEqual(["3"]));
        Assert.Equal(["0", "1", "2", "3"], reelEdges.SelectMany(x => x.LogicalReelRoleIds!).OrderBy(x => x).ToArray());
        AssertNoNodeOverlap(graph);
        AssertRoutesAvoidUnrelatedNodes(graph);
        AssertNoCoincidentSegments(graph);
        AssertLabelsUseGutters(graph);
        var faceNodes = graph.Nodes.Where(x => x.Kind == MachineCompositionNodeKind.Face).ToDictionary(x => x.Title);
        Assert.True(faceNodes["Top"].Y < faceNodes["Bottom"].Y);
        var reelNodes = graph.Nodes.Where(x => x.Kind == MachineCompositionNodeKind.Reel).ToDictionary(x => x.Title);
        Assert.Equal(faceNodes["Top"].Y, reelNodes["Small"].Y);
        Assert.Equal(faceNodes["Bottom"].Y, reelNodes["Standard"].Y);
        var targetRoutes = graph.Routes.Where(x => x.Edge.RelationshipId is "topGlass" or "bottomGlass").ToArray();
        Assert.Equal(2, targetRoutes.Length);
        Assert.Equal(2, targetRoutes.Select(x => x.LabelPosition).Distinct().Count());
        Assert.Equal(2, targetRoutes.Select(x => x.Points[1].X).Distinct().Count());
        var provenanceRoutes = graph.Routes.Where(x => x.Edge.Kind == MachineCompositionEdgeKind.Provenance).ToArray();
        Assert.Equal(2, provenanceRoutes.Select(x => x.Points[1].X).Distinct().Count());
        foreach (var faceNode in faceNodes.Values)
        {
            var incoming = graph.Routes.Where(x => x.Edge.ToNodeId == faceNode.Id).OrderBy(x => x.DestinationPort.Y).ToArray();
            Assert.Equal(2, incoming.Length);
            Assert.NotEqual(incoming[0].DestinationPort.Y, incoming[1].DestinationPort.Y);
            Assert.Equal(MachineCompositionEdgeKind.Composition, incoming[0].Edge.Kind);
            Assert.Equal(MachineCompositionEdgeKind.Provenance, incoming[1].Edge.Kind);
        }
        Assert.Empty(graph.Diagnostics);
    }

    [Theory]
    [InlineData(AssetReferenceScope.Project)]
    [InlineData(AssetReferenceScope.Library)]
    public void Build_ReportsPhysicalAssetScope(AssetReferenceScope scope)
    {
        using var fixture = new GraphFixture();
        var cabinet = fixture.WriteCabinet("Cabinet", scope);
        var reel = fixture.WriteReel("Reel", scope, 200, 50);
        var face = fixture.WriteFace("Face", null, 0);
        var graph = fixture.Build(MachineDocument.Create("Machine") with { CabinetAsset=cabinet,
            SurfaceAssignments=[new("glass",face)], ReelAssignments=[new(MachineObjectReference.Reel(0),reel)] });
        Assert.Equal(scope, Assert.Single(graph.Nodes.Where(x => x.Kind == MachineCompositionNodeKind.Cabinet)).Scope);
        Assert.Equal(scope, Assert.Single(graph.Nodes.Where(x => x.Kind == MachineCompositionNodeKind.Reel)).Scope);
    }

    [Fact]
    public void Build_RetainsMissingAndInvalidReferencesWithoutScanningUnrelatedAssets()
    {
        using var fixture = new GraphFixture();
        Directory.CreateDirectory(Path.Combine(fixture.Project.AssetsDirectory, "Reels", "Unrelated"));
        File.WriteAllText(Path.Combine(fixture.Project.AssetsDirectory, "Reels", "Unrelated", "asset.reel"), "broken");
        var face = fixture.WriteFace("Broken dependencies", "Assets/Panel2D/Missing/asset.panel2d", 3);
        var machine = MachineDocument.Create("Machine") with
        {
            CabinetAsset=AssetReference.Library("Cabinets/Missing/asset.cabinet3d"),
            SurfaceAssignments=[new("topGlass",face)]
        };
        var graph = fixture.Build(machine);
        Assert.True(Assert.Single(graph.Nodes.Where(x => x.Kind == MachineCompositionNodeKind.Cabinet)).IsMissing);
        Assert.True(Assert.Single(graph.Nodes.Where(x => x.Kind == MachineCompositionNodeKind.Panel2D)).IsMissing);
        Assert.Single(graph.Nodes.Where(x => x.Kind == MachineCompositionNodeKind.MissingReelAssignment));
        Assert.Equal(3, graph.Diagnostics.Count);
        AssertNoNodeOverlap(graph);
    }

    [Fact]
    public void Build_IsDeterministicAndDoesNotMutateMachine()
    {
        using var fixture = new GraphFixture();
        var machine = MachineDocument.Create("Machine") with { SurfaceAssignments=[new("z", "Assets/Faces/Z/asset.face"), new("a", "Assets/Faces/A/asset.face")] };
        var json = MachineDocumentStorage.Serialize(machine);
        var first = fixture.Build(machine); var second = fixture.Build(machine);
        Assert.Equal(first.Nodes.Select(x => (x.Id,x.X,x.Y)), second.Nodes.Select(x => (x.Id,x.X,x.Y)));
        Assert.Equal(RouteSignature(first), RouteSignature(second));
        Assert.Equal(json, MachineDocumentStorage.Serialize(machine));
    }

    [Fact]
    public void Build_OrdersFacesByCabinetTargetsThenUnknownTargetsDeterministically()
    {
        using var fixture = new GraphFixture();
        var cabinet = fixture.WriteCabinet("Cabinet", AssetReferenceScope.Project);
        var bottom = fixture.WriteFace("A-Bottom", null);
        var top = fixture.WriteFace("Z-Top", null);
        var unknownZ = fixture.WriteFace("A-Unknown", null);
        var unknownA = fixture.WriteFace("Z-Unknown", null);
        var graph = fixture.Build(MachineDocument.Create("Machine") with
        {
            CabinetAsset=cabinet,
            SurfaceAssignments=[new("unknownZ",unknownZ),new("bottomGlass",bottom),new("topGlass",top),new("unknownA",unknownA)]
        });

        Assert.Equal(["Z-Top", "A-Bottom", "Z-Unknown", "A-Unknown"],
            graph.Nodes.Where(x => x.Kind == MachineCompositionNodeKind.Face).OrderBy(x => x.Y).Select(x => x.Title).ToArray());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Build_CabinetMetadataCountsDetectedTargetsNotSparseOverrides(int overrideCount)
    {
        using var fixture = new GraphFixture();
        var settings = new[] { CabinetSurfaceTargetSettings.Default("topGlass"), CabinetSurfaceTargetSettings.Default("bottomGlass") }.Take(overrideCount).ToArray();
        var cabinet = fixture.WriteCabinet("Cabinet", AssetReferenceScope.Project, settings);
        var graph = fixture.Build(MachineDocument.Create("Machine") with { CabinetAsset=cabinet });
        Assert.Equal("Project · 2 face targets", Assert.Single(graph.Nodes.Where(x => x.Kind == MachineCompositionNodeKind.Cabinet)).Metadata);
    }

    [Fact]
    public void Build_DistributesThreeIncomingAndMultipleOutgoingPortsDeterministically()
    {
        using var fixture = new GraphFixture();
        var cabinet = fixture.WriteCabinet("Cabinet", AssetReferenceScope.Project);
        var panel = fixture.WritePanel("Source");
        var face = fixture.WriteFace("Shared", panel, 0, 1);
        var standard = fixture.WriteReel("Standard", AssetReferenceScope.Project, 290, 70);
        var small = fixture.WriteReel("Small", AssetReferenceScope.Project, 230, 70);
        var machine = MachineDocument.Create("Machine") with
        {
            CabinetAsset=cabinet,
            SurfaceAssignments=[new("topGlass",face),new("bottomGlass",face)],
            ReelAssignments=[new(MachineObjectReference.Reel(0),standard),new(MachineObjectReference.Reel(1),small)]
        };

        var graph = fixture.Build(machine);
        var faceNode = Assert.Single(graph.Nodes.Where(x => x.Kind == MachineCompositionNodeKind.Face));
        var incoming = graph.Routes.Where(x => x.Edge.ToNodeId == faceNode.Id).OrderBy(x => x.DestinationPort.Y).ToArray();
        Assert.Equal(3, incoming.Length);
        Assert.Equal([faceNode.Y + faceNode.Height * .25, faceNode.Y + faceNode.Height * .5, faceNode.Y + faceNode.Height * .75], incoming.Select(x => x.DestinationPort.Y).ToArray());
        Assert.All(incoming, route => Assert.Equal(faceNode.X, route.DestinationPort.X));
        Assert.Equal(3, incoming.Select(route => route.Points[1].X).Distinct().Count());
        Assert.Equal(MachineCompositionEdgeKind.Composition, incoming[0].Edge.Kind);
        Assert.Equal(MachineCompositionEdgeKind.Composition, incoming[1].Edge.Kind);
        Assert.Equal(MachineCompositionEdgeKind.Provenance, incoming[2].Edge.Kind);

        var outgoing = graph.Routes.Where(x => x.Edge.FromNodeId == faceNode.Id).OrderBy(x => x.SourcePort.Y).ToArray();
        Assert.Equal(2, outgoing.Length);
        Assert.Equal([faceNode.Y + faceNode.Height / 3, faceNode.Y + faceNode.Height * 2 / 3], outgoing.Select(x => x.SourcePort.Y).ToArray());
        Assert.All(outgoing, route => Assert.Equal(faceNode.X + faceNode.Width, route.SourcePort.X));
        Assert.Equal(2, outgoing.Select(route => route.Points[1].X).Distinct().Count());
        Assert.Equal(RouteSignature(graph), RouteSignature(fixture.Build(machine)));
    }

    [Fact]
    public void Build_LongTargetLabelHasBoundedWrappedPresentationContract()
    {
        using var fixture = new GraphFixture([StubTargetDetector.Target("mainPanel", "Main Control Panel Relationship")]);
        var cabinet = fixture.WriteCabinet("Cabinet", AssetReferenceScope.Project);
        var face = fixture.WriteFace("Face", null);
        var graph = fixture.Build(MachineDocument.Create("Machine") with { CabinetAsset=cabinet, SurfaceAssignments=[new("mainPanel",face)] });
        var route = Assert.Single(graph.Routes.Where(x => x.Edge.RelationshipId == "mainPanel"));
        Assert.Equal("Main Control Panel Relationship", route.Edge.Label);
        Assert.InRange(route.LabelMaxWidth, 90, 120);
    }

    [Fact]
    public void Build_ExistingOpenFaceUsesCurrentInMemoryGraphState()
    {
        using var fixture = new GraphFixture();
        var cabinet = fixture.WriteCabinet("Cabinet", AssetReferenceScope.Project);
        var panel = fixture.WritePanel("LiveSource");
        var facePath = fixture.WriteFace("LiveFace", null);
        var openFace = fixture.OpenFace(facePath, "Unsaved Live Face", panel, 0);
        var reel = fixture.WriteReel("Live Reel", AssetReferenceScope.Project, 200, 50);
        var machine = MachineDocument.Create("Machine") with
        {
            CabinetAsset=cabinet, SurfaceAssignments=[new("topGlass",facePath)],
            ReelAssignments=[new(MachineObjectReference.Reel(0),reel)]
        };

        var graph = fixture.Build(machine, [openFace]);
        var face = Assert.Single(graph.Nodes.Where(x => x.Kind == MachineCompositionNodeKind.Face));
        Assert.False(face.IsMissing);
        Assert.Equal("Unsaved Live Face", face.Title);
        Assert.Single(graph.Nodes.Where(x => x.Kind == MachineCompositionNodeKind.Panel2D));
        Assert.Single(graph.Nodes.Where(x => x.Kind == MachineCompositionNodeKind.Reel));
    }

    [Fact]
    public void Build_MissingOpenFaceDoesNotResolveFromStaleTabAndRecoversWithoutMachineMutation()
    {
        using var fixture = new GraphFixture();
        var cabinet = fixture.WriteCabinet("Cabinet", AssetReferenceScope.Project);
        var panel = fixture.WritePanel("Source");
        var facePath = fixture.WriteFace("Face", panel, 0);
        var openFace = fixture.OpenFace(facePath, "Stale Face", panel, 0);
        var reel = fixture.WriteReel("Reel", AssetReferenceScope.Project, 200, 50);
        var machine = MachineDocument.Create("Machine") with
        {
            CabinetAsset=cabinet, SurfaceAssignments=[new("topGlass",facePath)],
            ReelAssignments=[new(MachineObjectReference.Reel(0),reel)]
        };
        var machineJson = MachineDocumentStorage.Serialize(machine);
        var machineTab = new DocumentTabViewModel(EditorDocument.CreateFromFile(fixture.Resolve("Assets/Machines/Game/asset.machine"), "Machine"), machineDocumentJson:machineJson);
        var manifestPath = fixture.Resolve(facePath);
        var savedFaceJson = File.ReadAllText(manifestPath);
        File.Delete(manifestPath);

        var missing = fixture.Build(machine, [openFace]);
        var missingFace = Assert.Single(missing.Nodes.Where(x => x.Kind == MachineCompositionNodeKind.Face));
        Assert.True(missingFace.IsMissing);
        Assert.Contains(missing.Edges, edge => edge.RelationshipId == "topGlass" && edge.ToNodeId == missingFace.Id);
        Assert.Contains(missing.Diagnostics, diagnostic => diagnostic.NodeId == missingFace.Id && diagnostic.Message.Contains("missing", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(missing.Edges, edge => edge.Kind == MachineCompositionEdgeKind.Provenance);
        Assert.DoesNotContain(missing.Edges, edge => edge.LogicalReelRoleIds is { Length: > 0 });

        File.WriteAllText(manifestPath, savedFaceJson);
        var recovered = fixture.Build(machine, [openFace]);
        Assert.False(Assert.Single(recovered.Nodes.Where(x => x.Kind == MachineCompositionNodeKind.Face)).IsMissing);
        Assert.Contains(recovered.Edges, edge => edge.Kind == MachineCompositionEdgeKind.Provenance);
        Assert.Contains(recovered.Edges, edge => edge.LogicalReelRoleIds is { Length: > 0 });
        Assert.DoesNotContain(recovered.Diagnostics, diagnostic => diagnostic.Message.Contains("Face", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(machineJson, MachineDocumentStorage.Serialize(machine));
        Assert.False(machineTab.IsDirty);
        Assert.False(machineTab.CommandService.CanUndo);
    }

    private static void AssertNoNodeOverlap(CompositionGraph graph)
    {
        for (var i = 0; i < graph.Nodes.Count; i++)
        for (var j = i + 1; j < graph.Nodes.Count; j++)
            Assert.False(Intersects(graph.Nodes[i], graph.Nodes[j]), $"{graph.Nodes[i].Id} overlaps {graph.Nodes[j].Id}");
    }

    private static string[] RouteSignature(CompositionGraph graph) => graph.Routes.Select(route =>
        $"{route.Edge.FromNodeId}|{route.Edge.ToNodeId}|{route.Edge.Label}|{route.Edge.RelationshipId}|{string.Join(',', route.Edge.LogicalReelRoleIds ?? [])}|"
        + $"{route.SourcePort.X},{route.SourcePort.Y}|{route.DestinationPort.X},{route.DestinationPort.Y}|"
        + string.Join(';', route.Points.Select(point => $"{point.X},{point.Y}")) + $"|{route.LabelPosition.X},{route.LabelPosition.Y}|{route.LabelMaxWidth}|{route.LaneOffset}").ToArray();

    private static void AssertRoutesAvoidUnrelatedNodes(CompositionGraph graph)
    {
        foreach (var route in graph.Routes)
        foreach (var node in graph.Nodes.Where(x => x.Id != route.Edge.FromNodeId && x.Id != route.Edge.ToNodeId))
        for (var index = 1; index < route.Points.Count; index++)
            Assert.False(SegmentIntersectsInterior(route.Points[index - 1], route.Points[index], node),
                $"Route {route.Edge.FromNodeId} -> {route.Edge.ToNodeId} crosses {node.Id}");
    }

    private static void AssertNoCoincidentSegments(CompositionGraph graph)
    {
        for (var routeIndex = 0; routeIndex < graph.Routes.Count; routeIndex++)
        for (var otherIndex = routeIndex + 1; otherIndex < graph.Routes.Count; otherIndex++)
        for (var segment = 1; segment < graph.Routes[routeIndex].Points.Count; segment++)
        for (var otherSegment = 1; otherSegment < graph.Routes[otherIndex].Points.Count; otherSegment++)
            Assert.False(SharesNonTrivialSegment(
                graph.Routes[routeIndex].Points[segment - 1], graph.Routes[routeIndex].Points[segment],
                graph.Routes[otherIndex].Points[otherSegment - 1], graph.Routes[otherIndex].Points[otherSegment]),
                $"Routes {graph.Routes[routeIndex].Edge.Label} and {graph.Routes[otherIndex].Edge.Label} share a segment.");
    }

    private static bool SharesNonTrivialSegment(MachineCompositionPoint a, MachineCompositionPoint b,
        MachineCompositionPoint c, MachineCompositionPoint d)
    {
        const double epsilon = .001;
        if (Math.Abs(a.X-b.X) < epsilon && Math.Abs(c.X-d.X) < epsilon && Math.Abs(a.X-c.X) < epsilon)
            return Math.Min(Math.Max(a.Y,b.Y),Math.Max(c.Y,d.Y))-Math.Max(Math.Min(a.Y,b.Y),Math.Min(c.Y,d.Y)) > epsilon;
        if (Math.Abs(a.Y-b.Y) < epsilon && Math.Abs(c.Y-d.Y) < epsilon && Math.Abs(a.Y-c.Y) < epsilon)
            return Math.Min(Math.Max(a.X,b.X),Math.Max(c.X,d.X))-Math.Max(Math.Min(a.X,b.X),Math.Min(c.X,d.X)) > epsilon;
        return false;
    }

    private static void AssertLabelsUseGutters(CompositionGraph graph)
    {
        foreach (var route in graph.Routes.Where(x => !string.IsNullOrWhiteSpace(x.Edge.Label)))
            Assert.DoesNotContain(graph.Nodes, node => PointInside(route.LabelPosition, node));
    }

    private static bool PointInside(MachineCompositionPoint point, MachineCompositionNode node) =>
        point.X > node.X && point.X < node.X + node.Width && point.Y > node.Y && point.Y < node.Y + node.Height;

    private static bool Intersects(MachineCompositionNode left, MachineCompositionNode right) =>
        left.X < right.X + right.Width && left.X + left.Width > right.X
        && left.Y < right.Y + right.Height && left.Y + left.Height > right.Y;

    private static bool SegmentIntersectsInterior(MachineCompositionPoint a, MachineCompositionPoint b, MachineCompositionNode node)
    {
        const double epsilon = .001;
        var left=node.X+epsilon; var right=node.X+node.Width-epsilon; var top=node.Y+epsilon; var bottom=node.Y+node.Height-epsilon;
        if (Math.Abs(a.X-b.X) < epsilon) return a.X > left && a.X < right && Math.Max(a.Y,b.Y) > top && Math.Min(a.Y,b.Y) < bottom;
        return a.Y > top && a.Y < bottom && Math.Max(a.X,b.X) > left && Math.Min(a.X,b.X) < right;
    }

    private sealed class GraphFixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "OasisGraph_" + Guid.NewGuid().ToString("N"));
        public EditorProject Project { get; }
        public string LibraryRoot { get; }
        private readonly StubTargetDetector _targetDetector;
        public GraphFixture(IReadOnlyList<CabinetFaceTarget>? targets = null)
        {
            _targetDetector = new(targets);
            var assets=Path.Combine(_root,"Assets"); Directory.CreateDirectory(assets); LibraryRoot=Path.Combine(_root,"Library"); Directory.CreateDirectory(LibraryRoot);
            Project=new EditorProject { Name="Graph", ProjectDirectory=_root, ProjectFilePath=Path.Combine(_root,"Graph.oasisproj"), AssetsDirectory=assets, GeneratedDirectory=Path.Combine(_root,"Generated") };
        }
        public CompositionGraph Build(MachineDocument machine, IReadOnlyList<DocumentTabViewModel>? openDocuments = null) =>
            new MachineCompositionGraphBuilder(_targetDetector).Build(machine,Project,LibraryRoot,openDocuments);
        public AssetReference WriteCabinet(string name, AssetReferenceScope scope, CabinetSurfaceTargetSettings[]? settings = null)
        {
            var relative=$"Cabinets/{name}/asset.cabinet3d"; var path=Path.Combine(scope==AssetReferenceScope.Project?_root:LibraryRoot,relative.Replace('/',Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(path)!, "cabinet.glb"), "test model");
            File.WriteAllText(path,CabinetDocumentStorage.Serialize(CabinetDocument.FromModelPath("cabinet.glb") with { SurfaceTargetSettings=settings ?? [] }));
            return new(scope,relative);
        }
        public AssetReference WriteReel(string name, AssetReferenceScope scope, double diameter, double width)
        {
            var relative=$"Reels/{name}/asset.reel"; var path=Path.Combine(scope==AssetReferenceScope.Project?_root:LibraryRoot,relative.Replace('/',Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path,ReelDocumentStorage.Serialize(ReelDocument.Create(name) with { DiameterMm=diameter,WidthMm=width }));
            return new(scope,relative);
        }
        public string WritePanel(string name)
        {
            var relative=$"Assets/Panel2D/{name}/asset.panel2d"; var path=Path.Combine(_root,relative.Replace('/',Path.DirectorySeparatorChar)); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path,Panel2DDocumentStorage.Serialize(name,"",[])); return relative;
        }
        public string WriteFace(string name, string? panel, params int[] reels)
        {
            var relative=$"Assets/Faces/{name}/asset.face"; var path=Path.Combine(_root,relative.Replace('/',Path.DirectorySeparatorChar)); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var face=CreateFace(name,panel,reels);
            File.WriteAllText(path,FaceDocumentStorage.Serialize(face)); return relative;
        }
        public DocumentTabViewModel OpenFace(string relative, string title, string? panel, params int[] reels)
        {
            var path=Resolve(relative);
            return new DocumentTabViewModel(EditorDocument.CreateFromFile(path,title),faceDocumentJson:FaceDocumentStorage.Serialize(CreateFace(title,panel,reels)));
        }
        public string Resolve(string relative) => Path.Combine(_root,relative.Replace('/',Path.DirectorySeparatorChar));
        private static FaceDocumentModel CreateFace(string name, string? panel, params int[] reels) => new() { Id=Guid.NewGuid().ToString("D"),Title=name,SourcePanel2DDocumentPath=panel,SourceRegion=new FaceSourceRegionModel { Width=100,Height=100 },
            Elements=reels.Select((r,i)=>(FaceElementModel)new FaceReelMount { ObjectId=$"r{r}",Name=$"Reel {r}",X=i*10,Y=0,Width=10,Height=20,LinkedMachineObjectReference=MachineObjectReference.Reel(r) }).ToArray() };
        public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root,true); }
    }

    private sealed class StubTargetDetector : ICabinetFaceTargetDetector
    {
        private readonly IReadOnlyList<CabinetFaceTarget> _targets;
        public StubTargetDetector(IReadOnlyList<CabinetFaceTarget>? targets = null) => _targets = targets ?? [Target("topGlass", "Top Glass"), Target("bottomGlass", "Bottom Glass")];
        public IReadOnlyList<CabinetFaceTarget> DetectTargets(string modelPath, CancellationToken cancellationToken = default) => _targets;
        public static CabinetFaceTarget Target(string id, string name) => new(id, "OasisFace_" + id, name, [], default, default, true, null);
    }
}
