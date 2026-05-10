using System.Text.Json;
using Xunit;

namespace OasisEditor.Tests;

public sealed class EditorPreferencesSerializationTests
{
    [Fact]
    public void MamePreferences_Defaults_AutoUpdateToTrue()
    {
        var preferences = new EditorPreferences();

        Assert.True(preferences.Mame.KeepMameUpToDateAutomatically);
    }

    [Fact]
    public void DeserializingLegacyPreferences_UsesAutoUpdateDefaultTrue()
    {
        var json = """
        {
          "ThemePreference": 2,
          "Mame": {
            "Version": "0268",
            "ExecutablePath": "C:\\\\Mame\\\\mame.exe",
            "ReleaseSource": "https://github.com/mamedev/mame/releases",
            "CommandLineOverrides": ""
          },
          "ProjectWindowStates": {}
        }
        """;

        var preferences = JsonSerializer.Deserialize<EditorPreferences>(json);

        Assert.NotNull(preferences);
        Assert.True(preferences!.Mame.KeepMameUpToDateAutomatically);
    }
}
