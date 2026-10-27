using System.Text.Json;
using System.Xml.Linq;
using Xunit;

namespace OasisEditor.Tests;

public sealed class OasisRuntimeDefinitionTests
{
    [Fact] public void OasisRoundTripsMinimalShapeUnderSchema11()
    {
        var machine = MachineDocument.Create("Pool") with { Runtime = new OasisRuntimeDefinition(), Behavior = MachineBehaviorDefinition.OasisScript() };
        var json = MachineDocumentStorage.Serialize(machine);
        using var parsed = JsonDocument.Parse(json);
        Assert.Equal(11, parsed.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Single(parsed.RootElement.GetProperty("runtime").EnumerateObject());
        Assert.Equal("Oasis", parsed.RootElement.GetProperty("runtime").GetProperty("kind").GetString());
        Assert.True(MachineDocumentStorage.TryRead(json, out var loaded, out var error), error);
        Assert.IsType<OasisRuntimeDefinition>(loaded.Runtime); Assert.Equal(machine.Behavior, loaded.Behavior);
        Assert.False(MachineDocumentStorage.TryRead(json.Replace("\"schemaVersion\": 11", "\"schemaVersion\": 7"), out _, out error));
        Assert.Contains("only version 11", error);
        Assert.False(MachineDocumentStorage.TryRead(json.Replace("\"Oasis\"", "\"Unknown\""), out _, out error)); Assert.Contains("Unsupported", error);
    }
    [Fact] public void RuntimeBehaviorUnionIsExclusive()
    {
        Assert.Throws<InvalidOperationException>(() => MachineDocumentStorage.Serialize(MachineDocument.Create("Pool") with { Runtime = new OasisRuntimeDefinition() }));
        Assert.Throws<InvalidOperationException>(() => MachineDocumentStorage.Serialize(MachineDocument.Create("Emulator") with { Behavior = MachineBehaviorDefinition.OasisScript() }));
        var emulator = MachineDocument.Create("Emulator"); var json = MachineDocumentStorage.Serialize(emulator);
        Assert.True(MachineDocumentStorage.TryRead(json, out var loaded, out var error), error); Assert.IsType<EmulationRuntimeDefinition>(loaded.Runtime); Assert.Null(loaded.Behavior);
    }
}
