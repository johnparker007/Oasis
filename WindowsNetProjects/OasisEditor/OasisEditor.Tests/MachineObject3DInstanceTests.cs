using System.Text.Json;
using Xunit;

namespace OasisEditor.Tests;

public sealed class MachineObject3DInstanceTests
{
    private static readonly AssetReference ProjectBall = AssetReference.Project("Assets/Object3D/Pool Ball/asset.object3d");
    private static readonly AssetReference LibraryBall = AssetReference.Library("Object3D/Pool Ball/asset.object3d");

    [Fact]
    public void EmptyInstances_RoundTripInSchema8()
    {
        var json = MachineDocumentStorage.Serialize(MachineDocument.Create("Pool"));
        Assert.True(MachineDocumentStorage.TryRead(json, out var reopened, out var error), error);
        Assert.Equal(8, reopened.SchemaVersion);
        Assert.Empty(reopened.ObjectInstances);
    }

    [Fact]
    public void MultipleIndependentProjectAndLibraryInstancesAndTransforms_RoundTrip()
    {
        var machine = MachineDocument.Create("Pool") with { ObjectInstances =
        [
            new("cueBall", "Cue Ball", ProjectBall, MachineObjectTransform.Identity),
            new("ball01", "Ball 1", LibraryBall, new(new(1, 2, 3), new(10, 20, 30), new(.5, .75, 1.25))),
            new("ball08", "Ball 8", LibraryBall, new(new(-1, 0, 4), new(0, 90, 0), new(1, 1, 1)))
        ]};

        var json = MachineDocumentStorage.Serialize(machine);
        Assert.True(MachineDocumentStorage.TryRead(json, out var reopened, out var error), error);
        Assert.Equal(["cueBall", "ball01", "ball08"], reopened.ObjectInstances.Select(instance => instance.Id).ToArray());
        Assert.Equal(ProjectBall, reopened.ObjectInstances[0].ObjectAsset);
        Assert.Equal(LibraryBall, reopened.ObjectInstances[1].ObjectAsset);
        Assert.Equal(new MachineVector3(1, 2, 3), reopened.ObjectInstances[1].Transform.Position);
        Assert.Equal(new MachineVector3(10, 20, 30), reopened.ObjectInstances[1].Transform.Rotation);
        Assert.Equal(new MachineVector3(.5, .75, 1.25), reopened.ObjectInstances[1].Transform.Scale);
        Assert.Equal(MachineObjectTransform.Identity, reopened.ObjectInstances[0].Transform);
    }

    [Theory]
    [InlineData("")]
    [InlineData("has space")]
    [InlineData("ball:08")]
    public void InvalidIds_AreRejected(string id) => Assert.Throws<InvalidOperationException>(() => MachineDocumentStorage.Serialize(With(new(id, "Ball", ProjectBall, MachineObjectTransform.Identity))));

    [Fact]
    public void DuplicateIds_AreCaseSensitiveAndExactDuplicatesAreRejected()
    {
        Assert.Throws<InvalidOperationException>(() => MachineDocumentStorage.Serialize(MachineDocument.Create("Pool") with { ObjectInstances = [Instance("ball01"), Instance("ball01")] }));
        _ = MachineDocumentStorage.Serialize(MachineDocument.Create("Pool") with { ObjectInstances = [Instance("ball01"), Instance("Ball01")] });
    }

    [Fact]
    public void EmptyDisplayNameAndMissingAssetAreRejected()
    {
        Assert.Throws<InvalidOperationException>(() => MachineDocumentStorage.Serialize(With(Instance("ball") with { DisplayName = " " })));
        Assert.Throws<InvalidOperationException>(() => MachineDocumentStorage.Serialize(With(Instance("ball") with { ObjectAsset = null })));
    }

    [Fact]
    public void InvalidScaleAndNonFiniteTransformsAreRejected()
    {
        Assert.Throws<InvalidOperationException>(() => MachineDocumentStorage.Serialize(With(Instance("zero") with { Transform = MachineObjectTransform.Identity with { Scale = new(1, 0, 1) } })));
        Assert.Throws<InvalidOperationException>(() => MachineDocumentStorage.Serialize(With(Instance("nan") with { Transform = MachineObjectTransform.Identity with { Position = new(double.NaN, 0, 0) } })));
        Assert.Throws<InvalidOperationException>(() => MachineDocumentStorage.Serialize(With(Instance("infinity") with { Transform = MachineObjectTransform.Identity with { Rotation = new(0, double.PositiveInfinity, 0) } })));
    }

    [Fact]
    public void AddRemoveAssetIdAndTransformEditsSupportUndoRedo()
    {
        var tab = Tab(MachineDocument.Create("Pool"));
        tab.AddMachineObjectInstanceCommand.Execute(null);
        Assert.True(tab.IsDirty); Assert.Single(tab.GetMachineDocument().ObjectInstances);
        Assert.True(tab.CommandService.TryUndo()); Assert.Empty(tab.GetMachineDocument().ObjectInstances);
        Assert.True(tab.CommandService.TryRedo()); Assert.Single(tab.GetMachineDocument().ObjectInstances);

        var row = Assert.Single(tab.MachineObjectInstanceRows);
        row.Id = "cueBall";
        row.SelectedChoice = new MachineAssetChoice("Ball", ProjectBall);
        row.PositionX = 1.5;
        Assert.Equal("cueBall", tab.GetMachineDocument().ObjectInstances[0].Id);
        Assert.Equal(ProjectBall, tab.GetMachineDocument().ObjectInstances[0].ObjectAsset);
        Assert.Equal(1.5, tab.GetMachineDocument().ObjectInstances[0].Transform.Position.X);
        Assert.True(tab.CommandService.TryUndo()); Assert.Equal(0, tab.GetMachineDocument().ObjectInstances[0].Transform.Position.X);
        Assert.True(tab.CommandService.TryRedo()); Assert.Equal(1.5, tab.GetMachineDocument().ObjectInstances[0].Transform.Position.X);

        tab.MachineObjectInstanceRows[0].RemoveCommand.Execute(null);
        Assert.Empty(tab.GetMachineDocument().ObjectInstances);
        Assert.True(tab.CommandService.TryUndo()); Assert.Single(tab.GetMachineDocument().ObjectInstances);
        Assert.True(tab.CommandService.TryRedo()); Assert.Empty(tab.GetMachineDocument().ObjectInstances);
    }

    [Fact]
    public void DuplicateOrInvalidIdEditIsRejectedWithoutChangingDisplayName()
    {
        var tab = Tab(MachineDocument.Create("Pool") with { ObjectInstances = [Instance("cueBall"), Instance("ball01")] });
        var row = tab.MachineObjectInstanceRows[1];
        var idNotifications = 0;
        row.PropertyChanged += (_, args) => { if (args.PropertyName == nameof(MachineObjectInstanceRow.Id)) idNotifications++; };
        row.Id = "cueBall";
        Assert.Equal("ball01", tab.GetMachineDocument().ObjectInstances[1].Id);
        Assert.Equal("ball01", row.Id);
        row.Id = "bad id";
        Assert.Equal("ball01", tab.GetMachineDocument().ObjectInstances[1].Id);
        Assert.Equal("ball01", row.Id);
        Assert.Equal("ball01", row.DisplayName);
        Assert.Equal(2, idNotifications);
    }

    [Fact]
    public void SelectingAndClearingObjectAssetImmediatelyUpdatesOpenAvailability()
    {
        var root = Path.Combine(Path.GetTempPath(), "OasisMachineObjectOpen_" + Guid.NewGuid().ToString("N"));
        try
        {
            var project = new EditorProject { Name = "Pool", ProjectDirectory = root, ProjectFilePath = Path.Combine(root, "Pool.oasisproj"), AssetsDirectory = Path.Combine(root, "Assets"), GeneratedDirectory = Path.Combine(root, "Generated") };
            var manifest = new ProjectAssetPathService().GetObject3DManifestPath(project, "Ball");
            Directory.CreateDirectory(Path.GetDirectoryName(manifest)!);
            File.WriteAllText(manifest, Object3DDocumentStorage.Serialize(Object3DDocument.Create("Ball")));
            var tab = Tab(MachineDocument.Create("Pool"));
            tab.SetProjectAccessor(() => project);
            tab.SetAssetDocumentOpener(_ => { });
            tab.AddMachineObjectInstanceCommand.Execute(null);
            var row = Assert.Single(tab.MachineObjectInstanceRows);
            Assert.False(row.OpenSelectedAssetCommand.CanExecute(null));

            row.SelectedChoice = row.Choices.Single(choice => Equals(choice.AssetPath, AssetReference.Project("Assets/Object3D/Ball/asset.object3d")));
            Assert.True(row.OpenSelectedAssetCommand.CanExecute(null));

            row.SelectedChoice = row.Choices.Single(choice => choice.AssetPath is null);
            Assert.False(row.OpenSelectedAssetCommand.CanExecute(null));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void MachineChoicesContainOnlyProjectAndLibraryObject3DAssets()
    {
        var root = Path.Combine(Path.GetTempPath(), "OasisMachineObjects_" + Guid.NewGuid().ToString("N"));
        try
        {
            var project = new EditorProject { Name = "Pool", ProjectDirectory = root, ProjectFilePath = Path.Combine(root, "Pool.oasisproj"), AssetsDirectory = Path.Combine(root, "Assets"), GeneratedDirectory = Path.Combine(root, "Generated") };
            var paths = new ProjectAssetPathService();
            var projectObject = paths.GetObject3DManifestPath(project, "Project Ball");
            Directory.CreateDirectory(Path.GetDirectoryName(projectObject)!);
            File.WriteAllText(projectObject, Object3DDocumentStorage.Serialize(Object3DDocument.Create("Project Ball")));
            var reel = paths.GetReelManifestPath(project, "Not An Object");
            Directory.CreateDirectory(Path.GetDirectoryName(reel)!);
            File.WriteAllText(reel, ReelDocumentStorage.Serialize(ReelDocument.Create("Not An Object")));

            var library = Path.Combine(root, "Library");
            var libraryPackage = Path.Combine(library, "Object3D", "Library Ball");
            Directory.CreateDirectory(libraryPackage);
            File.WriteAllText(Path.Combine(libraryPackage, "asset.object3d"), Object3DDocumentStorage.Serialize(Object3DDocument.Create("Library Ball")));
            File.WriteAllBytes(Path.Combine(libraryPackage, "model.glb"), [1]);

            var tab = Tab(MachineDocument.Create("Pool"));
            tab.SetLibraryRootAccessor(() => library);
            tab.SetProjectAccessor(() => project);
            tab.AddMachineObjectInstanceCommand.Execute(null);
            var choices = Assert.Single(tab.MachineObjectInstanceRows).Choices;
            Assert.Contains(choices, choice => choice.DisplayName == "Project Ball" && Equals(choice.AssetPath, AssetReference.Project("Assets/Object3D/Project Ball/asset.object3d")));
            Assert.Contains(choices, choice => choice.DisplayName == "Library Ball [Library]" && Equals(choice.AssetPath, AssetReference.Library("Object3D/Library Ball/asset.object3d")));
            Assert.DoesNotContain(choices, choice => choice.DisplayName.Contains("Not An Object", StringComparison.Ordinal));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    private static MachineObject3DInstance Instance(string id) => new(id, id, ProjectBall, MachineObjectTransform.Identity);
    private static MachineDocument With(MachineObject3DInstance instance) => MachineDocument.Create("Pool") with { ObjectInstances = [instance] };
    private static DocumentTabViewModel Tab(MachineDocument machine) => new(EditorDocument.CreateFromFile("C:/Project/Assets/Machines/Pool/asset.machine", "Machine"), machineDocumentJson: MachineDocumentStorage.Serialize(machine));
}
