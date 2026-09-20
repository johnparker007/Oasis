using OasisEditor.Features.MachineEditor.Models;
using Xunit;

namespace OasisEditor.Tests;

public sealed class MachineDocumentStorageTests
{
    [Fact]
    public void SerializeAndTryRead_RoundTripCurrentSchema()
    {
        var document = new MachineDocument(
            1,
            "machine-id",
            "Test Machine",
            "Summary",
            "Assets/Cabinet3D/TestCabinet",
            [new MachineSurfaceAssignment("top", "Assets/Faces/Top")],
            [new MachineReelAssignment(MachineObjectReference.Reel(0), "standard")],
            MachineRuntimeDefinition.CreateDefault() with { Platform = FruitMachinePlatformType.Impact },
            [new InputDefinitionModel { Id = "input-1", ButtonNumber = "1", KeyboardShortcut = "A" }]);

        var json = MachineDocumentStorage.Serialize(document);
        Assert.True(MachineDocumentStorage.TryRead(json, out var parsed));
        Assert.Equal(document.Id, parsed.Id);
        Assert.Equal(document.Title, parsed.Title);
        Assert.Equal(document.CabinetAssetPath, parsed.CabinetAssetPath);
        Assert.Single(parsed.SurfaceAssignments);
        Assert.Single(parsed.ReelAssignments);
        Assert.Equal(FruitMachinePlatformType.Impact, parsed.Runtime.Platform);
        Assert.Single(parsed.InputDefinitions);
    }

    [Fact]
    public void TryRead_RejectsUnsupportedSchemaVersion()
    {
        const string json = "{\"version\":0,\"id\":\"x\",\"title\":\"Machine\",\"runtime\":{\"kind\":\"Emulation\",\"platform\":\"None\"}}";
        Assert.False(MachineDocumentStorage.TryRead(json, out _));
    }
}
