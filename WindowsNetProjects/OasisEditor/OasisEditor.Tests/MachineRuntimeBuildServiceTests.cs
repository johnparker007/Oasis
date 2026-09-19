using OasisEditor.Progress;
using Xunit;

namespace OasisEditor.Tests;

public sealed class MachineRuntimeBuildServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "OasisMachineBuild_" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void BuildFromMachineDocument_MissingCabinetReportsMachineContext()
    {
        var project = Project();
        var machine = MachineDocument.Create("Bonanza") with { CabinetAssetPath = "Assets/Cabinet3D/Missing/asset.cabinet3d" };
        var path = WriteMachine(project, machine);
        var result = new MachineRuntimeBuildService().BuildFromMachineDocument(project, path, NoOpEditorProgressReporter.Instance, CancellationToken.None);
        Assert.False(result.Success);
        Assert.Contains("Bonanza", result.ErrorMessage);
        Assert.Contains("missing Cabinet", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildFromMachineDocument_RequiresCanonicalMachinePackage()
    {
        var project = Project();
        var path = Path.Combine(_root, "loose.machine");
        var machine = MachineDocument.Create("Loose");
        File.WriteAllText(path, MachineDocumentStorage.Serialize(machine));
        var result = new MachineRuntimeBuildService().BuildFromMachineDocument(project, path, machine, NoOpEditorProgressReporter.Instance, CancellationToken.None);
        Assert.False(result.Success);
        Assert.Contains("Assets/Machines", result.ErrorMessage);
    }

    private EditorProject Project()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Assets");
        Directory.CreateDirectory(Path.Combine(_root, "Generated"));
        return new EditorProject { Name = "Workspace", ProjectFilePath = Path.Combine(_root, "Workspace.oasisproj"), ProjectDirectory = _root, AssetsDirectory = Path.Combine(_root, "Assets"), GeneratedDirectory = Path.Combine(_root, "Generated") };
    }

    private static string WriteMachine(EditorProject project, MachineDocument machine)
    {
        var path = new ProjectAssetPathService().GetMachineManifestPath(project, machine.DisplayName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, MachineDocumentStorage.Serialize(machine));
        return path;
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
