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
        Assert.Equal(7, typeof(EditorPreferences).GetProperties().Length);
    }
}
