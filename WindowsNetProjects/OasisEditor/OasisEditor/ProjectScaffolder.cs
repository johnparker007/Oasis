using System.IO;
using System.Text.Json;
using OasisEditor.Features.MachineEditor.Models;

namespace OasisEditor;

public sealed class ProjectScaffolder
{
    private static readonly string[] ProjectFolders =
    [
        "Assets",
        "Assets/Panel2D",
        "Assets/Cabinet3D",
        "Assets/Faces",
        "Assets/Machines",
        "Generated",
        "Generated/Build",
        "Generated/Preview"
    ];

    public string CreateProject(string projectName, string rootLocation)
    {
        if (string.IsNullOrWhiteSpace(projectName))
        {
            throw new ArgumentException("Project name is required.", nameof(projectName));
        }

        if (string.IsNullOrWhiteSpace(rootLocation))
        {
            throw new ArgumentException("Project location is required.", nameof(rootLocation));
        }

        var sanitizedName = projectName.Trim();
        var baseLocation = Path.GetFullPath(rootLocation.Trim());
        var projectDirectory = Path.Combine(baseLocation, sanitizedName);

        if (Directory.Exists(projectDirectory))
        {
            throw new InvalidOperationException($"Project folder already exists: {projectDirectory}");
        }

        Directory.CreateDirectory(projectDirectory);

        foreach (var folder in ProjectFolders)
        {
            Directory.CreateDirectory(Path.Combine(projectDirectory, folder));
        }

        var projectFilePath = Path.Combine(projectDirectory, $"{sanitizedName}.oasisproj");
        var projectMetadata = new
        {
            name = sanitizedName,
            createdUtc = DateTime.UtcNow,
            version = EditorProject.CurrentSchemaVersion,
            layout = new
            {
                assets = "Assets",
                generated = "Generated"
            }
        };

        var json = JsonSerializer.Serialize(projectMetadata, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        File.WriteAllText(projectFilePath, json);

        var pathService = new ProjectAssetPathService();
        var machineAssetName = pathService.EnsureUniqueAssetName(
            new EditorProject
            {
                Name = sanitizedName,
                ProjectFilePath = projectFilePath,
                ProjectDirectory = projectDirectory,
                AssetsDirectory = Path.Combine(projectDirectory, "Assets"),
                GeneratedDirectory = Path.Combine(projectDirectory, "Generated")
            },
            EditorAssetType.Machine,
            sanitizedName);
        var machinePackage = pathService.CreateAssetPackageDirectory(
            new EditorProject
            {
                Name = sanitizedName,
                ProjectFilePath = projectFilePath,
                ProjectDirectory = projectDirectory,
                AssetsDirectory = Path.Combine(projectDirectory, "Assets"),
                GeneratedDirectory = Path.Combine(projectDirectory, "Generated")
            },
            EditorAssetType.Machine,
            machineAssetName);
        var machineDocument = MachineDocumentExtensions.Empty(machineAssetName) with { Title = machineAssetName };
        File.WriteAllText(Path.Combine(machinePackage.FullName, ProjectAssetPathService.MachineManifestFileName), MachineDocumentStorage.Serialize(machineDocument));

        return projectDirectory;
    }
}
