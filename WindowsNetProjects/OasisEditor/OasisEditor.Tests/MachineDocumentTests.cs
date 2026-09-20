using System.Text.Json;

namespace OasisEditor.Tests;

public sealed class MachineDocumentTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "OasisMachineTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Storage_RoundTripsLatestSchemaAndRejectsWrongVersion()
    {
        var machine = MachineDocument.Create("Bonanza") with
        {
            CabinetAssetPath = "Assets/Cabinet3D/Vogue/asset.cabinet3d",
            SurfaceAssignments = [new("OasisFace_TopGlass", "Assets/Faces/Top/asset.face")],
            ReelAssignments = [new(MachineObjectReference.Reel(0), "standard")],
            Runtime = MachineEmulationRuntime.ForPlatform(FruitMachinePlatformType.MPU5, new Mpu5NativeRomSettings())
        };

        var json = MachineDocumentStorage.Write(machine);
        Assert.True(MachineDocumentStorage.TryRead(json, out var restored));
        Assert.Equal(machine.Id, restored.Id);
        Assert.Equal(FruitMachinePlatformType.MPU5, restored.Runtime.Platform);
        Assert.Single(restored.SurfaceAssignments);
        Assert.False(MachineDocumentStorage.TryRead(json.Replace("\"version\": 1", "\"version\": 0"), out _));
    }

    [Fact]
    public void AssetPath_UsesCanonicalMachinePackage()
    {
        var project = Project("Workspace");
        Assert.Equal(Path.Combine(_root, "Assets", "Machines", "Machine A", "asset.machine"), new ProjectAssetPathService().GetMachineManifestPath(project, "Machine A"));
    }

    [Fact]
    public void ActiveContext_DoesNotLeakBetweenTwoMachines()
    {
        var first = MachineDocument.Create("A") with { Runtime = MachineEmulationRuntime.ForPlatform(FruitMachinePlatformType.MPU5, new Mpu5NativeRomSettings { Stake = 10 }) };
        var second = MachineDocument.Create("B") with { Runtime = MachineEmulationRuntime.ForPlatform(FruitMachinePlatformType.Epoch, new EpochNativeRomSettings { Stake = 20 }) };
        var context = new ActiveMachineContext();
        context.Select(Path.Combine(_root, "A", "asset.machine"), first);
        Assert.Equal((uint)10, context.ActiveMachine!.Runtime.ReadSettings<Mpu5NativeRomSettings>().Stake);
        context.Select(Path.Combine(_root, "B", "asset.machine"), second);
        Assert.Equal(FruitMachinePlatformType.Epoch, context.ActiveMachine!.Runtime.Platform);
        Assert.Equal((uint)20, context.ActiveMachine.Runtime.ReadSettings<EpochNativeRomSettings>().Stake);
        Assert.Equal(FruitMachinePlatformType.MPU5, first.Runtime.Platform);
    }

    [Fact]
    public void NewProject_IsWorkspaceOnlyAndCreatesOneMachine()
    {
        Directory.CreateDirectory(_root);
        var directory = new ProjectScaffolder().CreateProject("Example", _root);
        using var project = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "Example.oasisproj")));
        Assert.Equal(EditorProject.CurrentSchemaVersion, project.RootElement.GetProperty("version").GetInt32());
        Assert.False(project.RootElement.TryGetProperty("project_settings", out _));
        Assert.False(project.RootElement.GetProperty("layout").TryGetProperty("machines", out _));
        var path = Path.Combine(directory, "Assets", "Machines", "Example", "asset.machine");
        Assert.True(MachineDocumentStorage.TryRead(File.ReadAllText(path), out var machine));
        Assert.Equal("Example", machine.DisplayName);
    }

    private EditorProject Project(string name)
    {
        Directory.CreateDirectory(Path.Combine(_root, "Assets"));
        return new EditorProject { Name = name, ProjectFilePath = Path.Combine(_root, name + ".oasisproj"), ProjectDirectory = _root, AssetsDirectory = Path.Combine(_root, "Assets"), MachinesDirectory = Path.Combine(_root, "Machines"), GeneratedDirectory = Path.Combine(_root, "Generated") };
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
