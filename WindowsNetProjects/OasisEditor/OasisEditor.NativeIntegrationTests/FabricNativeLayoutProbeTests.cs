using Xunit;
using System.Diagnostics;

namespace OasisEditor.NativeIntegrationTests;

public sealed class FabricNativeLayoutProbeTests
{
    [NativeFact("FABRIC_LAYOUT_PROBE_EXE")]
    public void NativeCLayoutsMatchManagedX64Assertions()
    {
        var probe = NativePrerequisites.RequireFile("FABRIC_LAYOUT_PROBE_EXE");
        var process = Process.Start(new ProcessStartInfo(probe)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        })!;
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, $"Native layout probe failed: {error}");

        var values = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split('=', 2))
            .ToDictionary(parts => parts[0], parts => int.Parse(parts[1]), StringComparer.Ordinal);
        var expected = new Dictionary<string, int>
        {
            ["sizeof.FabricLaunchRequest"] = 1208,
            ["sizeof.FabricRomResource"] = 40,
            ["sizeof.FabricCapabilities"] = 48,
            ["sizeof.FabricInput"] = 84,
            ["sizeof.FabricLamp"] = 84,
            ["sizeof.FabricReel"] = 80,
            ["sizeof.FabricCharacterDisplay"] = 164,
            ["sizeof.FabricSegmentDisplay"] = 208,
            ["sizeof.FabricDotMatrixDisplay"] = 2140,
            ["sizeof.FabricMachineSnapshot"] = 96,
            ["sizeof.FabricAudioFormat"] = 20,
            ["sizeof.AmberReelConfigV1"] = 24,
            ["sizeof.AmberReelConfigurationV1"] = 208,
            ["sizeof.AmberCoinChannelConfigV1"] = 20,
            ["sizeof.AmberCoinRouteConfigV1"] = 32,
            ["sizeof.AmberCoinConfigurationV1"] = 408,
            ["sizeof.FabricAmberConfigurationV1"] = 648,
            ["sizeof.FabricAmberMpu5ReelConfigV1"] = 20,
            ["sizeof.FabricAmberMpu5ReelConfigurationV1"] = 176,
            ["sizeof.FabricAmberMpu5CoinChannelConfigV1"] = 20,
            ["sizeof.FabricAmberMpu5CoinConfigurationV1"] = 152,
            ["sizeof.FabricAmberMpu5OptionsV1"] = 60,
            ["sizeof.FabricAmberMpu5ConfigurationV1"] = 404,
            ["offsetof.FabricLaunchRequest.rom_paths"] = 1160,
            ["offsetof.FabricLaunchRequest.machine_configuration"] = 1176,
            ["offsetof.FabricLaunchRequest.rom_resources"] = 1192,
            ["offsetof.FabricRomResource.path"] = 16,
            ["offsetof.FabricMachineSnapshot.lamps"] = 16,
            ["offsetof.FabricMachineSnapshot.reels"] = 32,
            ["offsetof.FabricMachineSnapshot.character_displays"] = 48,
            ["offsetof.FabricMachineSnapshot.segment_displays"] = 64,
            ["offsetof.FabricMachineSnapshot.dot_matrix_displays"] = 80,
            ["offsetof.FabricMachineSnapshot.dot_matrix_display_capacity"] = 88,
            ["offsetof.FabricMachineSnapshot.dot_matrix_display_count"] = 92,
            ["offsetof.FabricCharacterDisplay.brightness"] = 160,
            ["offsetof.FabricAmberMpu5ConfigurationV1.reels"] = 16,
            ["offsetof.FabricAmberMpu5ConfigurationV1.coins"] = 192,
            ["offsetof.FabricAmberMpu5ConfigurationV1.options"] = 344
        };

        Assert.Equal(expected.Count, values.Count);
        foreach (var pair in expected)
            Assert.Equal(pair.Value, values[pair.Key]);
    }
}
