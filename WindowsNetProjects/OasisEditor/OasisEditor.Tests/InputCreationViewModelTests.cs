using System.IO;
using System.Text.Json;
using OasisEditor.Automation;
using Xunit;

namespace OasisEditor.Tests;

public sealed class InputCreationViewModelTests
{
    [Fact]
    public void EmptyOasisMachine_ExplicitIdsPersistAndRevalidateThroughUndoRedo()
    {
        var root = Path.Combine(Path.GetTempPath(), "oasis-add-input-" + Guid.NewGuid().ToString("N"));
        try
        {
            var tab = Machine();
            tab.MachineBehaviorSource = "on input.pressed(input:rerack) { } on input.pressed(input:newGame) { }";
            Assert.Equal(2, tab.MachineBehaviorDiagnostics.Count(d => d.Code == "OSM3004"));
            var path = Path.Combine(root, "asset.machine");
            new DocumentSaveService().SaveDocument(tab, path).ApplyTo(tab);
            Assert.False(tab.IsDirty);
            var closed = 0;
            var form = new InputCreationViewModel(tab, () => tab, () => closed++) { Id = "rerack", DisplayName = "Rerack balls" };
            Assert.True(form.TryCreate());
            Assert.Equal(1, closed);
            Assert.True(tab.IsDirty);
            var input = Assert.Single(tab.GetMachineDocument().InputDefinitions);
            Assert.Equal("rerack", input.Id);
            Assert.Equal("Rerack balls", input.Name);
            Assert.Equal(InputDefinitionKind.Button, input.Kind);
            Assert.Equal(string.Empty, input.ButtonNumber);
            Assert.False(input.CoinInput);
            Assert.Null(input.CoinChannel);
            Assert.Null(input.CoinValue);
            Assert.Null(input.LinkedVisualElementId);
            Assert.Equal(string.Empty, input.RawMfmeShortcut);
            Assert.Equal(string.Empty, input.KeyboardShortcut);
            Assert.DoesNotContain(tab.MachineBehaviorDiagnostics, d => d.Message.Contains("'rerack'"));
            Assert.Contains(tab.MachineBehaviorDiagnostics, d => d.Code == "OSM3004" && d.Message.Contains("'newGame'"));
            Assert.True(tab.CommandService.TryUndo());
            Assert.Empty(tab.GetMachineDocument().InputDefinitions);
            Assert.Equal(2, tab.MachineBehaviorDiagnostics.Count(d => d.Code == "OSM3004"));
            Assert.True(tab.CommandService.TryRedo());
            Assert.Single(tab.MachineBehaviorDiagnostics);
            Assert.True(new InputCreationViewModel(tab, () => tab, () => { }) { Id = "newGame" }.TryCreate());
            Assert.Empty(tab.MachineBehaviorDiagnostics);
            new DocumentSaveService().SaveDocument(tab, path).ApplyTo(tab);
            var reopened = new DocumentTabViewModel(EditorDocument.CreateFromFile(path, "Machine"), machineDocumentJson: File.ReadAllText(path));
            Assert.Equal(new[] { "rerack", "newGame" }, reopened.GetMachineDocument().InputDefinitions.Select(i => i.Id));
            Assert.Empty(reopened.MachineBehaviorDiagnostics);
            Assert.False(tab.IsDirty);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData("", "Enter")]
    [InlineData("   ", "Enter")]
    [InlineData("input:rerack", "prefix")]
    [InlineData(" rerack", "ASCII")]
    [InlineData("re rack", "ASCII")]
    [InlineData("ré rack", "ASCII")]
    [InlineData("rerack", "already exists")]
    public void InvalidOrDuplicateIdDoesNotMutateOrRecordCommand(string id, string message)
    {
        var imported = new InputDefinitionModel { Id = "rerack", Name = "Imported", ButtonNumber = "17", RawMfmeShortcut = "A", Kind = InputDefinitionKind.Switch };
        var tab = Machine([imported]);
        // Machine() serializes and reloads the fixture, producing new input instances.
        var loadedInput = Assert.Single(tab.GetMachineDocument().InputDefinitions);
        var importedData = JsonSerializer.Serialize(imported);
        Assert.Equal(importedData, JsonSerializer.Serialize(loadedInput));
        var before = tab.GetMachineDocument();
        var form = new InputCreationViewModel(tab, () => tab, () => throw new Exception("Must remain open")) { Id = id };
        Assert.False(form.TryCreate());
        Assert.Contains(message, form.Error);
        Assert.Same(before, tab.GetMachineDocument());
        Assert.False(tab.IsDirty);
        Assert.False(tab.CommandService.TryUndo());
        Assert.Same(loadedInput, Assert.Single(tab.GetMachineDocument().InputDefinitions));
        Assert.Equal(importedData, JsonSerializer.Serialize(loadedInput));
    }

    [Fact]
    public void CaseSensitiveIdsAndImportedDataArePreserved()
    {
        var imported = new InputDefinitionModel { Id = "rerack", Name = "Imported", ButtonNumber = "17", CoinChannel = 2, CoinValue = 3, LinkedVisualElementId = Guid.NewGuid(), RawMfmeShortcut = "A" };
        var tab = Machine([imported]);
        // Machine() serializes and reloads the fixture, producing new input instances.
        var loadedInput = Assert.Single(tab.GetMachineDocument().InputDefinitions);
        var importedData = JsonSerializer.Serialize(imported);
        Assert.Equal(importedData, JsonSerializer.Serialize(loadedInput));
        Assert.True(new InputCreationViewModel(tab, () => tab, () => { }) { Id = "Rerack_2-1" }.TryCreate());
        Assert.True(new InputCreationViewModel(tab, () => tab, () => { }) { Id = "Rerack" }.TryCreate());
        Assert.Same(loadedInput, tab.GetMachineDocument().InputDefinitions[0]);
        Assert.Equal(importedData, JsonSerializer.Serialize(loadedInput));
        Assert.Equal("Rerack", tab.GetMachineDocument().InputDefinitions[2].Name);
        Assert.Equal("17", loadedInput.ButtonNumber);
        Assert.True(tab.CommandService.TryUndo());
        Assert.Equal(2, tab.GetMachineDocument().InputDefinitions.Count);
        Assert.True(tab.CommandService.TryUndo());
        Assert.Same(loadedInput, Assert.Single(tab.GetMachineDocument().InputDefinitions));
        Assert.Equal(importedData, JsonSerializer.Serialize(loadedInput));
    }

    [Fact]
    public void CancellationAndChangedOrMissingContextCannotAddToEitherDocument()
    {
        var target = Machine();
        var other = Machine();
        DocumentTabViewModel? active = other;
        var closed = 0;
        var form = new InputCreationViewModel(target, () => active, () => closed++) { Id = "rerack" };
        Assert.False(form.TryCreate());
        Assert.Contains("active Machine changed", form.Error);
        active = null;
        Assert.False(form.TryCreate());
        active = target;
        form.CancelCommand.Execute(null);
        Assert.False(form.TryCreate());
        Assert.Equal(1, closed);
        Assert.Empty(target.GetMachineDocument().InputDefinitions);
        Assert.Empty(other.GetMachineDocument().InputDefinitions);
        Assert.False(target.IsDirty);
        Assert.False(other.IsDirty);
        Assert.False(target.CommandService.TryUndo());
        Assert.False(other.CommandService.TryUndo());

        var next = new InputCreationViewModel(other, () => other, () => { }) { Id = "newGame" };
        Assert.True(next.TryCreate());
        Assert.Empty(target.GetMachineDocument().InputDefinitions);
        Assert.Equal("newGame", Assert.Single(other.GetMachineDocument().InputDefinitions).Id);
    }

    private static DocumentTabViewModel Machine(List<InputDefinitionModel>? inputs = null)
    {
        var machine = MachineDocument.Create("Machine") with
        {
            Runtime = new OasisRuntimeDefinition(), Behavior = MachineBehaviorDefinition.OasisScript(),
            InputDefinitions = inputs ?? []
        };
        return new DocumentTabViewModel(EditorDocument.CreateMachineStub("Machine"), machineDocumentJson: MachineDocumentStorage.Serialize(machine));
    }
}
