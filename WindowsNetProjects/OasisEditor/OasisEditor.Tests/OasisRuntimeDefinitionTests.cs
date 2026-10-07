using System.Text.Json;
using System.Xml.Linq;
using Xunit;

namespace OasisEditor.Tests;

public sealed class OasisRuntimeDefinitionTests
{
    [Fact] public void OasisRoundTripsMinimalShapeUnderSchema8()
    {
        var machine = MachineDocument.Create("Pool") with { Runtime = new OasisRuntimeDefinition(), Behavior = MachineBehaviorDefinition.OasisScript() };
        var json = MachineDocumentStorage.Serialize(machine);
        using var parsed = JsonDocument.Parse(json);
        Assert.Equal(8, parsed.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Single(parsed.RootElement.GetProperty("runtime").EnumerateObject());
        Assert.Equal("Oasis", parsed.RootElement.GetProperty("runtime").GetProperty("kind").GetString());
        Assert.True(MachineDocumentStorage.TryRead(json, out var loaded, out var error), error);
        Assert.IsType<OasisRuntimeDefinition>(loaded.Runtime); Assert.Equal(machine.Behavior, loaded.Behavior);
        Assert.False(MachineDocumentStorage.TryRead(json.Replace("\"schemaVersion\": 8", "\"schemaVersion\": 7"), out _, out error));
        Assert.Contains("only version 8", error);
        Assert.False(MachineDocumentStorage.TryRead(json.Replace("\"Oasis\"", "\"Unknown\""), out _, out error)); Assert.Contains("Unsupported", error);
    }
    [Fact] public void RuntimeBehaviorUnionIsExclusive()
    {
        Assert.Throws<InvalidOperationException>(() => MachineDocumentStorage.Serialize(MachineDocument.Create("Pool") with { Runtime = new OasisRuntimeDefinition() }));
        Assert.Throws<InvalidOperationException>(() => MachineDocumentStorage.Serialize(MachineDocument.Create("Emulator") with { Behavior = MachineBehaviorDefinition.OasisScript() }));
        var emulator = MachineDocument.Create("Emulator"); var json = MachineDocumentStorage.Serialize(emulator);
        Assert.True(MachineDocumentStorage.TryRead(json, out var loaded, out var error), error); Assert.IsType<EmulationRuntimeDefinition>(loaded.Runtime); Assert.Null(loaded.Behavior);
    }
    [Fact] public void RuntimeSelectionAndBehaviorAddAreAtomicUndoableMutations()
    {
        var tab = new DocumentTabViewModel(EditorDocument.CreateMachineStub("Machine"));
        tab.MachinePlatform = FruitMachinePlatformType.MPU5;
        tab.MachineRuntimeKind = "Oasis";
        Assert.False(tab.IsMachineEmulationRuntime); Assert.True(tab.HasMachineBehavior); Assert.Equal(DocumentTabViewModel.DefaultMachineBehaviorSource, tab.MachineBehaviorSource);
        Assert.NotNull(tab.MachineRuntimeSettings); // Hidden controls may still evaluate their bindings safely.
        Assert.True(MachineDocumentStorage.TryRead(MachineDocumentStorage.Serialize(tab.GetMachineDocument()), out _, out _));
        Assert.True(tab.CommandService.TryUndo()); Assert.True(tab.IsMachineEmulationRuntime); Assert.Equal(FruitMachinePlatformType.MPU5, tab.MachinePlatform); Assert.False(tab.HasMachineBehavior);
        Assert.True(tab.CommandService.TryRedo()); Assert.False(tab.IsMachineEmulationRuntime);
        tab.MachinePlatform = FruitMachinePlatformType.Impact; Assert.Equal("Oasis", tab.MachineRuntimeKind);
        tab.MachineBehaviorSource = "on machine.started() { timer.stop(\"kept\"); }";
        tab.ConfirmRemoveMachineBehavior = () => false; tab.MachineRuntimeKind = "Emulation";
        Assert.Equal("Oasis", tab.MachineRuntimeKind);
        tab.ConfirmRemoveMachineBehavior = () => true; tab.MachineRuntimeKind = "Emulation";
        Assert.True(tab.IsMachineEmulationRuntime); Assert.False(tab.HasMachineBehavior);
        Assert.True(MachineDocumentStorage.TryRead(MachineDocumentStorage.Serialize(tab.GetMachineDocument()), out _, out _));
        Assert.True(tab.CommandService.TryUndo()); Assert.True(tab.HasMachineBehavior); Assert.Contains("kept", tab.MachineBehaviorSource);
        Assert.True(tab.CommandService.TryRedo()); tab.AddMachineBehaviorCommand.Execute(null); Assert.Equal("Oasis", tab.MachineRuntimeKind); Assert.Contains("kept", tab.MachineBehaviorSource);
    }
    [Fact] public void RuntimeControlsAreScopedToEmulationWhileBehaviorEditorRemainsAvailable()
    {
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var document = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Views", "DocumentEditorView.xaml"));
        var platform = document.Descendants(x + "ComboBox").Single(e => e.Attribute("SelectedItem")?.Value == "{Binding MachinePlatform}");
        Assert.Contains(platform.Ancestors(x + "StackPanel"), panel => panel.Element(x + "StackPanel.Style")?.ToString().Contains("IsMachineEmulationRuntime") == true);
        Assert.Contains(document.Descendants(x + "ComboBox"), e => e.Attribute("ItemsSource")?.Value == "{Binding MachineRuntimeKinds}");
        Assert.Contains(document.Descendants(x + "TextBox"), e => e.Attribute("Text")?.Value.Contains("MachineBehaviorSource") == true);
    }
}
