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

    public void Dispose() => Directory.Delete(_root, true);
}
