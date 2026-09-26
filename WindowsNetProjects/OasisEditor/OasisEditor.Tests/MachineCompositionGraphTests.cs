using OasisEditor.Features.CabinetEditor.Models;
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
        Assert.Contains(graph.Edges, x => x.Label == "topGlass");
        Assert.Contains(graph.Edges, x => x.Label == "bottomGlass");
        Assert.Equal(4, graph.Edges.Count(x => x.Label.StartsWith("Reel:", StringComparison.Ordinal)));
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
    }

    [Fact]
    public void Build_IsDeterministicAndDoesNotMutateMachine()
    {
        using var fixture = new GraphFixture();
        var machine = MachineDocument.Create("Machine") with { SurfaceAssignments=[new("z", "Assets/Faces/Z/asset.face"), new("a", "Assets/Faces/A/asset.face")] };
        var json = MachineDocumentStorage.Serialize(machine);
        var first = fixture.Build(machine); var second = fixture.Build(machine);
        Assert.Equal(first.Nodes.Select(x => (x.Id,x.X,x.Y)), second.Nodes.Select(x => (x.Id,x.X,x.Y)));
        Assert.Equal(json, MachineDocumentStorage.Serialize(machine));
    }

    private sealed class GraphFixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "OasisGraph_" + Guid.NewGuid().ToString("N"));
        public EditorProject Project { get; }
        public string LibraryRoot { get; }
        public GraphFixture()
        {
            var assets=Path.Combine(_root,"Assets"); Directory.CreateDirectory(assets); LibraryRoot=Path.Combine(_root,"Library"); Directory.CreateDirectory(LibraryRoot);
            Project=new EditorProject { Name="Graph", ProjectDirectory=_root, ProjectFilePath=Path.Combine(_root,"Graph.oasisproj"), AssetsDirectory=assets, GeneratedDirectory=Path.Combine(_root,"Generated") };
        }
        public CompositionGraph Build(MachineDocument machine) => new MachineCompositionGraphBuilder().Build(machine,Project,LibraryRoot);
        public AssetReference WriteCabinet(string name, AssetReferenceScope scope)
        {
            var relative=$"Cabinets/{name}/asset.cabinet3d"; var path=Path.Combine(scope==AssetReferenceScope.Project?_root:LibraryRoot,relative.Replace('/',Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path,CabinetDocumentStorage.Serialize(CabinetDocument.FromModelPath("cabinet.glb")));
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
            var face=new FaceDocumentModel { Id=Guid.NewGuid().ToString("D"),Title=name,SourcePanel2DDocumentPath=panel,SourceRegion=new FaceSourceRegionModel { Width=100,Height=100 },
                Elements=reels.Select((r,i)=>(FaceElementModel)new FaceReelMount { ObjectId=$"r{r}",Name=$"Reel {r}",X=i*10,Y=0,Width=10,Height=20,LinkedMachineObjectReference=MachineObjectReference.Reel(r) }).ToArray() };
            File.WriteAllText(path,FaceDocumentStorage.Serialize(face)); return relative;
        }
        public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root,true); }
    }
}
