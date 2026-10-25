using OasisEditor.Automation;
using Xunit;

namespace OasisEditor.Tests;

public sealed class McpMachineDependencyEditingTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "OasisMachineDependencies-" + Guid.NewGuid().ToString("N"));
    private readonly McpEditorSession _session;

    public McpMachineDependencyEditingTests()
    {
        Directory.CreateDirectory(_root);
        _session = new McpEditorSession(_root);
        _session.CreateProject("DependencyTest");
    }

    [Fact]
    public void DeclarePrefabAndGeometrySurfaceAreUndoableNoOpAwareAndPersisted()
    {
        var prefab = _session.CreatePrefab("PoolBall");
        var machine = _session.OpenMachine(Assert.Single(_session.ListMachineAssets()));

        var declared = _session.DeclareMachinePrefab(machine.DocumentId, "poolBall", prefab.Path);
        Assert.True(declared.IsDirty);
        Assert.True(declared.CanUndo);
        Assert.Empty(declared.Machine.ObjectInstances);
        Assert.Equal(AssetReference.Project("Assets/Object3D/PoolBall/asset.object3d"), Assert.Single(declared.Machine.PrefabDependencies).PrefabAsset);
        var noOpDeclaration = _session.DeclareMachinePrefab(machine.DocumentId, "poolBall", prefab.Path);
        Assert.True(noOpDeclaration.IsDirty);
        Assert.True(noOpDeclaration.CanUndo);

        var surface = new MachineGeometrySurface("playingSurface", new(0, 1, 0), MachineVector3.Zero, 1.2, 2.4);
        var updated = _session.SetMachineGeometrySurface(machine.DocumentId, surface);
        Assert.Equal(surface, Assert.Single(updated.Machine.GeometrySurfaces));
        Assert.True(updated.CanUndo);
        var noOpSurface = _session.SetMachineGeometrySurface(machine.DocumentId, surface);
        Assert.Equal(updated.CanUndo, noOpSurface.CanUndo);

        var pool = _session.SetMachineInteraction(machine.DocumentId, "Pool", "poolBall", "playingSurface");
        Assert.Equal(new MachineInteractionDefinition("Pool", "poolBall", "playingSurface"), pool.Machine.Interaction);
        Assert.True(pool.IsDirty);
        Assert.True(pool.CanUndo);
        Assert.Throws<InvalidOperationException>(() => _session.SetMachineInteraction(machine.DocumentId, "Pool", "missing", "playingSurface"));
        Assert.Throws<InvalidOperationException>(() => _session.SetMachineInteraction(machine.DocumentId, "Pool", "poolBall", "missing"));
        Assert.Throws<InvalidOperationException>(() => _session.SetMachineInteraction(machine.DocumentId, "Unknown"));
        Assert.Equal("Buttons", _session.UndoMachine(machine.DocumentId).Machine.Interaction.Kind);
        Assert.Equal("Pool", _session.RedoMachine(machine.DocumentId).Machine.Interaction.Kind);

        var saved = _session.SaveMachine(machine.DocumentId);
        Assert.False(saved.IsDirty);
        var reopenedSession = new McpEditorSession(_root);
        reopenedSession.OpenProject(Path.Combine(_root, "DependencyTest", "DependencyTest.oasisproj"));
        var reopened = reopenedSession.OpenMachine(saved.Path);
        Assert.Equal(surface, Assert.Single(reopened.Machine.GeometrySurfaces));
        Assert.Equal("poolBall", Assert.Single(reopened.Machine.PrefabDependencies).Alias);
        Assert.Equal(new MachineInteractionDefinition("Pool", "poolBall", "playingSurface"), reopened.Machine.Interaction);
    }

    [Fact]
    public void SetBehaviorAndAnchorUndoAndObjectRemovalUseDocumentCommands()
    {
        var path = Assert.Single(_session.ListMachineAssets());
        var machineModel = MachineDocument.Create("DependencyTest") with
        {
            Runtime = new OasisRuntimeDefinition(),
            Behavior = MachineBehaviorDefinition.OasisScript(),
            Anchors = [MachineAnchor.Create("pool_spawn")]
        };
        File.WriteAllText(path, MachineDocumentStorage.Serialize(machineModel));
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(path)!, "behavior.oasis"), "on machine.started() { }");
        var opened = _session.OpenMachine(path);

        var editedSource = _session.SetMachineBehavior(opened.DocumentId, "on machine.started() { state = 1; }");
        Assert.True(editedSource.IsDirty);
        Assert.True(editedSource.CanUndo);
        Assert.Equal("on machine.started() { state = 1; }", editedSource.BehaviorSource);
        Assert.Equal("on machine.started() { }", _session.UndoMachine(opened.DocumentId).BehaviorSource);
        Assert.Equal("on machine.started() { state = 1; }", _session.RedoMachine(opened.DocumentId).BehaviorSource);

        var anchor = new MachineAnchor("pool_spawn", "Pool Spawn", new(0.2, 0.3, 0.4), new(0, 90, 0));
        Assert.Equal(anchor, Assert.Single(_session.SetMachineAnchor(opened.DocumentId, anchor).Machine.Anchors));
        Assert.Throws<InvalidOperationException>(() => _session.SetMachineAnchor(opened.DocumentId,
            anchor with { Position = new(double.NaN, 0, 0) }));
        var addedAnchor = new MachineAnchor("rackOrigin", "Rack Origin", MachineVector3.Zero, MachineVector3.Zero);
        var added = _session.SetMachineAnchor(opened.DocumentId, addedAnchor);
        Assert.Contains(addedAnchor, added.Machine.Anchors);
        Assert.DoesNotContain(addedAnchor, _session.UndoMachine(opened.DocumentId).Machine.Anchors);
        var redone = _session.RedoMachine(opened.DocumentId);
        Assert.Contains(addedAnchor, redone.Machine.Anchors);
        var noOpAnchor = _session.SetMachineAnchor(opened.DocumentId, addedAnchor);
        Assert.True(noOpAnchor.CanUndo);
        Assert.Throws<ArgumentException>(() => _session.SetMachineBehavior(opened.DocumentId, new string('a', 256 * 1024 + 1)));

        var prefab = _session.CreatePrefab("PlacedBall");
        _session.AddMachineObject(opened.DocumentId, "ball01", prefab.Path);
        Assert.Empty(_session.RemoveMachineObject(opened.DocumentId, "ball01").Machine.ObjectInstances);
        Assert.Equal("ball01", Assert.Single(_session.UndoMachine(opened.DocumentId).Machine.ObjectInstances).Id);
    }

    public void Dispose() => Directory.Delete(_root, true);
}
