using System.Windows.Media.Media3D;
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
}
