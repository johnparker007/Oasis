using System.IO;
using System.Text.Json;

namespace OasisEditor;

public sealed class EditorPreferencesStore
{
    private readonly string _storageFilePath;

    public EditorPreferencesStore()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var oasisFolder = Path.Combine(appDataPath, "OasisEditor");
        _storageFilePath = Path.Combine(oasisFolder, "editor-preferences.json");
    }

    public EditorPreferences Load()
    {
        try
        {
            if (!File.Exists(_storageFilePath))
            {
                return new EditorPreferences();
            }

            var json = File.ReadAllText(_storageFilePath);
            return JsonSerializer.Deserialize<EditorPreferences>(json) ?? new EditorPreferences();
        }
        catch
        {
            return new EditorPreferences();
        }
    }

    public void Save(EditorPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);

        var folder = Path.GetDirectoryName(_storageFilePath);
        if (!string.IsNullOrWhiteSpace(folder))
        {
            Directory.CreateDirectory(folder);
        }

        var json = JsonSerializer.Serialize(preferences, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        File.WriteAllText(_storageFilePath, json);
    }
}
