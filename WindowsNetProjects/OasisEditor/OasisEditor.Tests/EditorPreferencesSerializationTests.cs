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
    public void PlayerPreferences_Defaults_ToWindowed1280By800()
    {
        var preferences = new EditorPreferences();

        Assert.Equal(string.Empty, preferences.Player.ExecutablePath);
        Assert.False(preferences.Player.Fullscreen);
        Assert.Equal(1280, preferences.Player.PreviewWidth);
        Assert.Equal(800, preferences.Player.PreviewHeight);
    }

    [Fact]
    public void PlayerPreferences_RoundTrip_PreservesSettings()
    {
        var preferences = new EditorPreferences
        {
            Player = new OasisPlayerPreferences
            {
                ExecutablePath = "C:\\Oasis Player\\OasisPlayer.exe",
                Fullscreen = true,
                PreviewWidth = 1920,
                PreviewHeight = 1080
            }
        };

        var json = JsonSerializer.Serialize(preferences);
        var roundTripped = JsonSerializer.Deserialize<EditorPreferences>(json);

        Assert.NotNull(roundTripped);
        Assert.Equal("C:\\Oasis Player\\OasisPlayer.exe", roundTripped!.Player.ExecutablePath);
        Assert.True(roundTripped.Player.Fullscreen);
        Assert.Equal(1920, roundTripped.Player.PreviewWidth);
        Assert.Equal(1080, roundTripped.Player.PreviewHeight);
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
