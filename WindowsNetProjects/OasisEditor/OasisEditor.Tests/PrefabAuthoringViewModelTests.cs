using Xunit;

namespace OasisEditor.Tests;

public sealed class PrefabAuthoringViewModelTests
{
    [Fact]
    public void ChildRenameAndReorderPreserveNodeIdsAndUndoThroughDocumentCommands()
    {
        using var tab = new DocumentTabViewModel(EditorDocument.CreateObject3DStub("Pool Ball"));
        var authoring = tab.PrefabAuthoring!;
        var root = tab.GetObject3DDocument().Root;

        var firstId = authoring.AddChild(root.Id, "First");
        var secondId = authoring.AddChild(root.Id, "Second");
        Assert.NotNull(firstId);
        Assert.NotNull(secondId);
        Assert.True(authoring.RenameNode(firstId!, "Renamed"));
        Assert.True(authoring.ReorderNode(firstId!, 1));

        var savedRoot = tab.GetObject3DDocument().Root;
        Assert.Equal(new[] { secondId, firstId }, savedRoot.Children.Select(child => child.Id));
        Assert.Equal("Renamed", savedRoot.Children[1].Name);
        Assert.True(tab.CommandService.TryUndo());
        Assert.Equal(new[] { firstId, secondId }, tab.GetObject3DDocument().Root.Children.Select(child => child.Id));
    }

    [Fact]
    public void ReparentRejectsDescendantCycleWithoutDirtyingOrRecordingCommand()
    {
        var root = Object3DDocument.Create("Pool Ball").Root;
        var child = new Object3DNodeDefinition(Guid.NewGuid().ToString("D"), "Child", [Object3DComponentDefinition.CreateTransform()], []);
        var grandchild = new Object3DNodeDefinition(Guid.NewGuid().ToString("D"), "Grandchild", [Object3DComponentDefinition.CreateTransform()], []);
        child = child.WithChildren([grandchild]);
        var document = Object3DDocument.Create("Pool Ball").WithRoot(root.WithChildren([child]));
        using var tab = new DocumentTabViewModel(EditorDocument.CreateObject3DStub("Pool Ball"), object3DDocumentJson: Object3DDocumentStorage.Serialize(document));
        var authoring = tab.PrefabAuthoring!;
        var previous = tab.GetObject3DDocument();

        Assert.False(authoring.ReparentNode(child.Id, grandchild.Id));

        Assert.Same(previous, tab.GetObject3DDocument());
        Assert.False(tab.IsDirty);
        Assert.False(tab.CommandService.CanUndo);
        Assert.Contains("descendants", authoring.Diagnostic, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddChildCreatesTransformOnlyGroupingAndIsUndoable()
    {
        using var tab = new DocumentTabViewModel(EditorDocument.CreateObject3DStub("Pool Ball"));
        var authoring = tab.PrefabAuthoring!;
        var childId = authoring.AddChild(tab.GetObject3DDocument().Root.Id);

        var child = Assert.Single(tab.GetObject3DDocument().Root.Children);
        Assert.Equal(childId, child.Id);
        Assert.Equal("New Node", child.Name);
        Assert.Equal(new[] { Object3DComponentDefinition.TransformKind }, child.Components.Select(component => component.Kind));
        Assert.True(tab.CommandService.TryUndo());
        Assert.Empty(tab.GetObject3DDocument().Root.Children);
    }

    [Fact]
    public void ReorderingToCurrentIndexIsANoOp()
    {
        using var tab = new DocumentTabViewModel(EditorDocument.CreateObject3DStub("Pool Ball"));
        var authoring = tab.PrefabAuthoring!;
        var first = authoring.AddChild(tab.GetObject3DDocument().Root.Id, "First");
        var second = authoring.AddChild(tab.GetObject3DDocument().Root.Id, "Second");
        var historyCount = tab.CommandService.History.Count;

        Assert.False(authoring.ReorderNode(first!, 0));

        Assert.Equal(historyCount, tab.CommandService.History.Count);
        Assert.Equal(new[] { first, second }, tab.GetObject3DDocument().Root.Children.Select(child => child.Id));
    }
}
