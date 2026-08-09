using System.Runtime.InteropServices;

namespace OasisEditor;

public sealed record FabricAmberMpu3Configuration(Mpu3ProjectSettings Settings) : IFabricBackendConfiguration
{
    public const uint ConfigurationMagic = 0x334D4146;
    public const uint ConfigurationVersion = 1;
    public const int ReelCount = 4;
    public const int DipCount = 16;
    public const int ConfigurationSize = 48;

    public static FabricAmberMpu3Configuration FromMpu3(Mpu3ProjectSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Validate(settings);
        return new(settings);
    }

    public static void Validate(Mpu3ProjectSettings settings)
    {
        if (settings.Reels.Count != ReelCount || settings.Reels.Select(x => x.ReelIndex).Distinct().Count() != ReelCount || settings.Reels.Any(x => x.ReelIndex is < 0 or >= ReelCount))
            throw new ArgumentException("MPU3 requires exactly four uniquely indexed reel configurations.", nameof(settings));
        if (settings.Dips.Count != DipCount)
            throw new ArgumentException("MPU3 requires exactly 16 DIP switch values.", nameof(settings));
        foreach (var reel in settings.Reels)
        {
            if (reel.Steps is < 1 or > byte.MaxValue) throw new ArgumentOutOfRangeException(nameof(reel.Steps), "MPU3 reel steps must be 1..255.");
            if (reel.OptoStart is < 0 or > byte.MaxValue) throw new ArgumentOutOfRangeException(nameof(reel.OptoStart), "MPU3 opto start must be 0..255.");
            if (reel.OptoEnd is < 0 or > byte.MaxValue) throw new ArgumentOutOfRangeException(nameof(reel.OptoEnd), "MPU3 opto end must be 0..255.");
        }
    }

    public unsafe byte[] ToNativeBytes()
    {
        Validate(Settings);
        if (Marshal.SizeOf<FabricAmberMpu3Config>() != ConfigurationSize) throw new InvalidOperationException("Managed MPU3 configuration ABI is not 48 bytes.");
        var native = new FabricAmberMpu3Config { Magic = ConfigurationMagic, StructSize = ConfigurationSize, Version = ConfigurationVersion, ReelCount = ReelCount };
        var reels = (FabricAmberMpu3ReelConfig*)native.Reels;
        foreach (var reel in Settings.Reels)
            reels[reel.ReelIndex] = new() { Steps = (byte)reel.Steps, OptoStart = (byte)reel.OptoStart, OptoEnd = (byte)reel.OptoEnd, OptoInvert = reel.OptoInvert ? (byte)1 : (byte)0 };
        for (var index = 0; index < DipCount; index++) native.Dips[index] = Settings.Dips[index] ? (byte)1 : (byte)0;
        var bytes = new byte[ConfigurationSize];
        fixed (byte* destination = bytes) Buffer.MemoryCopy(&native, destination, ConfigurationSize, ConfigurationSize);
        return bytes;
    }
}
