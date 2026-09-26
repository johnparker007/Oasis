using Xunit;

namespace OasisEditor.Tests;

public sealed class EditorPreferencesSerializationTests
{
    [Fact]
    public void FabricPreferences_RoundTripWithoutLegacyModeSettings()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(new EditorPreferences
        {
            NativeEmulation = new NativeEmulationPreferences
            {
                FabricRuntimeLibraryPath = @"C:\Fabric\FabricRuntime.dll",
                ProductionAmberLibraryPath = @"C:\Amber\ProductionAmber.dll",
                Mpu5AmberLibraryPath = @"C:\Amber\Mpu5.dll",
                EpochAmberLibraryPath = @"C:\Amber\Epoch.dll",
                Mpu3AmberLibraryPath = @"C:\Amber\Mpu3.dll",
                M1AmberLibraryPath = @"C:\Amber\M1.dll",
                Scorpion4AmberLibraryPath = @"C:\Amber\Scorpion4.dll",
                AudioBufferLengthMilliseconds = 73
            }
        });
        var restored = System.Text.Json.JsonSerializer.Deserialize<EditorPreferences>(json)!;
        Assert.Equal(@"C:\Fabric\FabricRuntime.dll", restored.NativeEmulation.FabricRuntimeLibraryPath);
        Assert.Equal(@"C:\Amber\ProductionAmber.dll", restored.NativeEmulation.ProductionAmberLibraryPath);
        Assert.Equal(@"C:\Amber\Epoch.dll", restored.NativeEmulation.EpochAmberLibraryPath);
        Assert.Equal(@"C:\Amber\Mpu3.dll", restored.NativeEmulation.Mpu3AmberLibraryPath);
        Assert.Equal(@"C:\Amber\M1.dll", restored.NativeEmulation.M1AmberLibraryPath);
        Assert.Equal(@"C:\Amber\Scorpion4.dll", restored.NativeEmulation.Scorpion4AmberLibraryPath);
        Assert.Equal(73, restored.NativeEmulation.AudioBufferLengthMilliseconds);
        Assert.DoesNotContain("UseFabric", json, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(8, typeof(NativeEmulationPreferences).GetProperties().Length);
        Assert.Equal(9, typeof(EditorPreferences).GetProperties().Length);
    }

    [Fact]
    public void ProcessingPreferences_DefaultToAutoAndRoundTripCustomWorkers()
    {
        Assert.Equal(CpuImageProcessingMode.Auto, new EditorPreferences().Processing.CpuMode);
        var json = System.Text.Json.JsonSerializer.Serialize(new EditorPreferences
        {
            Processing = new ProcessingPreferences { CpuMode = CpuImageProcessingMode.Custom, CustomMaximumWorkers = 7 }
        });
        var restored = System.Text.Json.JsonSerializer.Deserialize<EditorPreferences>(json)!;
        Assert.Equal(CpuImageProcessingMode.Custom, restored.Processing.CpuMode);
        Assert.Equal(7, restored.Processing.CustomMaximumWorkers);
    }

    [Fact]
    public void WindowPlacementUpdate_PreservesEveryUnrelatedPreferenceAcrossStoreRoundTrip()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var store = new EditorPreferencesStore(Path.Combine(temporaryDirectory.Path, "preferences.json"));
        store.Save(CreateDistinctPreferences());

        store.Update(preferences => preferences with
        {
            ProjectWindowStates = new Dictionary<string, ProjectWindowState>
            {
                [@"C:\Projects\Updated.oasis"] = new()
                {
                    Left = 11,
                    Top = 22,
                    Width = 1333,
                    Height = 777,
                    IsMaximized = true
                }
            }
        });

        var restored = store.Load();
        Assert.Equal(ThemePreference.Light, restored.ThemePreference);
        Assert.Equal(@"C:\Fabric\Runtime.dll", restored.NativeEmulation.FabricRuntimeLibraryPath);
        Assert.False(restored.OutputLog.ShowInfoLogs);
        Assert.Equal("lamp", restored.OutputLog.SearchText);
        Assert.Equal(0.75, restored.FaceGeneration.DefaultPostWarpSharpeningAmount);
        Assert.Equal(CpuImageProcessingMode.Custom, restored.Processing.CpuMode);
        Assert.Equal(6, restored.Processing.CustomMaximumWorkers);
        Assert.Equal(@"C:\Oasis\OasisPlayer.exe", restored.Player.ExecutablePath);
        Assert.Equal(@"C:\Test\LibraryZZZ", restored.AssetLibrary.RootPath);
        Assert.Equal(@"C:\Imports", restored.LastMfmeFmlImportDirectory);
        Assert.True(restored.ProjectWindowStates[@"C:\Projects\Updated.oasis"].IsMaximized);
    }

    [Fact]
    public void NormalPreferenceUpdate_PreservesProjectWindowStates()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var store = new EditorPreferencesStore(Path.Combine(temporaryDirectory.Path, "preferences.json"));
        store.Save(CreateDistinctPreferences());

        store.Update(preferences => preferences with
        {
            AssetLibrary = new AssetLibraryPreferences { RootPath = @"C:\Test\LibraryChanged" }
        });

        var restored = store.Load();
        Assert.Equal(@"C:\Test\LibraryChanged", restored.AssetLibrary.RootPath);
        var state = Assert.Single(restored.ProjectWindowStates).Value;
        Assert.Equal(1200, state.Width);
        Assert.Equal(800, state.Height);
    }

    private static EditorPreferences CreateDistinctPreferences() => new()
    {
        ThemePreference = ThemePreference.Light,
        NativeEmulation = new NativeEmulationPreferences
        {
            FabricRuntimeLibraryPath = @"C:\Fabric\Runtime.dll",
            AudioBufferLengthMilliseconds = 91
        },
        OutputLog = new OutputLogPreferences
        {
            ShowInfoLogs = false,
            ShowWarningLogs = true,
            ShowErrorLogs = false,
            AutoScroll = false,
            SearchText = "lamp"
        },
        FaceGeneration = new FaceGenerationPreferences
        {
            DefaultPostWarpSharpeningEnabled = true,
            DefaultPostWarpSharpeningAmount = 0.75
        },
        Processing = new ProcessingPreferences
        {
            CpuMode = CpuImageProcessingMode.Custom,
            CustomMaximumWorkers = 6
        },
        Player = new OasisPlayerPreferences
        {
            ExecutablePath = @"C:\Oasis\OasisPlayer.exe",
            Fullscreen = true,
            PreviewWidth = 1600,
            PreviewHeight = 900
        },
        AssetLibrary = new AssetLibraryPreferences { RootPath = @"C:\Test\LibraryZZZ" },
        LastMfmeFmlImportDirectory = @"C:\Imports",
        ProjectWindowStates = new Dictionary<string, ProjectWindowState>
        {
            [@"C:\Projects\Original.oasis"] = new() { Width = 1200, Height = 800 }
        }
    };

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"oasis-preferences-test-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
