using OasisEditor.Features.MachineEditor.Models;
using OasisEditor.Features.MachineEditor.Services;
using Xunit;

namespace OasisEditor.Tests;

public sealed class ActiveMachineContextTests
{
    [Fact]
    public void TwoMachinesKeepIsolatedRuntimeSettings()
    {
        var root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "OasisEditorTests", Guid.NewGuid().ToString("N"))).FullName;
        var project = TestProjectFactory.CreateDefaultMachine(root, "ProjectA", "Machine A");
        var secondMachineDir = new ProjectAssetPathService().CreateAssetPackageDirectory(project, EditorAssetType.Machine, "Machine B");
        var machineB = MachineDocumentExtensions.Empty("Machine B") with
        {
            Runtime = MachineRuntimeDefinition.CreateDefault() with { Platform = FruitMachinePlatformType.MPU5 }
        };
        File.WriteAllText(Path.Combine(secondMachineDir.FullName, ProjectAssetPathService.MachineManifestFileName), MachineDocumentStorage.Serialize(machineB));

        var context = new ActiveMachineContext();
        Assert.False(context.TryAutoSelectFromProject(project));

        var firstPath = MachineAssetDiscovery.EnumerateMachineManifestPaths(project)[0];
        var firstDocument = new ProjectAssetReferenceResolver().ResolveMachineDocument(project, firstPath)!;
        var firstDocumentImpact = firstDocument with
        {
            Runtime = MachineRuntimeDefinition.CreateDefault() with { Platform = FruitMachinePlatformType.Impact }
        };
        context.SetActiveMachine(Path.Combine(project.ProjectDirectory, firstPath.Replace('/', Path.DirectorySeparatorChar)), firstDocumentImpact);

        var secondPath = MachineAssetDiscovery.EnumerateMachineManifestPaths(project)[1];
        var secondDocument = new ProjectAssetReferenceResolver().ResolveMachineDocument(project, secondPath)!;
        context.SetActiveMachine(Path.Combine(project.ProjectDirectory, secondPath.Replace('/', Path.DirectorySeparatorChar)), secondDocument);

        Assert.Equal(FruitMachinePlatformType.MPU5, context.Document!.Runtime.Platform);

        context.SetActiveMachine(Path.Combine(project.ProjectDirectory, firstPath.Replace('/', Path.DirectorySeparatorChar)), firstDocumentImpact);
        Assert.Equal(FruitMachinePlatformType.Impact, context.Document!.Runtime.Platform);
    }
}
