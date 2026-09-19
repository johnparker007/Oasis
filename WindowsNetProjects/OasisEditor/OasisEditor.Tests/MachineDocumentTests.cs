using Xunit;
using System.Text.Json;
using OasisEditor.Features.CabinetEditor.Models;

namespace OasisEditor.Tests;

public sealed class MachineDocumentTests
{
    [Fact]
    public void Schema1_RoundTripsCompositionRuntimeAndInputs()
    {
        var machine = MachineDocument.Create("Machine A") with
        {
            CabinetAssetPath = "Assets/Cabinet3D/Vogue/asset.cabinet3d",
            SurfaceAssignments = [new("OasisFace_TopGlass", "Assets/Faces/Top/asset.face")],
            ReelAssignments = [new(MachineObjectReference.Reel(0), "standard")],
            Runtime = new MachineEmulationRuntime(FruitMachinePlatformType.MPU5, new Mpu5NativeRomSettings { ProgramRom1Path = "Assets/ROMs/game.bin" })
        };
        var json = MachineDocumentStorage.Serialize(machine);
        Assert.True(MachineDocumentStorage.TryRead(json, out var result, out var error), error);
        Assert.Equal(machine.Id, result.Id);
        Assert.Equal(FruitMachinePlatformType.MPU5, result.Runtime.Platform);
        Assert.Equal("Assets/ROMs/game.bin", result.Runtime.SettingsAs<Mpu5NativeRomSettings>().ProgramRom1Path);
        Assert.Single(result.SurfaceAssignments);
        Assert.Single(result.ReelAssignments);
    }

    [Fact]
    public void WrongSchema_IsRejectedWithoutCompatibilityFallback()
    {
        var json = MachineDocumentStorage.Serialize(MachineDocument.Create("Machine")).Replace("\"schemaVersion\": 1", "\"schemaVersion\": 0");
        Assert.False(MachineDocumentStorage.TryRead(json, out _, out var error));
        Assert.Contains("only version 1", error);
    }

    [Fact]
    public void TwoMachines_KeepRuntimeSettingsIsolated()
    {
        var a = MachineDocument.Create("A") with { Runtime = new(FruitMachinePlatformType.MPU5, new Mpu5NativeRomSettings { ProgramRom1Path = "a.bin" }) };
        var b = MachineDocument.Create("B") with { Runtime = new(FruitMachinePlatformType.Epoch, new EpochNativeRomSettings { ProgramRom1Path = "b.bin" }) };
        Assert.Equal("a.bin", a.Runtime.SettingsAs<Mpu5NativeRomSettings>().ProgramRom1Path);
        Assert.Equal("b.bin", b.Runtime.SettingsAs<EpochNativeRomSettings>().ProgramRom1Path);
        Assert.NotEqual(a.Runtime.Platform, b.Runtime.Platform);
    }

    [Fact]
    public void OneDocumentAuthority_PreservesUnsavedCompositionRuntimeAndInputs()
    {
        var tab = new DocumentTabViewModel(EditorDocument.CreateFromFile("C:/Project/Assets/Machines/Game/asset.machine", "Machine"), machineDocumentJson: MachineDocumentStorage.Serialize(MachineDocument.Create("Game")));
        tab.MachineCabinetAssetPath = "Assets/Cabinet3D/Vogue/asset.cabinet3d";
        tab.ExecuteMachineMutation(machine => machine with { Runtime = new(FruitMachinePlatformType.MPU5, new Mpu5NativeRomSettings { ProgramRom1Path = "game.bin" }) }, "Runtime");
        tab.ExecuteMachineMutation(machine => machine with { InputDefinitions = [new InputDefinitionModel { Id = "start" }] }, "Inputs");
        var result = tab.GetMachineDocument();
        Assert.Equal("Assets/Cabinet3D/Vogue/asset.cabinet3d", result.CabinetAssetPath);
        Assert.Equal("game.bin", result.Runtime.SettingsAs<Mpu5NativeRomSettings>().ProgramRom1Path);
        Assert.Single(result.InputDefinitions);
        Assert.True(tab.IsDirty);
    }

    [Fact]
    public void EditedDisplayName_IsNotReplacedByPackageTitleWhenSaved()
    {
        var tab = new DocumentTabViewModel(EditorDocument.CreateFromFile("C:/Project/Assets/Machines/Package Name/asset.machine", "Machine"), machineDocumentJson: MachineDocumentStorage.Serialize(MachineDocument.Create("Original")));
        tab.MachineDisplayName = "Authored Display Name";
        var json = DocumentWorkspaceViewModel.BuildDocumentContent(tab);
        Assert.True(MachineDocumentStorage.TryRead(json, out var reopened, out var error), error);
        Assert.Equal("Authored Display Name", reopened.DisplayName);
        Assert.Equal("Package Name", tab.Document.Title);
    }

    [Theory]
    [InlineData(0, null)]
    [InlineData(1, "machine-0")]
    [InlineData(2, null)]
    public void StartupSelection_AutomaticallySelectsExactlyOneMachine(int count, string? expected)
    {
        var paths = Enumerable.Range(0, count).Select(index => $"machine-{index}").ToArray();
        Assert.Equal(expected, MachineStartupSelectionPolicy.SelectAutomatic(paths));
    }

    [Fact]
    public void RuntimeProjection_RetainsConcreteSelectedPlatformSettingsAsJson()
    {
        var runtime = new MachineEmulationRuntime(FruitMachinePlatformType.MPU5, new Mpu5NativeRomSettings { ProgramRom1Path = "game.bin" });
        var projected = MachineRuntimeDefinition.From(runtime);
        Assert.False(projected.ExecutionSupportedByPlayer);
        Assert.Equal("MPU5", projected.Platform);
        using var settings = JsonDocument.Parse(projected.PlatformSettingsJson);
        Assert.Equal("game.bin", settings.RootElement.GetProperty("programRom1Path").GetString());
    }

    [Fact]
    public void MachineEditor_DiscoversCabinetsAndEditsTemporaryReelAssignments()
    {
        var root = Path.Combine(Path.GetTempPath(), "OasisMachineEditor_" + Guid.NewGuid().ToString("N"));
        try
        {
            var assets = Path.Combine(root, "Assets"); var cabinetPath = Path.Combine(assets, "Cabinet3D", "Vogue", "asset.cabinet3d");
            Directory.CreateDirectory(Path.GetDirectoryName(cabinetPath)!);
            File.WriteAllText(cabinetPath, CabinetDocumentStorage.Serialize(CabinetDocument.FromModelPath("missing.glb") with { ReelSpecifications = [new("standard", "Standard", 210, 50)] }));
            var project = new EditorProject { Name = "Project", ProjectFilePath = Path.Combine(root, "Project.oasisproj"), ProjectDirectory = root, AssetsDirectory = assets, GeneratedDirectory = Path.Combine(root, "Generated") };
            var machine = MachineDocument.Create("Game") with { CabinetAssetPath = "Assets/Cabinet3D/Vogue/asset.cabinet3d" };
            var tab = new DocumentTabViewModel(EditorDocument.CreateFromFile(Path.Combine(assets, "Machines", "Game", "asset.machine"), "Machine"), machineDocumentJson: MachineDocumentStorage.Serialize(machine));
            tab.SetProjectAccessor(() => project);
            Assert.Contains(tab.MachineCabinetChoices, choice => choice.DisplayName == "Vogue");
            var reelZero = Assert.Single(tab.MachineReelAssignmentRows.Where(row => row.Reference == MachineObjectReference.Reel(0)));
            reelZero.Selected = Assert.Single(reelZero.Choices.Where(choice => choice.AssetPath == "standard"));
            Assert.Equal("standard", Assert.Single(tab.GetMachineDocument().ReelAssignments).CabinetReelSpecificationId);
            Assert.True(tab.CommandService.CanUndo);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void SettingsBinding_SwitchingMachinesCreatesIsolatedViewModelModels()
    {
        var machineA = MachineDocument.Create("A") with { Runtime = new(FruitMachinePlatformType.MPU3, new Mpu3ProjectSettings()) };
        var machineBSettings = new Mpu3ProjectSettings(); machineBSettings.ProgramRoms[0].Path = "b.rom";
        var machineB = MachineDocument.Create("B") with { Runtime = new(FruitMachinePlatformType.MPU3, machineBSettings) };
        var boundA = MachineRuntimeSettingsBinding.CreateEditableSnapshot<Mpu3ProjectSettings>(machineA);
        var boundB = MachineRuntimeSettingsBinding.CreateEditableSnapshot<Mpu3ProjectSettings>(machineB);
        var viewModelA = new Mpu3ProjectSettingsViewModel(boundA, _ => { });
        var viewModelB = new Mpu3ProjectSettingsViewModel(boundB, _ => { });
        viewModelA.ProgramRoms[0].Path = "a-edited.rom";
        Assert.Equal("b.rom", viewModelB.ProgramRoms[0].Path);
        Assert.Equal(string.Empty, machineA.Runtime.SettingsAs<Mpu3ProjectSettings>().ProgramRoms[0].Path);
        Assert.Equal("b.rom", machineB.Runtime.SettingsAs<Mpu3ProjectSettings>().ProgramRoms[0].Path);
    }
}
