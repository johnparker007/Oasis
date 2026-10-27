using System.IO;
using System.Text.Json;

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
        "Assets/Reels",
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
        if (sanitizedName is "." or ".." || Path.IsPathRooted(sanitizedName)
            || sanitizedName.Contains('/') || sanitizedName.Contains('\\')
            || sanitizedName.IndexOfAny("<>:\"|?*".ToCharArray()) >= 0
            || sanitizedName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new ArgumentException("Project name must be a single safe folder name.", nameof(projectName));
        var baseLocation = Path.GetFullPath(rootLocation.Trim());
        var projectDirectory = Path.Combine(baseLocation, sanitizedName);

        if (Directory.Exists(projectDirectory) || File.Exists(projectDirectory))
        {
            throw new InvalidOperationException($"Project folder already exists: {projectDirectory}");
        }

        Directory.CreateDirectory(baseLocation);
        WorkspacePathPolicy.RejectRootReparsePoints(baseLocation, requireExists: true);
        var reservationName = ".oasis-project-" + Guid.NewGuid().ToString("N");
        var reservation = WorkspacePathPolicy.ResolveConfined(baseLocation, reservationName);
        Directory.CreateDirectory(reservation);
        try { Directory.Move(reservation, projectDirectory); }
        catch
        {
            Directory.Delete(WorkspacePathPolicy.ResolveConfined(baseLocation, reservationName), recursive: false);
            if (Directory.Exists(projectDirectory) || File.Exists(projectDirectory))
                throw new InvalidOperationException($"Project folder already exists: {projectDirectory}");
            throw;
        }
        try
        {
            foreach (var folder in ProjectFolders)
                Directory.CreateDirectory(Path.Combine(projectDirectory, folder));

            var projectFilePath = Path.Combine(projectDirectory, $"{sanitizedName}.oasisproj");
            var projectMetadata = new
            {
                name = sanitizedName,
                createdUtc = DateTime.UtcNow,
                version = EditorProject.CurrentSchemaVersion,
                layout = new { assets = "Assets", generated = "Generated" }
            };

            var json = JsonSerializer.Serialize(projectMetadata, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(projectFilePath, json);

            var machineDirectory = Path.Combine(projectDirectory, "Assets", "Machines", sanitizedName);
            Directory.CreateDirectory(machineDirectory);
            File.WriteAllText(Path.Combine(machineDirectory, ProjectAssetPathService.MachineManifestFileName), MachineDocumentStorage.Serialize(MachineDocument.Create(sanitizedName)));

            return projectDirectory;
        }
        catch
        {
            // This directory did not exist before this call. Remove only this confined,
            // newly-owned scaffold if any step failed.
            var relative = Path.GetRelativePath(baseLocation, projectDirectory);
            var confined = WorkspacePathPolicy.ResolveConfined(baseLocation, relative, requireExists: true);
            Directory.Delete(confined, recursive: true);
            throw;
        }

    }
}
