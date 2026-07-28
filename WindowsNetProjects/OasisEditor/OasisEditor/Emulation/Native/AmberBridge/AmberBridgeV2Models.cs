using System.Runtime.InteropServices;

namespace OasisEditor;

public sealed record AmberBridgeCapabilities(ulong RawFeatureBits, uint MaximumSwitches)
{
    public bool SupportsSwitchInput => (RawFeatureBits & AmberNativeConstants.SwitchInput) != 0;
    public bool SupportsOutputSnapshots => (RawFeatureBits & AmberNativeConstants.OutputSnapshot) != 0;
    public bool SupportsAudio => (RawFeatureBits & AmberNativeConstants.Audio) != 0;
    public bool SupportsReelConfiguration => (RawFeatureBits & AmberNativeConstants.ReelConfiguration) != 0;
    public bool SupportsCoinConfiguration => (RawFeatureBits & AmberNativeConstants.CoinConfiguration) != 0;
    public bool SupportsPercentageSwitch => (RawFeatureBits & AmberNativeConstants.PercentageSwitch) != 0;
}

public readonly record struct AmberAudioFormat(uint SampleRate, uint Channels, uint SampleFormat, uint Interleaving);
public readonly record struct AmberLampState(bool IsOn, double Brightness);
public sealed record AmberAlphaDisplayState(ushort[] SegmentMasks, byte[] Punctuation, double Brightness);
public readonly record struct AmberSevenSegmentState(uint SegmentMask, double Brightness);
public readonly record struct AmberReelConfigurationEntry(uint Index, bool Enabled, uint Steps, uint OptoStart, uint OptoEnd, bool OptoInvert);
public sealed record AmberReelConfiguration(uint ApplyMask, IReadOnlyList<AmberReelConfigurationEntry> Reels);
public readonly record struct AmberCoinChannelConfiguration(uint Index, bool Enabled, uint Value, bool LockoutInvert);
public readonly record struct AmberCoinRouteConfiguration(uint Index, bool Enabled, uint CounterIn, uint CounterOut, uint PortIndex, uint CoinCode, uint Level, uint FullLevel);
public sealed record AmberCoinConfiguration(uint ChannelApplyMask, uint RouteApplyMask,
    IReadOnlyList<AmberCoinChannelConfiguration> Channels, IReadOnlyList<AmberCoinRouteConfiguration> Routes,
    bool ApplyLockoutPort = false, uint LockoutPortBase = 0, uint LockoutPortValue = 0);

public sealed unsafe class AmberOutputSnapshotBuffer
{
    internal AmberOutputSnapshotV1Native Native;
    public uint MatrixLampCount => Native.MatrixLampCount;
    public uint ReelCount => Native.ReelCount;
    public uint AlphaDisplayCount => Native.AlphaDisplayCount;
    public uint SevenSegmentDisplayCount => Native.SevenSegmentDisplayCount;
    public AmberLampState GetLamp(int index)
    {
        Check(index, MatrixLampCount, AmberNativeConstants.MaximumMatrixLamps);
        fixed (byte* p = Native.MatrixLamps) { var value = ((AmberLampStateV1Native*)p)[index]; return new(value.IsOn != 0, AmberQ16_16.Decode(value.BrightnessQ16_16)); }
    }
    public int GetReelPosition(int index) { Check(index, ReelCount, AmberNativeConstants.MaximumReels); fixed (int* p = Native.ReelPositions) return p[index]; }
    public AmberAlphaDisplayState GetAlphaDisplay(int index)
    {
        Check(index, AlphaDisplayCount, AmberNativeConstants.MaximumAlphaDisplays);
        var masks = new ushort[16]; var punctuation = new byte[16]; double brightness;
        fixed (byte* p = Native.AlphaDisplays)
        {
            var state = (AmberAlphaDisplayStateV1Native*)(p + index * sizeof(AmberAlphaDisplayStateV1Native));
            for (var i = 0; i < 16; i++) { masks[i] = state->SegmentMasks[i]; punctuation[i] = state->DotComma[i]; }
            brightness = AmberQ16_16.Decode(state->BrightnessQ16_16);
        }
        return new(masks, punctuation, brightness);
    }
    public ushort GetAlphaSegmentMask(int displayIndex, int characterIndex)
    { Check(displayIndex, AlphaDisplayCount, 2); if ((uint)characterIndex >= 16) throw new ArgumentOutOfRangeException(nameof(characterIndex)); fixed (byte* p = Native.AlphaDisplays) return ((AmberAlphaDisplayStateV1Native*)(p + displayIndex * 52))->SegmentMasks[characterIndex]; }
    public byte GetAlphaPunctuation(int displayIndex, int characterIndex)
    { Check(displayIndex, AlphaDisplayCount, 2); if ((uint)characterIndex >= 16) throw new ArgumentOutOfRangeException(nameof(characterIndex)); fixed (byte* p = Native.AlphaDisplays) return ((AmberAlphaDisplayStateV1Native*)(p + displayIndex * 52))->DotComma[characterIndex]; }
    public double GetAlphaBrightness(int displayIndex)
    { Check(displayIndex, AlphaDisplayCount, 2); fixed (byte* p = Native.AlphaDisplays) return AmberQ16_16.Decode(((AmberAlphaDisplayStateV1Native*)(p + displayIndex * 52))->BrightnessQ16_16); }
    public AmberSevenSegmentState GetSevenSegmentDisplay(int index)
    {
        Check(index, SevenSegmentDisplayCount, AmberNativeConstants.MaximumSevenSegmentDisplays);
        fixed (byte* p = Native.SevenSegmentDisplays) { var value = ((AmberSevenSegmentStateV1Native*)p)[index]; return new(value.SegmentMask, AmberQ16_16.Decode(value.BrightnessQ16_16)); }
    }
    internal void Prepare() { Native = default; Native.StructSize = (uint)sizeof(AmberOutputSnapshotV1Native); Native.Version = 1; }
    internal void ValidateCounts()
    {
        if (MatrixLampCount > 512 || ReelCount > 8 || AlphaDisplayCount > 2 || SevenSegmentDisplayCount > 40)
            throw new InvalidDataException($"Amber output snapshot contains invalid counts: lamps={MatrixLampCount}, reels={ReelCount}, alpha={AlphaDisplayCount}, sevenSegment={SevenSegmentDisplayCount}.");
    }
    private static void Check(int index, uint count, int capacity) { if (index < 0 || index >= count || index >= capacity) throw new ArgumentOutOfRangeException(nameof(index)); }
}

internal static class AmberQ16_16 { internal static double Decode(uint value) => value / 65536.0; }
