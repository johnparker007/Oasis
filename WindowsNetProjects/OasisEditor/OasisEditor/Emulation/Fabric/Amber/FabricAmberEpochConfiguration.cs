namespace OasisEditor;

/// <summary>Serializes the current Fabric v3 Maygay Epoch configuration ABI.</summary>
public sealed record FabricAmberEpochConfiguration(EpochNativeRomSettings Settings) : IFabricBackendConfiguration
{
    public const uint Magic = 0x50454146; // "FAEP" in little-endian memory
    public const uint Version = 1;
    public const uint ConfigureReels = 1, ConfigureCoins = 2, ConfigureOptions = 4;
    public const uint OptionDips = 1, OptionStake = 2, OptionPrize = 4, OptionPercentage = 8;

    public static FabricAmberEpochConfiguration FromEpoch(EpochNativeRomSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Validate(settings);
        return new(settings);
    }

    internal static void Validate(EpochNativeRomSettings settings)
    {
        ValidateIndexed(settings.Reels, 8, item => item.ReelIndex, "reel");
        foreach (var reel in settings.Reels)
        {
            Range(reel.Steps, 1, 255, "Epoch reel steps");
            Range(reel.OptoStart, 0, 255, "Epoch opto start");
            Range(reel.OptoEnd, 0, 255, "Epoch opto end");
        }
        ValidateIndexed(settings.Coins, 6, item => item.Channel, "coin channel");
        foreach (var coin in settings.Coins)
        {
            Range(coin.Value, 0, 255, "Epoch coin value");
            Range(coin.LockoutValue, 0, 255, "Epoch coin lockout value");
        }
        if (settings.ConfigureCoins && settings.PulseCycles == 0)
            throw new ArgumentOutOfRangeException(nameof(settings.PulseCycles), "Epoch coin pulse cycles must be nonzero.");
        Range((int)settings.CommunicationStyle, 0, 3, "Epoch communication style");
        if (settings.DipSwitchBits > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(settings.DipSwitchBits), "Epoch DIP switches use only the lower 16 bits.");
        if (settings.Stake > 255 || settings.Prize > 255 || settings.Percentage > 15)
            throw new ArgumentOutOfRangeException(nameof(settings), "Epoch stake/prize must be 0..255 and percentage 0..15.");
    }

    public unsafe byte[] ToNativeBytes()
    {
        Validate(Settings);
        var native = new FabricAmberEpochConfigurationNative
        {
            Magic = Magic, Size = (uint)sizeof(FabricAmberEpochConfigurationNative), Version = Version,
            FlashRomMode = Settings.FlashRomMode ? 1u : 0u
        };
        native.Reels.Size = (uint)sizeof(FabricAmberEpochReelConfigurationNative); native.Reels.Version = 1; native.Reels.Count = 8;
        native.Reels.ReelExt = Settings.ReelExt; native.Reels.ReelExtApply = Settings.ApplyReelExt ? 1u : 0u;
        native.Coins.Size = (uint)sizeof(FabricAmberEpochCoinConfigurationNative); native.Coins.Version = 1; native.Coins.Count = 6;
        native.Options.Size = (uint)sizeof(FabricAmberEpochOptionsNative); native.Options.Version = 1;
        if (Settings.ConfigureReels) native.Flags |= ConfigureReels;
        if (Settings.ConfigureCoins) native.Flags |= ConfigureCoins;
        if (Settings.ConfigureMachineOptions) native.Flags |= ConfigureOptions;
        foreach (var reel in Settings.Reels)
        {
            if (reel.Apply) native.Reels.ApplyMask |= 1u << reel.ReelIndex;
            ((FabricAmberEpochReelConfigNative*)native.Reels.Reels)[reel.ReelIndex] = new()
            { ReelIndex=(uint)reel.ReelIndex, Steps=(uint)reel.Steps, OptoStart=(uint)reel.OptoStart, OptoEnd=(uint)reel.OptoEnd, OptoInvert=reel.OptoInvert?1u:0u };
        }
        native.Coins.CommunicationStyle=(uint)Settings.CommunicationStyle; native.Coins.CommunicationInvert=Settings.CommunicationInvert?1u:0u;
        native.Coins.PulseCycles=Settings.PulseCycles; native.Coins.EdcEnabled=Settings.EdcEnabled?1u:0u;
        foreach (var coin in Settings.Coins)
        {
            if (coin.Apply) native.Coins.ApplyMask |= 1u << coin.Channel;
            ((FabricAmberEpochCoinChannelConfigNative*)native.Coins.Channels)[coin.Channel] = new()
            { ChannelIndex=(uint)coin.Channel, Enabled=coin.Enabled?1u:0u, Value=(uint)coin.Value, LockoutValue=(uint)coin.LockoutValue, LockoutInvert=coin.LockoutInvert?1u:0u };
        }
        native.Options.ApplyMask = (Settings.ApplyDips?OptionDips:0)|(Settings.ApplyStake?OptionStake:0)|(Settings.ApplyPrize?OptionPrize:0)|(Settings.ApplyPercentage?OptionPercentage:0);
        native.Options.DipSwitchBits=Settings.DipSwitchBits; native.Options.Stake=Settings.Stake; native.Options.Prize=Settings.Prize; native.Options.Percentage=Settings.Percentage;
        var bytes = new byte[sizeof(FabricAmberEpochConfigurationNative)];
        fixed (byte* destination = bytes) Buffer.MemoryCopy(&native, destination, bytes.Length, bytes.Length);
        return bytes;
    }

    private static void Range(int value,int min,int max,string name) { if(value<min||value>max) throw new ArgumentOutOfRangeException(name,value,$"{name} must be {min}..{max}."); }
    private static void ValidateIndexed<T>(IReadOnlyList<T> values,int capacity,Func<T,int> index,string name)
    { if(values.Count>capacity) throw new ArgumentException($"Epoch supports at most {capacity} {name} entries."); var seen=new HashSet<int>(); foreach(var value in values){var i=index(value); if(i<0||i>=capacity) throw new ArgumentOutOfRangeException(name,i,$"Epoch {name} index must be 0..{capacity-1}."); if(!seen.Add(i)) throw new ArgumentException($"Duplicate Epoch {name} index {i}.");} }
}
