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
                AudioBufferLengthMilliseconds = 73,
                AudioOutputBackend = EmulationAudioOutputBackend.WaveOutEvent,
                EnableAmberFabricAudioDiagnostics = true,
                AmberFabricAudioDiagnosticCaptureDirectory = @"C:\Temp\OasisAudio",
                AmberFabricAudioDiagnosticQueueBlockCapacity = 1024,
                AmberFabricAudioDiagnosticCaptureDurationSeconds = 90
            }
        });
        var restored = System.Text.Json.JsonSerializer.Deserialize<EditorPreferences>(json)!;
        Assert.Equal(@"C:\Fabric\FabricRuntime.dll", restored.NativeEmulation.FabricRuntimeLibraryPath);
        Assert.Equal(@"C:\Amber\ProductionAmber.dll", restored.NativeEmulation.ProductionAmberLibraryPath);
        Assert.Equal(73, restored.NativeEmulation.AudioBufferLengthMilliseconds);
        Assert.Equal(EmulationAudioOutputBackend.WaveOutEvent, restored.NativeEmulation.AudioOutputBackend);
        Assert.True(restored.NativeEmulation.EnableAmberFabricAudioDiagnostics);
        Assert.DoesNotContain("UseFabric", json, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(10, typeof(NativeEmulationPreferences).GetProperties().Length);
        Assert.Equal(7, typeof(EditorPreferences).GetProperties().Length);
    }
}
