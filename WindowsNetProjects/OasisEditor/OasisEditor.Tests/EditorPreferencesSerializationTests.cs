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
                AudioBufferLengthMilliseconds = 73
            }
        });
        var restored = System.Text.Json.JsonSerializer.Deserialize<EditorPreferences>(json)!;
        Assert.Equal(@"C:\Fabric\FabricRuntime.dll", restored.NativeEmulation.FabricRuntimeLibraryPath);
        Assert.Equal(@"C:\Amber\ProductionAmber.dll", restored.NativeEmulation.ProductionAmberLibraryPath);
        Assert.Equal(@"C:\Amber\Epoch.dll", restored.NativeEmulation.EpochAmberLibraryPath);
        Assert.Equal(73, restored.NativeEmulation.AudioBufferLengthMilliseconds);
        Assert.DoesNotContain("UseFabric", json, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(5, typeof(NativeEmulationPreferences).GetProperties().Length);
        Assert.Equal(7, typeof(EditorPreferences).GetProperties().Length);
    }
}
