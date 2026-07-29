namespace OasisEditor;

public sealed record FabricAmberReel(
    uint Index, bool Enabled, uint Steps, uint OptoStart, uint OptoEnd, bool OptoInvert);

public sealed record FabricAmberCoinChannel(
    uint Index, bool Enabled, uint Value, bool LockoutInvert);

public sealed record FabricAmberCoinRoute(
    uint Index, bool Enabled, uint CounterIn, uint CounterOut,
    uint PortIndex, uint CoinCode, uint Level, uint FullLevel);

public sealed record FabricAmberConfiguration(
    uint ReelApplyMask,
    IReadOnlyList<FabricAmberReel> Reels,
    uint CoinChannelApplyMask,
    uint CoinRouteApplyMask,
    IReadOnlyList<FabricAmberCoinChannel> CoinChannels,
    IReadOnlyList<FabricAmberCoinRoute> CoinRoutes,
    uint? PercentageSwitch) : IFabricBackendConfiguration
{
    public static FabricAmberConfiguration FromSystem6(System6NativeRomSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var reels = settings.ReelOptos.Select(reel => new FabricAmberReel(
            checked((uint)reel.ReelIndex), reel.Enabled, checked((uint)reel.Steps),
            checked((uint)reel.OptoStart), checked((uint)reel.OptoEnd), reel.OptoInvert)).ToArray();

        // Match System6NativeBackend: only enabled coin slots are intentionally applied.
        var coins = settings.Coins.Where(coin => coin.Enabled).ToArray();
        return new FabricAmberConfiguration(
            reels.Aggregate(0u, (mask, reel) => mask | 1u << (int)reel.Index),
            reels,
            coins.Aggregate(0u, (mask, coin) => mask | 1u << coin.Num),
            coins.Aggregate(0u, (mask, coin) => mask | 1u << coin.Num),
            coins.Select(coin => new FabricAmberCoinChannel(
                checked((uint)coin.Num), coin.CoinEnable != 0, checked((uint)coin.CoinValue), coin.LockoutInvert != 0)).ToArray(),
            coins.Select(coin => new FabricAmberCoinRoute(
                checked((uint)coin.Num), coin.Enabled, checked((uint)coin.CounterIn), checked((uint)coin.CounterOut),
                checked((uint)coin.PortIndex), checked((uint)coin.Coin), checked((uint)coin.Level), checked((uint)coin.FullLevel))).ToArray(),
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
            Version = 1,
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
        native.Coins.Version = 1;
        native.Coins.ChannelMask = CoinChannelApplyMask;
        native.Coins.RouteMask = CoinRouteApplyMask;

        fixed (byte* pointer = native.Reels.Reels)
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
        fixed (byte* pointer = native.Coins.Channels)
        {
            for (var index = 0; index < CoinChannels.Count; index++)
            {
                var channel = CoinChannels[index];
                ((AmberCoinChannelNative*)pointer)[index] = new AmberCoinChannelNative
                {
                    Index = channel.Index,
                    Enabled = channel.Enabled ? 1u : 0,
                    Value = channel.Value,
                    LockoutInvert = channel.LockoutInvert ? 1u : 0
                };
            }
        }
        fixed (byte* pointer = native.Coins.Routes)
        {
            for (var index = 0; index < CoinRoutes.Count; index++)
            {
                var route = CoinRoutes[index];
                ((AmberCoinRouteNative*)pointer)[index] = new AmberCoinRouteNative
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
        fixed (byte* pointer = bytes)
            Buffer.MemoryCopy(&native, pointer, bytes.Length, bytes.Length);
        return bytes;
    }
}
