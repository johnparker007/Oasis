namespace OasisEditor;

public sealed record FabricAmberReel(
    uint Index, bool Enabled, uint Steps, uint OptoStart, uint OptoEnd, bool OptoInvert);

public sealed record FabricAmberCoinChannel(
    uint Index, bool Enabled, uint Value, bool LockoutInvert);

public sealed record FabricAmberCoinRoute(
    uint Index, bool Enabled, uint CounterIn, uint CounterOut,
    uint PortIndex, uint CoinCode, uint Level, uint FullLevel);

public sealed record FabricAmberSystem6Configuration(
    uint ReelApplyMask,
    IReadOnlyList<FabricAmberReel> Reels,
    uint CoinChannelApplyMask,
    uint CoinRouteApplyMask,
    IReadOnlyList<FabricAmberCoinChannel> CoinChannels,
    IReadOnlyList<FabricAmberCoinRoute> CoinRoutes,
    AmberCoinCommunicationStyle CommunicationStyle,
    bool CommunicationInvert,
    uint PulseCycles,
    bool EdcEnabled,
    uint? PercentageSwitch) : IFabricBackendConfiguration
{
    public static FabricAmberSystem6Configuration FromSystem6(System6NativeRomSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var reels = settings.ReelOptos.Select(reel => new FabricAmberReel(
            checked((uint)reel.ReelIndex), reel.Enabled, checked((uint)reel.Steps),
            checked((uint)reel.OptoStart), checked((uint)reel.OptoEnd), reel.OptoInvert)).ToArray();

        // Only enabled coin slots are intentionally applied.
        var coins = settings.Coins.Where(coin => coin.Enabled).ToArray();
        return new FabricAmberSystem6Configuration(
            reels.Aggregate(0u, (mask, reel) => mask | 1u << (int)reel.Index),
            reels,
            coins.Aggregate(0u, (mask, coin) => mask | 1u << coin.Num),
            coins.Aggregate(0u, (mask, coin) => mask | 1u << coin.Num),
            coins.Select(coin => new FabricAmberCoinChannel(
                checked((uint)coin.Num), coin.CoinEnable != 0, checked((uint)coin.CoinValue), coin.LockoutInvert != 0)).ToArray(),
            coins.Select(coin => new FabricAmberCoinRoute(
                checked((uint)coin.Num), coin.Enabled, checked((uint)coin.CounterIn), checked((uint)coin.CounterOut),
                checked((uint)coin.PortIndex), checked((uint)coin.Coin), checked((uint)coin.Level), checked((uint)coin.FullLevel))).ToArray(),
            settings.CoinCommunicationStyle,
            settings.CoinCommunicationInvert,
            settings.CoinPulseCycles,
            settings.CoinEdcEnabled,
            ValidatePercentageSwitch(settings.PercentSwitchValue));
    }

    private static uint ValidatePercentageSwitch(int value)
    {
        if (value is < 0 or > 15)
            throw new ArgumentOutOfRangeException(nameof(value), value,
                "System 6 percentage switch must be a raw value from 0 to 15.");
        return (uint)value;
    }

    public unsafe byte[] ToNativeBytes()
    {
        if (Reels.Count > 8 || CoinChannels.Count > 6 || CoinRoutes.Count > 8)
            throw new ArgumentException("Amber configuration exceeds ABI capacities.");

        var native = new FabricAmberConfigurationNative
        {
            Magic = 0x32424146,
            Size = (uint)sizeof(FabricAmberConfigurationNative),
            Version = 2,
            Flags = (Reels.Count > 0 ? 1u : 0)
                | (CoinChannels.Count > 0 || CoinRoutes.Count > 0 ? 2u : 0)
                | (PercentageSwitch.HasValue ? 4u : 0),
            Percentage = PercentageSwitch ?? 0
        };
        native.Reels.Size = (uint)sizeof(AmberReelsNative);
        native.Reels.Version = 1;
        native.Reels.Count = (uint)Reels.Count;
        native.Reels.ApplyMask = ReelApplyMask;
        native.Coins.Size = (uint)sizeof(AmberCoinsNative);
        native.Coins.Version = 2;
        native.Coins.ChannelMask = CoinChannelApplyMask;
        native.Coins.RouteMask = CoinRouteApplyMask;
        native.Coins.CommunicationStyle = (uint)CommunicationStyle;
        native.Coins.CommunicationInvert = CommunicationInvert ? 1u : 0u;
        native.Coins.PulseCycles = PulseCycles != 0 ? PulseCycles : throw new ArgumentOutOfRangeException(nameof(PulseCycles));
        native.Coins.EdcEnabled = EdcEnabled ? 1u : 0u;

        byte* pointer = native.Reels.Reels;
        {
            for (var index = 0; index < Reels.Count; index++)
            {
                var reel = Reels[index];
                ((AmberReelNative*)pointer)[index] = new AmberReelNative
                {
                    Index = reel.Index,
                    Enabled = reel.Enabled ? 1u : 0,
                    Steps = reel.Steps,
                    OptoStart = reel.OptoStart,
                    OptoEnd = reel.OptoEnd,
                    OptoInvert = reel.OptoInvert ? 1u : 0
                };
            }
        }
        pointer = native.Coins.Channels;
        {
            foreach (var channel in CoinChannels)
            {
                if (channel.Index >= 6)
                    throw new ArgumentOutOfRangeException(nameof(CoinChannels), channel.Index, "Amber coin channel index must be from 0 to 5.");
                if (channel.Value > 12)
                    throw new ArgumentOutOfRangeException(nameof(CoinChannels), channel.Value, "Amber coin denomination must be from 0 to 12.");
                ((AmberCoinChannelNative*)pointer)[channel.Index] = new AmberCoinChannelNative
                {
                    Index = channel.Index,
                    Enabled = channel.Enabled ? 1u : 0,
                    Value = channel.Value,
                    LockoutInvert = channel.LockoutInvert ? 1u : 0
                };
            }
        }
        pointer = native.Coins.Routes;
        {
            foreach (var route in CoinRoutes)
            {
                if (route.Index >= 8)
                    throw new ArgumentOutOfRangeException(nameof(CoinRoutes), route.Index, "Amber coin route index must be from 0 to 7.");
                ((AmberCoinRouteNative*)pointer)[route.Index] = new AmberCoinRouteNative
                {
                    Index = route.Index,
                    Enabled = route.Enabled ? 1u : 0,
                    CounterIn = route.CounterIn,
                    CounterOut = route.CounterOut,
                    PortIndex = route.PortIndex,
                    CoinCode = route.CoinCode,
                    Level = route.Level,
                    FullLevel = route.FullLevel
                };
            }
        }

        var bytes = new byte[sizeof(FabricAmberConfigurationNative)];
        fixed (byte* destination = bytes)
            Buffer.MemoryCopy(&native, destination, bytes.Length, bytes.Length);
        return bytes;
    }
}
