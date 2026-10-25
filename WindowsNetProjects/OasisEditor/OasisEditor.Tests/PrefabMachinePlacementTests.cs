using Xunit;

namespace OasisEditor.Tests;

public sealed class PrefabMachinePlacementTests
{
    private static MachineObject3DInstance Instance(string id, string? parent = null) => new(id, id,
        AssetReference.Project("Assets/Object3D/Ball/asset.object3d"), MachineObjectTransform.Identity, parent);

    [Fact]
    public void ParentGraphRejectsMissingAndCyclicReferences()
    {
        var machine = MachineDocument.Create("Pool") with { ObjectInstances = [Instance("child", "missing")] };
        Assert.Contains("missing", Assert.Throws<InvalidOperationException>(() => MachineDocumentStorage.Serialize(machine)).Message);
        machine = machine with { ObjectInstances = [Instance("one", "two"), Instance("two", "one")] };
        Assert.Contains("cycle", Assert.Throws<InvalidOperationException>(() => MachineDocumentStorage.Serialize(machine)).Message);
    }

    [Fact]
    public void RenameAndSubtreeRemovalPreserveParentLinksAcrossUndo()
    {
        var machine = MachineDocument.Create("Pool") with { ObjectInstances = [Instance("parent"), Instance("child", "parent")] };
        using var tab = new DocumentTabViewModel(EditorDocument.CreateMachineStub("Pool"), machineDocumentJson: MachineDocumentStorage.Serialize(machine));
        var parent = tab.MachineObjectInstanceRows.Single(i => i.Id == "parent");
        parent.Id = "renamed";
        Assert.Equal("renamed", tab.GetMachineDocument().ObjectInstances.Single(i => i.Id == "child").ParentId);
        Assert.True(tab.CommandService.TryUndo());
        Assert.Equal("parent", tab.GetMachineDocument().ObjectInstances.Single(i => i.Id == "child").ParentId);
        var before = tab.GetMachineDocument();
        tab.MachineObjectInstanceRows.Single(i => i.Id == "parent").ParentId = "child";
        Assert.Equal(before, tab.GetMachineDocument());
        tab.MachineObjectInstanceRows.Single(i => i.Id == "parent").RemoveCommand.Execute(null);
        Assert.Empty(tab.GetMachineDocument().ObjectInstances);
        Assert.True(tab.CommandService.TryUndo());
        Assert.Equal(2, tab.GetMachineDocument().ObjectInstances.Length);
        Assert.Equal("parent", tab.GetMachineDocument().ObjectInstances.Single(i => i.Id == "child").ParentId);
    }
}
