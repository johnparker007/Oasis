using OasisEditor.Automation;
using Xunit;

namespace OasisEditor.Tests;

public sealed class McpMachineInspectionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "OasisMcpMachine-" + Guid.NewGuid().ToString("N"));
    private readonly McpEditorSession _session;
    public McpMachineInspectionTests()
    {
        Directory.CreateDirectory(_root);
        _session = new McpEditorSession(_root);
        _session.CreateProject("Probe");
    }

    [Fact]
    public void ListsAndOpensDefaultMachineWithoutWritingIt()
    {
        var path = Assert.Single(_session.ListMachineAssets());
        var before = File.ReadAllBytes(path);
        var machine = _session.OpenMachine(path);
        Assert.Equal("Probe", machine.Machine.DisplayName);
        Assert.Empty(machine.Machine.ObjectInstances);
        Assert.Equal(before, File.ReadAllBytes(path));
        Assert.Equal(machine.Machine.Id, _session.InspectMachine(machine.DocumentId).Machine.Id);
        Assert.Equal(machine.DocumentId, Assert.Single(_session.InspectProject().OpenMachines).DocumentId);
        Assert.Throws<InvalidOperationException>(() => _session.OpenMachine(path));
    }

    [Fact]
    public void BuildReportsMissingCabinetAndRefusesExternalManifestChanges()
    {
        var machine = _session.OpenMachine(Assert.Single(_session.ListMachineAssets()));
        var result = _session.BuildMachine(machine.DocumentId, default);
        Assert.False(result.Success);
        Assert.Contains("Cabinet", result.ErrorMessage);
        File.AppendAllText(machine.Path, " ");
        Assert.Throws<IOException>(() => _session.BuildMachine(machine.DocumentId, default));
    }

    [Fact]
    public void RejectsPathsOutsideWorkspaceAndUnknownMachineIds()
    {
        Assert.Throws<UnauthorizedAccessException>(() => _session.OpenMachine("../outside/asset.machine"));
        Assert.Throws<ArgumentException>(() => _session.InspectMachine(Guid.NewGuid()));
    }

    [Fact]
    public void RejectsExternalAssetPathsBeforeOpeningMachinePreview()
    {
        var path = Assert.Single(_session.ListMachineAssets());
        var package = Path.GetDirectoryName(path)!;
        File.WriteAllText(Path.Combine(package, "asset.invalid"), "{\"modelPath\":\"../../../../../outside.glb\"}");
        Assert.Throws<UnauthorizedAccessException>(() => _session.OpenMachine(path));
    }

    [Fact]
    public void AnchorEditUsesUndoableCommandAndNoOpDoesNotDirtyDocument()
    {
        var path = ConfigureMachineWithAnchor();
        var opened = _session.OpenMachine(path);
        var anchor = Assert.Single(opened.Machine.Anchors);
        Assert.False(opened.IsDirty);

        var noOp = _session.UpdateMachineAnchor(opened.DocumentId, anchor.Id, anchor.Position);
        Assert.False(noOp.IsDirty);
        Assert.False(noOp.CanUndo);

        var edited = _session.UpdateMachineAnchor(opened.DocumentId, anchor.Id, new MachineVector3(1.25, -2, 3));
        Assert.True(edited.IsDirty);
        Assert.True(edited.CanUndo);
        Assert.Equal(new MachineVector3(1.25, -2, 3), Assert.Single(edited.Machine.Anchors).Position);

        var undone = _session.UndoMachine(opened.DocumentId);
        Assert.Equal(anchor.Position, Assert.Single(undone.Machine.Anchors).Position);
        Assert.True(undone.CanRedo);
        var redone = _session.RedoMachine(opened.DocumentId);
        Assert.Equal(new MachineVector3(1.25, -2, 3), Assert.Single(redone.Machine.Anchors).Position);
    }

    [Fact]
    public void SaveMachinePersistsAnchorAndBehaviorForReopen()
    {
        var path = ConfigureMachineWithAnchor();
        var opened = _session.OpenMachine(path);
        _session.UpdateMachineAnchor(opened.DocumentId, "pool_spawn", new MachineVector3(0.5, 1.25, 0));

        var saved = _session.SaveMachine(opened.DocumentId);
        Assert.False(saved.IsDirty);
        Assert.True(saved.CanUndo);
        Assert.Equal("// original behavior\n", File.ReadAllText(Path.Combine(Path.GetDirectoryName(path)!, "behavior.oasis")));

        var reopenedSession = new McpEditorSession(_root);
        reopenedSession.OpenProject(Path.Combine(_root, "Probe", "Probe.oasisproj"));
        var reopened = reopenedSession.OpenMachine(path);
        Assert.Equal(new MachineVector3(0.5, 1.25, 0), Assert.Single(reopened.Machine.Anchors).Position);
        Assert.Equal("// original behavior\n", reopened.BehaviorSource);
    }

    [Fact]
    public void SaveMachineRefusesExternalManifestOrBehaviorChanges()
    {
        var manifestPath = ConfigureMachineWithAnchor();
        var opened = _session.OpenMachine(manifestPath);
        _session.UpdateMachineAnchor(opened.DocumentId, "pool_spawn", new MachineVector3(1, 0, 0));
        File.AppendAllText(manifestPath, " ");
        Assert.Throws<IOException>(() => _session.SaveMachine(opened.DocumentId));

        // Use a fresh document/session baseline for the sidecar case.
        var sidecarPath = ConfigureMachineWithAnchor("Secondary");
        var freshSession = new McpEditorSession(_root);
        freshSession.OpenProject(Path.Combine(_root, "Probe", "Probe.oasisproj"));
        var sidecarOpened = freshSession.OpenMachine(sidecarPath);
        freshSession.UpdateMachineAnchor(sidecarOpened.DocumentId, "pool_spawn", new MachineVector3(1, 0, 0));
        File.AppendAllText(Path.Combine(Path.GetDirectoryName(sidecarPath)!, "behavior.oasis"), "// external\n");
        Assert.Throws<IOException>(() => freshSession.SaveMachine(sidecarOpened.DocumentId));
    }

    [Fact]
    public void BuildMachineRefusesUnsavedEditsAndExternalBehaviorChanges()
    {
        var path = ConfigureMachineWithAnchor();
        var opened = _session.OpenMachine(path);
        _session.UpdateMachineAnchor(opened.DocumentId, "pool_spawn", new MachineVector3(1, 0, 0));
        Assert.Throws<InvalidOperationException>(() => _session.BuildMachine(opened.DocumentId, default));

        _session.SaveMachine(opened.DocumentId);
        File.AppendAllText(Path.Combine(Path.GetDirectoryName(path)!, "behavior.oasis"), "// external\n");
        Assert.Throws<IOException>(() => _session.BuildMachine(opened.DocumentId, default));
    }

    private string ConfigureMachineWithAnchor(string? machineName = null)
    {
        var path = machineName is null
            ? Assert.Single(_session.ListMachineAssets())
            : CreateAdditionalMachine(machineName);
        var machine = MachineDocument.Create(Path.GetFileName(Path.GetDirectoryName(path)!)) with
        {
            Anchors = [MachineAnchor.Create("pool_spawn", "Pool Spawn")],
            Runtime = new OasisRuntimeDefinition(),
            Behavior = MachineBehaviorDefinition.OasisScript()
        };
        File.WriteAllText(path, MachineDocumentStorage.Serialize(machine));
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(path)!, "behavior.oasis"), "// original behavior\n");
        return path;
    }

    private string CreateAdditionalMachine(string name)
    {
        var source = Assert.Single(_session.ListMachineAssets());
        var target = Path.Combine(Path.GetDirectoryName(source)!, "..", name, "asset.machine");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Copy(source, target);
        return Path.GetFullPath(target);
    }

    public void Dispose() => Directory.Delete(_root, true);
}
