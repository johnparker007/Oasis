using System.Windows.Media.Media3D;
using System.Windows.Media;
using OasisEditor.Features.CabinetEditor.Models;
using OasisEditor.Features.CabinetEditor.Services;
using OasisEditor.Features.MachineComposition.ViewModels;
using Xunit;

namespace OasisEditor.Tests;

public sealed class MachineComposition3DTests
{
    [Fact]
    public void CompositionLoaderIsLazyUntilViewIsRequested()
    {
        using var tab = new DocumentTabViewModel(EditorDocument.CreateFromFile("Pool/asset.machine", "Machine", "Pool"));
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        tab.SetProjectAccessor(() => new EditorProject
        {
            Name = "Pool",
            ProjectDirectory = root,
            ProjectFilePath = Path.Combine(root, "Pool.oasisproj"),
            AssetsDirectory = Path.Combine(root, "Assets"),
            GeneratedDirectory = Path.Combine(root, "Generated")
        });

        Assert.Null(tab.ExistingMachineComposition3D);
    }

    [Fact]
    public void ProjectionDerivesEveryMachineInstanceAndSelectsByInstanceId()
    {
        var machine = MachineDocument.Create("Pool") with { ObjectInstances =
        [
            new("cueBall", "Cue Ball", AssetReference.Project("Assets/Object3D/Cue/asset.object3d"), MachineObjectTransform.Identity),
            new("ball08", "Ball 8", AssetReference.Library("Object3D/Ball8/asset.object3d"), MachineObjectTransform.Identity)
        ]};
        using var tab = new DocumentTabViewModel(
            EditorDocument.CreateFromFile("Pool/asset.machine", "Machine", "Pool"),
            machineDocumentJson: MachineDocumentStorage.Serialize(machine));

        var composition = Assert.IsType<MachineComposition3DViewModel>(tab.MachineComposition3D);
        composition.Refresh(machine, cabinetChanged: false, objectInstancesChanged: true);
        composition.SelectedObject = Assert.Single(composition.Objects, row => row.Id == "ball08");

        Assert.Equal(["cueBall", "ball08"], composition.Objects.Select(row => row.Id));
        Assert.Equal("ball08", composition.SelectedObject.Id);
    }

    [Fact]
    public void PreviewFiltersAreTransientAndDoNotDirtyMachine()
    {
        using var tab = new DocumentTabViewModel(EditorDocument.CreateFromFile("Pool/asset.machine", "Machine", "Pool"));
        var composition = Assert.IsType<MachineComposition3DViewModel>(tab.MachineComposition3D);

        composition.ShowCabinetVisual = false;
        composition.ShowObjects = false;
        composition.ShowCabinetCollision = true;
        composition.ShowColliders = false;
        composition.ShowTriggers = false;

        Assert.False(tab.IsDirty);
    }

    [Fact]
    public void AppliesMachinePositionAndInstanceAndIntrinsicScale()
    {
        var transform = MachineCompositionTransform.Create(
            new MachineObjectTransform(new(10, 20, 30), MachineVector3.Zero, new(2, 3, 4)),
            new Object3DModelDefinition("ball.glb", .5, "Y"));

        var point = transform.Transform(new Point3D(2, 2, 2));

        Assert.Equal(new Point3D(12, 23, 34), point);
    }

    [Theory]
    [InlineData("Y", 0, 1, 0)]
    [InlineData("Z", 0, 0, -1)]
    [InlineData("X", 0, 1, 0)]
    public void IntrinsicUpAxisUsesPlayerConvention(string upAxis, double expectedX, double expectedY, double expectedZ)
    {
        var source = upAxis == "X" ? new Point3D(1, 0, 0) : new Point3D(0, 1, 0);
        var transform = MachineCompositionTransform.Create(MachineObjectTransform.Identity, new Object3DModelDefinition("object.glb", 1, upAxis));

        var point = transform.Transform(source);

        Assert.Equal(expectedX, point.X, 8);
        Assert.Equal(expectedY, point.Y, 8);
        Assert.Equal(expectedZ, point.Z, 8);
    }

    [Fact]
    public void PlacementRotationIsAppliedOutsideIntrinsicCorrection()
    {
        var transform = MachineCompositionTransform.Create(
            new MachineObjectTransform(MachineVector3.Zero, new(0, 90, 0), MachineVector3.One),
            new Object3DModelDefinition("object.glb", 1, "Z"));

        var point = transform.Transform(new Point3D(0, 1, 0));

        Assert.Equal(-1, point.X, 8);
        Assert.Equal(0, point.Y, 8);
        Assert.Equal(0, point.Z, 8);
    }

    [Fact]
    public async Task ResolvesProjectAndLibraryAssetsAndReusesRepeatedDefinition()
    {
        using var environment = new CompositionEnvironment();
        var projectAsset = environment.WriteObject(AssetReferenceScope.Project, "ProjectBall", 1);
        var libraryAsset = environment.WriteObject(AssetReferenceScope.Library, "LibraryBall", 2);
        var machine = MachineDocument.Create("Pool") with { ObjectInstances =
        [
            Instance("one", projectAsset), Instance("two", projectAsset), Instance("three", libraryAsset)
        ]};
        using var tab = environment.CreateTab(machine);
        var composition = tab.MachineComposition3D!;

        await composition.WaitForRefreshAsync();

        Assert.Equal(3, composition.ObjectVisuals!.Children.Count);
        Assert.Equal(1, environment.Loader.CountFor("ProjectBall.glb"));
        Assert.Equal(1, environment.Loader.CountFor("LibraryBall.glb"));
        Assert.Empty(composition.Diagnostics.Where(message => message.Contains("Object3D", StringComparison.Ordinal)));

        composition.Objects.Single(row => row.Id == "one").SelectedChoice = composition.Objects.Single(row => row.Id == "one").Choices.Single(choice => Equals(choice.AssetPath, libraryAsset));
        await composition.WaitForRefreshAsync();
        var reassigned = (Model3DGroup)composition.ObjectVisuals!.Children[0];
        Assert.Equal(2, reassigned.Bounds.SizeX);
        Assert.Equal(1, environment.Loader.CountFor("LibraryBall.glb"));
    }

    [Fact]
    public async Task BrokenReferenceDiagnosticsTrackRepairAndRemovalCurrentState()
    {
        using var environment = new CompositionEnvironment();
        var valid = environment.WriteObject(AssetReferenceScope.Project, "Ball", 1);
        using var tab = environment.CreateTab(MachineDocument.Create("Pool"));
        tab.AddMachineObjectInstanceCommand.Execute(null);
        var composition = tab.MachineComposition3D!;
        await composition.WaitForRefreshAsync();
        Assert.Contains(composition.Diagnostics, message => message.Contains("no Object3D asset", StringComparison.Ordinal));

        var authoredId = composition.Objects.Single().Id;
        composition.Objects.Single().SelectedChoice = composition.Objects.Single().Choices.Single(choice => Equals(choice.AssetPath, valid));
        await composition.WaitForRefreshAsync();
        Assert.DoesNotContain(composition.Diagnostics, message => message.Contains(authoredId, StringComparison.Ordinal));
        Assert.Equal(authoredId, tab.GetMachineDocument().ObjectInstances.Single().Id);
        Assert.Single(composition.ObjectVisuals!.Children);

        tab.AddMachineObjectInstanceCommand.Execute(null);
        Assert.Contains(composition.Diagnostics, message => message.Contains("object2", StringComparison.Ordinal));
        tab.RemoveMachineObjectInstance("object2");
        Assert.DoesNotContain(composition.Diagnostics, message => message.Contains("object2", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CabinetSemanticOutputsRemainIndependentInComposition()
    {
        using var environment = new CompositionEnvironment();
        var cabinet = environment.WriteCabinet();
        var machine = MachineDocument.Create("Pool") with { CabinetAsset = cabinet };
        using var tab = environment.CreateTab(machine);
        var composition = tab.MachineComposition3D!;
        await composition.WaitForRefreshAsync();

        Assert.NotNull(composition.CabinetVisual);
        Assert.Null(composition.CabinetColliderPreview);
        Assert.Null(composition.CabinetTriggerPreview);
        composition.ShowCabinetCollision = true;
        Assert.NotNull(composition.CabinetColliderPreview);
        Assert.NotNull(composition.CabinetTriggerPreview);
        composition.ShowColliders = false;
        Assert.Null(composition.CabinetColliderPreview);
        Assert.NotNull(composition.CabinetTriggerPreview);
    }

    [Fact]
    public async Task DetailsAndCompositionShareTransformCommandsUndoRedoAndSelectionLifecycle()
    {
        using var environment = new CompositionEnvironment();
        var asset = environment.WriteObject(AssetReferenceScope.Project, "Ball", 1);
        using var tab = environment.CreateTab(MachineDocument.Create("Pool") with { ObjectInstances = [Instance("ball", asset)] });
        var composition = tab.MachineComposition3D!;
        await composition.WaitForRefreshAsync();
        composition.SelectedObject = composition.Objects.Single();

        tab.MachineObjectInstanceRows.Single().PositionX = 4;
        Assert.Equal(4, ((Model3DGroup)composition.ObjectVisuals!.Children.Single()).Transform.Value.OffsetX);
        composition.Objects.Single().PositionY = 5;
        Assert.Equal(5, tab.GetMachineDocument().ObjectInstances.Single().Transform.Position.Y);
        Assert.True(tab.IsDirty);
        Assert.True(tab.CommandService.TryUndo());
        Assert.Equal(0, tab.GetMachineDocument().ObjectInstances.Single().Transform.Position.Y);
        Assert.True(tab.CommandService.TryRedo());
        Assert.Equal(5, tab.GetMachineDocument().ObjectInstances.Single().Transform.Position.Y);

        composition.RemoveSelectedCommand.Execute(null);
        Assert.Empty(composition.Objects);
        Assert.Empty(composition.ObjectVisuals!.Children);
        Assert.Null(composition.SelectedObject);
    }

    [Fact]
    public async Task FiltersAndCameraUseVisibleBoundsWithoutReloadingDefinition()
    {
        using var environment = new CompositionEnvironment();
        var asset = environment.WriteObject(AssetReferenceScope.Project, "Wide", 10);
        using var tab = environment.CreateTab(MachineDocument.Create("Pool") with { ObjectInstances = [Instance("wide", asset)] });
        var composition = tab.MachineComposition3D!;
        await composition.WaitForRefreshAsync();
        var loads = environment.Loader.TotalCount;
        var framedPosition = composition.CameraPosition;

        composition.ShowObjects = false;
        composition.ResetCameraCommand.Execute(null);

        Assert.True(composition.Bounds.IsEmpty);
        Assert.NotEqual(framedPosition, composition.CameraPosition);
        Assert.Equal(loads, environment.Loader.TotalCount);
        Assert.False(tab.IsDirty);
    }

    private static MachineObject3DInstance Instance(string id, AssetReference asset) => new(id, id, asset, MachineObjectTransform.Identity);

    private sealed class CompositionEnvironment : IDisposable
    {
        public CompositionEnvironment()
        {
            Root = Path.Combine(Path.GetTempPath(), "OasisComposition_" + Guid.NewGuid().ToString("N"));
            ProjectRoot = Path.Combine(Root, "Project"); LibraryRoot = Path.Combine(Root, "Library");
            Directory.CreateDirectory(ProjectRoot); Directory.CreateDirectory(LibraryRoot);
            Project = new EditorProject { Name = "Pool", ProjectDirectory = ProjectRoot, ProjectFilePath = Path.Combine(ProjectRoot, "Pool.oasisproj"), AssetsDirectory = Path.Combine(ProjectRoot, "Assets"), GeneratedDirectory = Path.Combine(ProjectRoot, "Generated") };
        }
        public string Root { get; }
        public string ProjectRoot { get; }
        public string LibraryRoot { get; }
        public EditorProject Project { get; }
        public CountingModelLoader Loader { get; } = new();

        public AssetReference WriteObject(AssetReferenceScope scope, string name, double size)
        {
            var reference = scope == AssetReferenceScope.Project
                ? AssetReference.Project($"Assets/Object3D/{name}/asset.object3d")
                : AssetReference.Library($"Object3D/{name}/asset.object3d");
            var root = scope == AssetReferenceScope.Project ? ProjectRoot : LibraryRoot;
            var manifest = Path.Combine(root, reference.Path.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(manifest)!);
            File.WriteAllText(manifest, Object3DDocumentStorage.Serialize(Object3DDocument.Create(name) with { Model = new($"{name}.glb", 1, "Y") }));
            Loader.Results[$"{name}.glb"] = CabinetModelLoadResult.Success(Model(size));
            return reference;
        }

        public AssetReference WriteCabinet()
        {
            var reference = AssetReference.Project("Assets/Cabinet3D/Table/asset.cabinet3d");
            var manifest = Path.Combine(ProjectRoot, reference.Path.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(manifest)!);
            File.WriteAllText(manifest, CabinetDocumentStorage.Serialize(CabinetDocument.FromModelPath("table.glb")));
            Loader.Results["table.glb"] = CabinetModelLoadResult.Success(Model(4), colliderModel: Model(5), triggerModel: Model(6));
            return reference;
        }

        public DocumentTabViewModel CreateTab(MachineDocument machine)
        {
            var tab = new DocumentTabViewModel(EditorDocument.CreateFromFile(Path.Combine(ProjectRoot, "asset.machine"), "Machine", "Pool"), machineDocumentJson: MachineDocumentStorage.Serialize(machine));
            tab.SetProjectAccessor(() => Project); tab.SetLibraryRootAccessor(() => LibraryRoot); tab.MachineCompositionLoaderFactory = () => Loader;
            return tab;
        }

        private static Model3DGroup Model(double size)
        {
            var mesh = new MeshGeometry3D
            {
                Positions = new Point3DCollection { new(0, 0, 0), new(size, 0, 0), new(0, size, size) },
                TriangleIndices = new Int32Collection { 0, 1, 2 }
            };
            return new Model3DGroup { Children = { new GeometryModel3D(mesh, new DiffuseMaterial(Brushes.Gray)) } };
        }
        public void Dispose() { if (Directory.Exists(Root)) Directory.Delete(Root, true); }
    }

    private sealed class CountingModelLoader : ICabinetModelLoader
    {
        private readonly Dictionary<string, int> _counts = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, CabinetModelLoadResult> Results { get; } = new(StringComparer.OrdinalIgnoreCase);
        public int TotalCount => _counts.Values.Sum();
        public int CountFor(string file) => _counts.GetValueOrDefault(file);
        public Task<CabinetModelLoadResult> LoadAsync(string modelPath, CancellationToken cancellationToken = default)
        {
            var file = Path.GetFileName(modelPath); _counts[file] = CountFor(file) + 1;
            return Task.FromResult(Results.TryGetValue(file, out var result) ? result : CabinetModelLoadResult.Failure($"Missing fake model {file}"));
        }
    }
}
