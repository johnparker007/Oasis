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
    [Fact]
    public void DeserializingPreferences_WithAutoUpdateFalse_PreservesFalse()
    {
        var json = """
        {
          "Mame": {
            "KeepMameUpToDateAutomatically": false
          }
        }
        """;

        var preferences = JsonSerializer.Deserialize<EditorPreferences>(json);

        Assert.NotNull(preferences);
        Assert.False(preferences!.Mame.KeepMameUpToDateAutomatically);
    }

    [Fact]
    public void DeserializingLegacyPreferences_UsesLocalRomDefaults()
    {
        var json = """
        {
          "Mame": {
            "RomDownloadBaseUrl": "https://example.com/roms/"
          }
        }
        """;

        var preferences = JsonSerializer.Deserialize<EditorPreferences>(json);

        Assert.NotNull(preferences);
        Assert.Equal(string.Empty, preferences!.Mame.LocalRomSourceDirectory);
        Assert.Equal(".zip", preferences.Mame.LocalRomArchiveExtension);
    }

    [Fact]
    public void DeserializingLegacyPreferences_UsesEmptyMfmeFmlImportDirectory()
    {
        var json = """
        {
          "ThemePreference": 2
        }
        """;

        var preferences = JsonSerializer.Deserialize<EditorPreferences>(json);

        Assert.NotNull(preferences);
        Assert.Equal(string.Empty, preferences!.LastMfmeFmlImportDirectory);
    }

    [Fact]
    public void SerializingPreferences_PreservesMfmeFmlImportDirectory()
    {
        var preferences = new EditorPreferences
        {
            LastMfmeFmlImportDirectory = "C:\\\\Layouts"
        };

        var json = JsonSerializer.Serialize(preferences);
        var roundTripped = JsonSerializer.Deserialize<EditorPreferences>(json);

        Assert.NotNull(roundTripped);
        Assert.Equal("C:\\\\Layouts", roundTripped!.LastMfmeFmlImportDirectory);
    }
}
