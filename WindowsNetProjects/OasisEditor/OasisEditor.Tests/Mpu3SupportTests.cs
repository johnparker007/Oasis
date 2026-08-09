using System.Runtime.InteropServices;
using System.Text.Json;
using Xunit;

namespace OasisEditor.Tests;

public sealed class Mpu3SupportTests
{
    [Fact]
    public void ManagedConfigurationMatchesFabricAbiExactly()
    {
        Assert.True(Enum.IsDefined(FruitMachinePlatformType.MPU3));
        Assert.Equal(4, Marshal.SizeOf<FabricAmberMpu3ReelConfig>());
        Assert.Equal(0, Marshal.OffsetOf<FabricAmberMpu3ReelConfig>(nameof(FabricAmberMpu3ReelConfig.Steps)).ToInt32());
        Assert.Equal(1, Marshal.OffsetOf<FabricAmberMpu3ReelConfig>(nameof(FabricAmberMpu3ReelConfig.OptoStart)).ToInt32());
        Assert.Equal(2, Marshal.OffsetOf<FabricAmberMpu3ReelConfig>(nameof(FabricAmberMpu3ReelConfig.OptoEnd)).ToInt32());
        Assert.Equal(3, Marshal.OffsetOf<FabricAmberMpu3ReelConfig>(nameof(FabricAmberMpu3ReelConfig.OptoInvert)).ToInt32());
        Assert.Equal(48, Marshal.SizeOf<FabricAmberMpu3Config>());
        Assert.Equal(0, Marshal.OffsetOf<FabricAmberMpu3Config>(nameof(FabricAmberMpu3Config.Magic)).ToInt32());
        Assert.Equal(4, Marshal.OffsetOf<FabricAmberMpu3Config>(nameof(FabricAmberMpu3Config.StructSize)).ToInt32());
        Assert.Equal(8, Marshal.OffsetOf<FabricAmberMpu3Config>(nameof(FabricAmberMpu3Config.Version)).ToInt32());
        Assert.Equal(12, Marshal.OffsetOf<FabricAmberMpu3Config>(nameof(FabricAmberMpu3Config.ReelCount)).ToInt32());
        Assert.Equal(16, Marshal.OffsetOf<FabricAmberMpu3Config>(nameof(FabricAmberMpu3Config.Reels)).ToInt32());
        Assert.Equal(32, Marshal.OffsetOf<FabricAmberMpu3Config>(nameof(FabricAmberMpu3Config.Dips)).ToInt32());
    }

    [Fact]
    public void BuilderWritesOnlyFourReelsAndSixteenDipBytes()
    {
        var settings = new Mpu3ProjectSettings();
        settings.Reels[0].OptoInvert = true;
        settings.Dips[0] = true;
        settings.Dips[15] = true;
        var bytes = FabricAmberMpu3Configuration.FromMpu3(settings).ToNativeBytes();
        Assert.Equal(48, bytes.Length);
        Assert.Equal(0x334D4146u, BitConverter.ToUInt32(bytes, 0));
        Assert.Equal(48u, BitConverter.ToUInt32(bytes, 4));
        Assert.Equal(1u, BitConverter.ToUInt32(bytes, 8));
        Assert.Equal(4u, BitConverter.ToUInt32(bytes, 12));
        Assert.Equal(new byte[] { 96, 0, 2, 1 }, bytes[16..20]);
        Assert.Equal((byte)1, bytes[32]);
        Assert.Equal((byte)1, bytes[47]);
        Assert.All(bytes[33..47], value => Assert.Equal((byte)0, value));
    }

    [Fact]
    public void DedicatedSettingsRoundTripPreservesThreeProgramRomPathsReelsAndDips()
    {
        var settings = new Mpu3ProjectSettings();
        settings.Reels[3].Steps = 128;
        settings.Reels[3].OptoInvert = true;
        settings.Dips[7] = true;
        settings.ProgramRoms[0].Path = "rr1.bin";
        settings.ProgramRoms[1].Path = "rr2.bin";
        settings.ProgramRoms[2].Path = "rr3.bin";
        var json = JsonSerializer.Serialize(settings);
        var result = JsonSerializer.Deserialize<Mpu3ProjectSettings>(json)!;
        Assert.Equal(4, result.Reels.Count);
        Assert.Equal(16, result.Dips.Count);
        Assert.Equal(128, result.Reels[3].Steps);
        Assert.True(result.Reels[3].OptoInvert);
        Assert.True(result.Dips[7]);
        Assert.Equal(["rr1.bin", "rr2.bin", "rr3.bin", ""], result.ProgramRoms.Select(rom => rom.Path));
        Assert.DoesNotContain("LoadAddress", json, StringComparison.Ordinal);
        Assert.Null(typeof(Mpu3ProgramRomSettings).GetProperty("LoadAddress"));
    }

    [Fact]
    public void ProgramResourcesPreserveSlotsAndAllowBlankTrailingSlot()
    {
        var settings = new Mpu3ProjectSettings();
        settings.ProgramRoms[0].Path = "rr1.bin";
        settings.ProgramRoms[1].Path = "rr2.bin";
        settings.ProgramRoms[2].Path = "rr3.bin";
        var resources = FabricEmulationBackend.BuildRomResources(settings);
        Assert.Equal(3, resources.Count);
        Assert.Equal([0u, 1u, 2u], resources.Select(resource => resource.Slot));
        Assert.All(resources, resource => Assert.Equal(FabricRomRole.Program, resource.Role));
        Assert.Equal(["rr1.bin", "rr2.bin", "rr3.bin"], resources.Select(resource => resource.Path));
        settings.ProgramRoms[3].Path = "rr4.bin";
        Assert.Equal(3u, FabricEmulationBackend.BuildRomResources(settings)[3].Slot);
    }

    [Fact]
    public void SharedAmberVisualNormalizationIncludesMpu3WithoutEpochOffset()
    {
        Assert.True(AlphaCellOrder.IsAmberBackedPlatform(FruitMachinePlatformType.MPU3));
        Assert.True(PlatformReelDirectionResolver.RequiresReversal(FruitMachinePlatformType.MPU3));
        Assert.Equal(0d, InternalReelOffsetResolver.ResolveNormalizedOffset(FruitMachinePlatformType.MPU3, 12));
    }
}
