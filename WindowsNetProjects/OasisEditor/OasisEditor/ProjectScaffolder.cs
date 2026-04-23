using System.IO;
using System.Text.Json;

namespace OasisEditor;

public sealed class ProjectScaffolder
{
    private static readonly string[] ProjectFolders =
    [
        "Assets",
        "Assets/Images",
        "Assets/Audio",
        "Assets/Fonts",
        "Assets/Panel2D",
        "Assets/Cabinet3D",
        "Machines",
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
            version = 1,
            layout = new
            {
                assets = "Assets",
                machines = "Machines",
                generated = "Generated"
            }
        };

        var json = JsonSerializer.Serialize(projectMetadata, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        File.WriteAllText(projectFilePath, json);

        return projectDirectory;
    }
}
