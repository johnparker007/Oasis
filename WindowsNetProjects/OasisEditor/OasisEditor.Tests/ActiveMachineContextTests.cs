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

    [Fact]
    public void TrySyncFromOpenDocument_UpdatesActiveRuntimeWhenSelectedMachineMutates()
    {
        var root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "OasisEditorTests", Guid.NewGuid().ToString("N"))).FullName;
        var project = TestProjectFactory.CreateDefaultMachine(root, "ProjectA", "Machine A");
        var manifestPath = Path.Combine(project.ProjectDirectory, MachineAssetDiscovery.EnumerateMachineManifestPaths(project)[0].Replace('/', Path.DirectorySeparatorChar));
        var tab = new DocumentTabViewModel(EditorDocument.CreateFromFile(manifestPath, "Machine A"), machineDocumentJson: File.ReadAllText(manifestPath));
        tab.SetProjectAccessor(() => project);

        var context = new ActiveMachineContext();
        context.SetActiveMachine(manifestPath, tab.GetMachineDocument());

        tab.MachineEditor!.SelectedPlatform = FruitMachinePlatformType.MaygayM1;

        Assert.True(context.TrySyncFromOpenDocument(tab.FilePath, tab.GetMachineDocument()));
        Assert.Equal(FruitMachinePlatformType.MaygayM1, context.Document!.Runtime.Platform);
    }

    [Fact]
    public void TrySyncFromOpenDocument_IgnoresMutationsFromNonActiveMachine()
    {
        var root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "OasisEditorTests", Guid.NewGuid().ToString("N"))).FullName;
        var project = TestProjectFactory.CreateDefaultMachine(root, "ProjectA", "Machine A");
        var pathService = new ProjectAssetPathService();
        var machineBDir = pathService.CreateAssetPackageDirectory(project, EditorAssetType.Machine, "Machine B");
        var machineB = MachineDocumentExtensions.Empty("Machine B") with
        {
            Runtime = MachineRuntimeDefinition.CreateDefault() with { Platform = FruitMachinePlatformType.MPU5 }
        };
        File.WriteAllText(Path.Combine(machineBDir.FullName, ProjectAssetPathService.MachineManifestFileName), MachineDocumentStorage.Serialize(machineB));

        var machineAPath = Path.Combine(project.ProjectDirectory, MachineAssetDiscovery.EnumerateMachineManifestPaths(project)[0].Replace('/', Path.DirectorySeparatorChar));
        var machineBPath = Path.Combine(machineBDir.FullName, ProjectAssetPathService.MachineManifestFileName);
        var tabA = new DocumentTabViewModel(EditorDocument.CreateFromFile(machineAPath, "Machine A"), machineDocumentJson: File.ReadAllText(machineAPath));
        var tabB = new DocumentTabViewModel(EditorDocument.CreateFromFile(machineBPath, "Machine B"), machineDocumentJson: File.ReadAllText(machineBPath));
        tabA.SetProjectAccessor(() => project);
        tabB.SetProjectAccessor(() => project);

        var context = new ActiveMachineContext();
        context.SetActiveMachine(machineBPath, tabB.GetMachineDocument());

        tabA.MachineEditor!.SelectedPlatform = FruitMachinePlatformType.Impact;

        Assert.False(context.TrySyncFromOpenDocument(tabA.FilePath, tabA.GetMachineDocument()));
        Assert.Equal(FruitMachinePlatformType.MPU5, context.Document!.Runtime.Platform);
        Assert.Equal(FruitMachinePlatformType.Impact, tabA.GetMachineDocument().Runtime.Platform);
        Assert.Equal(FruitMachinePlatformType.MPU5, tabB.GetMachineDocument().Runtime.Platform);
    }

    [Fact]
    public void MachineDocumentMutationsRemainIsolatedAcrossOpenMachines()
    {
        var root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "OasisEditorTests", Guid.NewGuid().ToString("N"))).FullName;
        var project = TestProjectFactory.CreateDefaultMachine(root, "ProjectA", "Machine A");
        var pathService = new ProjectAssetPathService();
        var machineBDir = pathService.CreateAssetPackageDirectory(project, EditorAssetType.Machine, "Machine B");
        var machineB = MachineDocumentExtensions.Empty("Machine B") with
        {
            Runtime = MachineRuntimeDefinition.CreateDefault() with { Platform = FruitMachinePlatformType.MPU5 }
        };
        File.WriteAllText(Path.Combine(machineBDir.FullName, ProjectAssetPathService.MachineManifestFileName), MachineDocumentStorage.Serialize(machineB));

        var machineAPath = Path.Combine(project.ProjectDirectory, MachineAssetDiscovery.EnumerateMachineManifestPaths(project)[0].Replace('/', Path.DirectorySeparatorChar));
        var machineBPath = Path.Combine(machineBDir.FullName, ProjectAssetPathService.MachineManifestFileName);
        var tabA = new DocumentTabViewModel(EditorDocument.CreateFromFile(machineAPath, "Machine A"), machineDocumentJson: File.ReadAllText(machineAPath));
        var tabB = new DocumentTabViewModel(EditorDocument.CreateFromFile(machineBPath, "Machine B"), machineDocumentJson: File.ReadAllText(machineBPath));
        tabA.SetProjectAccessor(() => project);
        tabB.SetProjectAccessor(() => project);
        tabA.RuntimeState.FruitMachinePlatform = FruitMachinePlatformType.Impact;
        tabB.RuntimeState.FruitMachinePlatform = FruitMachinePlatformType.MPU5;

        tabA.MachineEditor!.SelectedPlatform = FruitMachinePlatformType.MaygayM1;

        Assert.Equal(FruitMachinePlatformType.MaygayM1, tabA.GetMachineDocument().Runtime.Platform);
        Assert.Equal(FruitMachinePlatformType.MPU5, tabB.GetMachineDocument().Runtime.Platform);
        Assert.Equal(FruitMachinePlatformType.MPU5, tabB.RuntimeState.FruitMachinePlatform);
    }
}
