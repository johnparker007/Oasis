using System.Text.Json;
using OasisEditor.Features.CabinetEditor.Models;

namespace OasisEditor.Tests;

public sealed class MachineRuntimeBuildServiceTests
{
    [Fact]
    public void BuildFromCabinetDocument_WritesDeterministicVersionedBuildAndCopiesGlb()
    {
        var root = CreateTempRoot();
        var project = CreateProject(root);
        var cabinetDir = Directory.CreateDirectory(Path.Combine(project.AssetsDirectory, "Cabinet3D", "Test Cabinet")).FullName;
        var sourceGlb = Path.Combine(cabinetDir, "source.glb");
        File.WriteAllBytes(sourceGlb, [1, 2, 3]);
        File.WriteAllText(Path.Combine(cabinetDir, ProjectAssetPathService.Cabinet3DManifestFileName), CabinetDocumentStorage.Serialize(new CabinetDocument(1, new CabinetModelReference("source.glb", 2.5, "Z"), [], CabinetPreviewSettings.Default)));
        var stale = Path.Combine(project.GeneratedDirectory, "Builds", "Test Cabinet", "stale.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(stale)!);
        File.WriteAllText(stale, "stale");

        var result = new MachineRuntimeBuildService().BuildFromCabinetDocument(project, Path.Combine(cabinetDir, ProjectAssetPathService.Cabinet3DManifestFileName));

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(Path.Combine(project.GeneratedDirectory, "Builds", "Test Cabinet"), result.BuildRoot);
        Assert.False(File.Exists(stale));
        Assert.Equal([1, 2, 3], File.ReadAllBytes(Path.Combine(result.BuildRoot!, "cabinet", "cabinet.glb")));
        using var machine = JsonDocument.Parse(File.ReadAllText(Path.Combine(result.BuildRoot, "machine.runtime.json")));
        Assert.Equal("oasis.machine.runtime", machine.RootElement.GetProperty("schema").GetString());
        Assert.Equal(1, machine.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("TestProject", machine.RootElement.GetProperty("machineId").GetString());
        Assert.Equal("cabinet/cabinet.runtime.json", machine.RootElement.GetProperty("cabinetManifest").GetString());
        using var cabinet = JsonDocument.Parse(File.ReadAllText(Path.Combine(result.BuildRoot, "cabinet", "cabinet.runtime.json")));
        Assert.Equal("oasis.cabinet.runtime", cabinet.RootElement.GetProperty("schema").GetString());
        Assert.Equal(1, cabinet.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("Test Cabinet", cabinet.RootElement.GetProperty("cabinetId").GetString());
        Assert.Equal("cabinet.glb", cabinet.RootElement.GetProperty("glb").GetString());
        Assert.Equal(2.5, cabinet.RootElement.GetProperty("scale").GetDouble());
        Assert.Equal("Z", cabinet.RootElement.GetProperty("upAxis").GetString());
    }

    [Fact]
    public void BuildFromCabinetDocument_MissingGlb_ReturnsClearFailure()
    {
        var root = CreateTempRoot();
        var project = CreateProject(root);
        var cabinetDir = Directory.CreateDirectory(Path.Combine(project.AssetsDirectory, "Cabinet3D", "Broken Cabinet")).FullName;
        var manifest = Path.Combine(cabinetDir, ProjectAssetPathService.Cabinet3DManifestFileName);
        File.WriteAllText(manifest, CabinetDocumentStorage.Serialize(CabinetDocument.FromModelPath("missing.glb")));

        var result = new MachineRuntimeBuildService().BuildFromCabinetDocument(project, manifest);

        Assert.False(result.Success);
        Assert.Contains("GLB model was not found", result.ErrorMessage);
    }

    private static EditorProject CreateProject(string root)
    {
        var projectDir = Path.Combine(root, "TestProject");
        var assets = Directory.CreateDirectory(Path.Combine(projectDir, "Assets")).FullName;
        var machines = Directory.CreateDirectory(Path.Combine(projectDir, "Machines")).FullName;
        var generated = Directory.CreateDirectory(Path.Combine(projectDir, "Generated")).FullName;
        return new EditorProject { Name = "TestProject", ProjectFilePath = Path.Combine(projectDir, "TestProject.oasisproj"), ProjectDirectory = projectDir, AssetsDirectory = assets, MachinesDirectory = machines, GeneratedDirectory = generated };
    }

    private static string CreateTempRoot() => Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "OasisEditorTests", Guid.NewGuid().ToString("N"))).FullName;
}
