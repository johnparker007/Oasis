using Xunit;

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
}
