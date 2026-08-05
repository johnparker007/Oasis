namespace OasisEditor;

public sealed record FabricAmberMpu5Configuration(
    uint ReelApplyMask,
    IReadOnlyList<FabricAmberReel> Reels,
    uint DipSwitchMask,
    AmberCoinCommunicationStyle CommunicationStyle,
    bool CommunicationInvert,
    uint PulseCycles,
    uint Percentage,
    uint Stake,
    uint Prize,
    Mpu5PicMode PicMode,
    uint CharacteriserAddress,
    bool SecFitted,
    Mpu5HopperType HopperType,
    uint ReelJumperProfile) : IFabricBackendConfiguration
{
    public static FabricAmberMpu5Configuration FromMpu5(Mpu5NativeRomSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var reels = settings.ReelOptos.Select(reel => new FabricAmberReel(
            checked((uint)reel.ReelIndex), reel.Enabled, checked((uint)reel.Steps),
            checked((uint)reel.OptoStart), checked((uint)reel.OptoEnd), reel.OptoInvert)).ToArray();
        var dipMask = settings.DipSwitches.Aggregate(0u, (mask, dip) =>
        {
            if (dip.Index is < 0 or >= Mpu5NativeRomSettings.DefaultDipSwitchCount)
                throw new ArgumentOutOfRangeException(nameof(settings.DipSwitches), dip.Index, "MPU5 DIP switch index must be from 0 to 15.");
            return dip.Enabled ? mask | (1u << dip.Index) : mask;
        });
        return new FabricAmberMpu5Configuration(
            reels.Aggregate(0u, (mask, reel) => mask | 1u << (int)reel.Index),
            reels, dipMask, settings.CoinCommunicationStyle, settings.CoinCommunicationInvert,
            settings.CoinPulseCycles, checked((uint)settings.Percentage), checked((uint)settings.Stake), checked((uint)settings.Prize),
            settings.PicMode, settings.CharacteriserAddress, settings.SecFitted, settings.HopperType, settings.ReelJumperProfile);
    }

    public unsafe byte[] ToNativeBytes()
    {
        if (Reels.Count > 8)
            throw new ArgumentException("MPU5 Amber configuration supports at most eight reels.");
        if (PulseCycles == 0)
            throw new ArgumentOutOfRangeException(nameof(PulseCycles));
        var native = new FabricAmberMpu5ConfigurationNative
        {
            Magic = 0x35554146,
            Size = (uint)sizeof(FabricAmberMpu5ConfigurationNative),
            Version = 1,
            Flags = 1,
            DipSwitchMask = DipSwitchMask,
            Percentage = Percentage,
            Stake = Stake,
            Prize = Prize,
            PicMode = (uint)PicMode,
            CharacteriserAddress = CharacteriserAddress,
            SecFitted = SecFitted ? 1u : 0u,
            HopperType = (uint)HopperType,
            ReelJumperProfile = ReelJumperProfile,
            CoinCommunicationStyle = (uint)CommunicationStyle,
            CoinCommunicationInvert = CommunicationInvert ? 1u : 0u,
            CoinPulseCycles = PulseCycles
        };
        native.Reels.Size = (uint)sizeof(AmberReelsNative);
        native.Reels.Version = 1;
        native.Reels.Count = (uint)Reels.Count;
        native.Reels.ApplyMask = ReelApplyMask;
        var pointer = native.Reels.Reels;
        for (var index = 0; index < Reels.Count; index++)
        {
            var reel = Reels[index];
            if (reel.Index >= 8)
                throw new ArgumentOutOfRangeException(nameof(Reels), reel.Index, "MPU5 reel index must be from 0 to 7.");
            if (reel.Steps == 0)
                throw new ArgumentOutOfRangeException(nameof(Reels), reel.Steps, "MPU5 reel steps must be nonzero.");
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
        var bytes = new byte[sizeof(FabricAmberMpu5ConfigurationNative)];
        fixed (byte* destination = bytes)
            Buffer.MemoryCopy(&native, destination, bytes.Length, bytes.Length);
        return bytes;
    }
}
