using OasisEditor.Automation;
using Xunit;

namespace OasisEditor.Tests;

public sealed class McpPrefabAuthoringTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "OasisPrefabMcp-" + Guid.NewGuid().ToString("N"));
    private readonly McpEditorSession _session;
    public McpPrefabAuthoringTests()
    {
        Directory.CreateDirectory(_root);
        _session = new(_root);
        _session.CreateProject("PrefabTest");
    }

    [Fact]
    public void HierarchyEditUndoSaveReopenPreservesIdentityAndNoOps()
    {
        var original = _session.CreatePrefab("Ball");
        Assert.False(original.IsDirty);
        Assert.False(original.CanBuildSingleRoot);
        var noOp = _session.UpdatePrefabNode(original.DocumentId, original.Prefab.Root.Id, name: original.Prefab.Root.Name);
        Assert.False(noOp.IsDirty);
        Assert.False(noOp.CanUndo);
        var added = _session.AddPrefabChild(original.DocumentId, original.Prefab.Root.Id, "Marker");
        var child = Assert.Single(added.Prefab.Root.Children);
        Assert.True(added.IsDirty);
        Assert.Empty(_session.PrefabHistory(original.DocumentId, false).Prefab.Root.Children);
        Assert.Equal(child.Id, Assert.Single(_session.PrefabHistory(original.DocumentId, true).Prefab.Root.Children).Id);
        var saved = _session.SavePrefab(original.DocumentId);
        Assert.False(saved.IsDirty);
        var reopened = new McpEditorSession(_root);
        reopened.OpenProject(Path.Combine(_root, "PrefabTest", "PrefabTest.oasisproj"));
        var loaded = reopened.OpenPrefabDocument(saved.Path);
        Assert.Equal(original.Prefab.Id, loaded.Prefab.Id);
        Assert.Equal(child.Id, Assert.Single(loaded.Prefab.Root.Children).Id);
    }

    [Fact]
    public void InvalidEditAndExternalFileChangesPreserveSavedPackage()
    {
        var prefab = _session.CreatePrefab("Marker");
        var bytes = File.ReadAllBytes(prefab.Path);
        Assert.Throws<InvalidOperationException>(() => _session.UpdatePrefabNode(prefab.DocumentId, prefab.Prefab.Root.Id,
            transform: new(MachineVector3.Zero, MachineVector3.Zero, new(0, 1, 1))));
        Assert.False(_session.InspectPrefab(prefab.DocumentId).CanUndo);
        Assert.Equal(bytes, File.ReadAllBytes(prefab.Path));
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(prefab.Path)!, "user-note.txt"), "Concurrent work");
        Assert.Throws<IOException>(() => _session.SavePrefab(prefab.DocumentId));
        Assert.Equal(bytes, File.ReadAllBytes(prefab.Path));
    }

    public void Dispose() => Directory.Delete(_root, true);
}
