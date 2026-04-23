using System.IO;
using System.Text.Json;

namespace OasisEditor;

public sealed class RecentProjectsStore
{
    private const int MaxRecentProjects = 10;
    private readonly string _storageFilePath;

    public RecentProjectsStore()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var oasisFolder = Path.Combine(appDataPath, "OasisEditor");
        _storageFilePath = Path.Combine(oasisFolder, "recent-projects.json");
    }

    public IReadOnlyList<string> Load()
    {
        try
        {
            if (!File.Exists(_storageFilePath))
            {
                return [];
            }

            var json = File.ReadAllText(_storageFilePath);
            var items = JsonSerializer.Deserialize<List<string>>(json) ?? [];

            return items
                .Where(static path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(MaxRecentProjects)
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    public IReadOnlyList<string> Add(string projectFilePath)
    {
        if (string.IsNullOrWhiteSpace(projectFilePath))
        {
            throw new ArgumentException("Project file path is required.", nameof(projectFilePath));
        }

        var normalizedPath = Path.GetFullPath(projectFilePath.Trim());
        var items = Load().ToList();

        items.RemoveAll(existing => string.Equals(existing, normalizedPath, StringComparison.OrdinalIgnoreCase));
        items.Insert(0, normalizedPath);

        if (items.Count > MaxRecentProjects)
        {
            items = items.Take(MaxRecentProjects).ToList();
        }

        var folder = Path.GetDirectoryName(_storageFilePath);
        if (!string.IsNullOrWhiteSpace(folder))
        {
            Directory.CreateDirectory(folder);
        }

        var json = JsonSerializer.Serialize(items, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        File.WriteAllText(_storageFilePath, json);
        return items;
    }
}
