using System.IO;
using System.Text.Json;

namespace OasisEditor;

public sealed class EditorPreferencesStore
{
    private readonly string _storageFilePath;

    public EditorPreferencesStore()
        : this(GetDefaultStorageFilePath())
    {
    }

    public EditorPreferencesStore(string storageFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageFilePath);
        _storageFilePath = storageFilePath;
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

    public void Update(Func<EditorPreferences, EditorPreferences> update)
    {
        ArgumentNullException.ThrowIfNull(update);
        Save(update(Load()));
    }

    private static string GetDefaultStorageFilePath()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appDataPath, "OasisEditor", "editor-preferences.json");
    }
}
