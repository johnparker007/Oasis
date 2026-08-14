using OasisEditor;
using Xunit;

namespace OasisEditor.Tests;

public sealed class EmulationBackendAbstractionsTests
{
    [Fact]
    public void Capabilities_RepresentBackendFeatures()
    {
        var capabilities = new EmulationBackendCapabilities(
            SupportsPause: true,
            SupportsResume: true,
            SupportsSoftReset: true,
            SupportsHardReset: true,
            SupportsSaveState: true,
            SupportsLoadState: true,
            SupportsThrottle: true);

        Assert.True(capabilities.SupportsPause);
        Assert.True(capabilities.SupportsResume);
        Assert.True(capabilities.SupportsSoftReset);
        Assert.True(capabilities.SupportsHardReset);
        Assert.True(capabilities.SupportsSaveState);
        Assert.True(capabilities.SupportsLoadState);
        Assert.True(capabilities.SupportsThrottle);
    }

    [Fact]
    public void LaunchRequest_StoresFabricSystem6Configuration()
    {
        var settings = new System6NativeRomSettings { ProgramRom1Path = "rom1.bin" };
        var request = new EmulationLaunchRequest(settings, [1, 2], [3]);
        Assert.Same(settings, request.System6Configuration);
        Assert.Equal([1, 2], request.ConfiguredLampIds);
        Assert.Equal([3], request.ConfiguredSevenSegmentDisplayIds);
    }

    [Fact]
    public void LaunchRequest_StoresSeparateMpu5Configuration()
    {
        var settings = new Mpu5NativeRomSettings();
        var request = EmulationLaunchRequest.ForMpu5(settings);
        Assert.Equal(FruitMachinePlatformType.MPU5, request.Platform);
        Assert.Same(settings, request.Mpu5Configuration);
        Assert.Null(request.System6Configuration);
    }

    [Fact]
    public void Mpu5ReelContractHasApplyButNoInventedEnabledSetter()
    {
        Assert.NotNull(typeof(Mpu5ReelSettings).GetProperty(nameof(Mpu5ReelSettings.Apply)));
        Assert.Null(typeof(Mpu5ReelSettings).GetProperty("Enabled"));
        var reels = new Mpu5NativeRomSettings().Reels;
        Assert.Equal(8, reels.Count);
        Assert.All(reels, reel =>
        {
            Assert.Equal(0, reel.OptoStart);
            Assert.Equal(2, reel.OptoEnd);
        });
        Assert.Equal(6, new Mpu5NativeRomSettings().Coins.Count);
    }

    [Fact]
    public void System6ReelsRetainTheirPlatformSpecificOptoDefaults()
    {
        var reels = new System6NativeRomSettings().ReelOptos;

        Assert.All(reels, reel =>
        {
            Assert.Equal(5, reel.OptoStart);
            Assert.Equal(7, reel.OptoEnd);
        });
    }

    [Fact]
    public void RuntimeEventArgs_PreserveConstructorValues()
    {
        var lamp = new MachineLampChangedEventArgs(1, 255);
        var reel = new MachineReelChangedEventArgs(2, 96);
        var segment = new MachineSegmentChangedEventArgs(3, 0x7f, SegmentOutputType.Digit);
        var vfd = new MachineVfdBrightnessChangedEventArgs(4, 0.5d);
        var dotMatrix = new MachineDotMatrixChangedEventArgs(5, 96, 8, [0, 1], 0.75d);

        Assert.Equal(1, lamp.LampId);
        Assert.Equal(255, lamp.Value);
        Assert.Equal(2, reel.ReelId);
        Assert.Equal(96, reel.Position);
        Assert.Equal(3, segment.CellId);
        Assert.Equal(0x7f, segment.SegmentMask);
        Assert.Equal(SegmentOutputType.Digit, segment.OutputType);
        Assert.Equal(4, vfd.CellId);
        Assert.Equal(0.5d, vfd.NormalizedBrightness);
        Assert.Equal(5, dotMatrix.DisplayId);
        Assert.Equal(96, dotMatrix.Width);
        Assert.Equal(8, dotMatrix.Height);
        Assert.Equal([0, 1], dotMatrix.Dots);
        Assert.Equal(0.75d, dotMatrix.Brightness);
    }
}
