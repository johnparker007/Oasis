namespace OasisEditor;

public sealed record FabricAmberMpu5Configuration(Mpu5NativeRomSettings Settings) : IFabricBackendConfiguration
{
    public static FabricAmberMpu5Configuration FromMpu5(Mpu5NativeRomSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Validate(settings);
        return new(settings);
    }

    private static void Validate(Mpu5NativeRomSettings settings)
    {
        if (settings.Reels.Count > Mpu5NativeRomSettings.ReelSlotCount)
            throw new ArgumentException("MPU5 supports at most eight configured reels.", nameof(settings));
        if (settings.Coins.Count > Mpu5NativeRomSettings.CoinChannelCount)
            throw new ArgumentException("MPU5 supports at most six coin channels.", nameof(settings));
        var reelIndices = new HashSet<int>();
        foreach (var reel in settings.Reels)
        {
            if (reel.ReelIndex is < 0 or >= Mpu5NativeRomSettings.ReelSlotCount)
                throw new ArgumentOutOfRangeException(nameof(settings), reel.ReelIndex, "MPU5 reel index must be from 0 to 7.");
            if (!reelIndices.Add(reel.ReelIndex))
                throw new ArgumentException($"MPU5 reel index {reel.ReelIndex} is configured more than once.", nameof(settings));
            if (reel.Steps <= 0)
                throw new ArgumentOutOfRangeException(nameof(settings), reel.Steps, "MPU5 reel step count must be positive.");
        }
        var coinIndices = new HashSet<int>();
        foreach (var coin in settings.Coins)
        {
            if (coin.Channel is < 0 or >= Mpu5NativeRomSettings.CoinChannelCount)
                throw new ArgumentOutOfRangeException(nameof(settings), coin.Channel, "MPU5 coin channel must be from 0 to 5.");
            if (!coinIndices.Add(coin.Channel))
                throw new ArgumentException($"MPU5 coin channel {coin.Channel} is configured more than once.", nameof(settings));
        }
    }

    public unsafe byte[] ToNativeBytes()
    {
        Validate(Settings);
        var native = new FabricAmberMpu5ConfigurationNative
        {
            Magic = 0x354D4146, // "FAM5", Fabric Amber MPU5 configuration.
            Size = (uint)sizeof(FabricAmberMpu5ConfigurationNative), Version = 1,
            Flags = 7,
            Options = new AmberMpu5MachineOptionsNative
            {
                Percentage=Settings.Percentage, Stake=Settings.Stake, Prize=Settings.Prize,
                DipSwitches=Settings.DipSwitches, PicMode=Settings.PicMode, PicSelection=Settings.PicSelection,
                CharacteriserAddress=Settings.CharacteriserAddress, SecFitted=Settings.SecFitted ? 1u : 0u,
                HopperType=Settings.HopperType
            }
        };
        native.Reels.Size=(uint)sizeof(AmberMpu5ReelsNative); native.Reels.Version=1; native.Reels.Count=(uint)Settings.Reels.Count;
        native.Coins.Size=(uint)sizeof(AmberMpu5CoinsNative); native.Coins.Version=1; native.Coins.Count=(uint)Settings.Coins.Count;
        foreach (var reel in Settings.Reels)
        {
            native.Reels.ApplyMask |= 1u << reel.ReelIndex;
            ((AmberMpu5ReelNative*)native.Reels.Reels)[reel.ReelIndex] = new() { Index=(uint)reel.ReelIndex, Enabled=reel.Enabled?1u:0u, Steps=(uint)reel.Steps, OptoStart=(uint)reel.OptoStart, OptoEnd=(uint)reel.OptoEnd, OptoInvert=reel.OptoInvert?1u:0u, JumperProfile=reel.JumperProfile };
        }
        foreach (var coin in Settings.Coins)
        {
            native.Coins.ApplyMask |= 1u << coin.Channel;
            ((AmberMpu5CoinNative*)native.Coins.Channels)[coin.Channel] = new() { Index=(uint)coin.Channel, Enabled=coin.Enabled?1u:0u, Value=coin.Value, LockoutInvert=coin.LockoutInvert?1u:0u };
        }
        var bytes=new byte[sizeof(FabricAmberMpu5ConfigurationNative)];
        fixed(byte* destination=bytes) Buffer.MemoryCopy(&native,destination,bytes.Length,bytes.Length);
        return bytes;
    }
}
