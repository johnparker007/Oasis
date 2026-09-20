using OasisEditor.Features.MachineEditor.Models;

namespace OasisEditor.Tests;

internal static class TestProjectFactory
{
    public static EditorProject Create(string root, string name = "TestProject")
    {
        var projectDir = Path.Combine(root, name);
        var assets = Directory.CreateDirectory(Path.Combine(projectDir, "Assets")).FullName;
        var generated = Directory.CreateDirectory(Path.Combine(projectDir, "Generated")).FullName;
        Directory.CreateDirectory(Path.Combine(assets, "Machines"));
        return new EditorProject
        {
            Name = name,
            ProjectFilePath = Path.Combine(projectDir, $"{name}.oasisproj"),
            ProjectDirectory = projectDir,
            AssetsDirectory = assets,
            GeneratedDirectory = generated
        };
    }

    public static EditorProject CreateDefaultMachine(string root, string name = "TestProject", string machineName = "Test Machine")
    {
        var project = Create(root, name);
        var pathService = new ProjectAssetPathService();
        var package = pathService.CreateAssetPackageDirectory(project, EditorAssetType.Machine, machineName);
        var document = MachineDocumentExtensions.Empty(machineName) with { Title = machineName };
        File.WriteAllText(Path.Combine(package.FullName, ProjectAssetPathService.MachineManifestFileName), MachineDocumentStorage.Serialize(document));
        return project;
    }
}
