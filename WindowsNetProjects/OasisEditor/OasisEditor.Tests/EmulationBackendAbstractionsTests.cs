using OasisEditor;
using Xunit;

namespace OasisEditor.Tests;

public sealed class EmulationBackendAbstractionsTests
{
    [Fact]
    public void Capabilities_CanRepresentMameLikeBackend()
    {
        var capabilities = new EmulationBackendCapabilities(
            SupportsPause: true,
            SupportsResume: true,
            SupportsSoftReset: true,
            SupportsHardReset: true,
            SupportsSaveState: true,
            SupportsLoadState: true,
            SupportsThrottle: true,
            SupportsDebugger: true);

        Assert.True(capabilities.SupportsPause);
        Assert.True(capabilities.SupportsResume);
        Assert.True(capabilities.SupportsSoftReset);
        Assert.True(capabilities.SupportsHardReset);
        Assert.True(capabilities.SupportsSaveState);
        Assert.True(capabilities.SupportsLoadState);
        Assert.True(capabilities.SupportsThrottle);
        Assert.True(capabilities.SupportsDebugger);
    }

    [Fact]
    public void LaunchRequest_StoresPlatformAndRomInformation()
    {
        var romPaths = new[] { "rom1.bin", "rom2.bin" };
        var request = new EmulationLaunchRequest(
            FruitMachinePlatformType.Impact,
            "machine-name",
            "rom-root",
            romPaths,
            "-debug");

        Assert.Equal(FruitMachinePlatformType.Impact, request.Platform);
        Assert.Equal("machine-name", request.MachineName);
        Assert.Equal("rom-root", request.RomRootPath);
        Assert.Same(romPaths, request.RomPaths);
        Assert.Equal("-debug", request.AdditionalArguments);
    }

    [Fact]
    public void RuntimeEventArgs_PreserveConstructorValues()
    {
        var lamp = new MachineLampChangedEventArgs(1, 255);
        var reel = new MachineReelChangedEventArgs(2, 96);
        var segment = new MachineSegmentChangedEventArgs(3, 0x7f, MameSegmentOutputType.Digit);
        var vfd = new MachineVfdBrightnessChangedEventArgs(4, 0.5d);
        var dotMatrix = new MachineDotMatrixChangedEventArgs(5, 1);

        Assert.Equal(1, lamp.LampId);
        Assert.Equal(255, lamp.Value);
        Assert.Equal(2, reel.ReelId);
        Assert.Equal(96, reel.Position);
        Assert.Equal(3, segment.CellId);
        Assert.Equal(0x7f, segment.SegmentMask);
        Assert.Equal(MameSegmentOutputType.Digit, segment.OutputType);
        Assert.Equal(4, vfd.CellId);
        Assert.Equal(0.5d, vfd.NormalizedBrightness);
        Assert.Equal(5, dotMatrix.DotIndex);
        Assert.Equal(1, dotMatrix.Value);
    }
}
