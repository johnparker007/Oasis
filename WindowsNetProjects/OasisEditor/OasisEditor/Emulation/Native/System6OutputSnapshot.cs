using System.Runtime.InteropServices;

namespace OasisEditor;

// Mirrors PA2CoreInterface.h from johnparker007/OasisEmulator (PA2_OUTPUT_SNAPSHOT_VERSION = 1).
// The native header uses #pragma pack(push, 4); keep these layouts in sync with that ABI.
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct System6NativeLampState
{
    public byte OnOff;
    public byte Reserved0;
    public byte Reserved1;
    public byte Reserved2;
    public float Brightness;
    public float FilamentR;
    public float FilamentG;
    public float FilamentB;
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct System6NativeReelState
{
    public int Position;
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public unsafe struct System6NativeAlphaSegmentedState
{
    public fixed ushort Segments[System6NativeOutputSnapshot.AlphaCharacterCount];
    public fixed byte DotComma[System6NativeOutputSnapshot.AlphaCharacterCount];
    public float Brightness;
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct System6NativeLedDisplayState
{
    public uint OnOff;
    public float Brightness;
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public unsafe struct System6NativeOutputSnapshot
{
    public const uint VersionValue = 1;
    public const int MatrixLampCapacity = 512;
    public const int ReelCapacity = 8;
    public const int AlphaDisplayCapacity = 2;
    public const int AlphaCharacterCount = 16;
    public const int LedDisplayCapacity = 40;

    public uint SizeBytes;
    public uint Version;
    public uint MatrixLampCount;
    public uint DirectLampCount;
    public uint FloLampCount;
    public uint PrismLampCount;
    public uint LedCount;
    public uint TriacLampCount;
    public uint FluorescentLampCount;
    public uint DiscoLampCount;
    public uint ReelCount;
    public uint AlphaSegmentedDisplayCount;
    public uint AlphaDotDisplayCount;
    public uint LedDisplayCount;
    public uint ElectronicMechCount;
    public uint MechanicalMechCount;
    public uint CoinEntryLampCount;
    public uint MeterCount;
    public uint TubeCount;
    public uint DipCount;
    public uint HopperCount;

    public fixed byte MatrixLamps[MatrixLampCapacity * 20];
    public fixed byte DirectLamps[32 * 20];
    public fixed byte FloLamps[4 * 20];
    public fixed byte PrismLamps[16 * 20];
    public fixed byte Leds[512 * 20];
    public fixed byte TriacLamps[32 * 20];
    public fixed byte FluorescentLamps[8 * 20];
    public fixed byte DiscoLamps[64 * 20];
    public fixed int Reels[ReelCapacity];
    public fixed byte AlphaSegmented[AlphaDisplayCapacity * 52];
    public fixed byte AlphaDot[AlphaDisplayCapacity * 100];
    public fixed byte LedDisplays[LedDisplayCapacity * 8];
    public fixed byte Remaining[464];


    public int GetReelPosition(int index)
    {
        if ((uint)index >= ReelCapacity) throw new ArgumentOutOfRangeException(nameof(index));
        unsafe
        {
            fixed (int* ptr = Reels)
            {
                return ptr[index];
            }
        }
    }

    public void SetReelPosition(int index, int position)
    {
        if ((uint)index >= ReelCapacity) throw new ArgumentOutOfRangeException(nameof(index));
        unsafe
        {
            fixed (int* ptr = Reels)
            {
                ptr[index] = position;
            }
        }
    }

    public System6NativeLampState GetMatrixLamp(int index)
    {
        if ((uint)index >= MatrixLampCapacity) throw new ArgumentOutOfRangeException(nameof(index));
        unsafe
        {
            fixed (byte* ptr = MatrixLamps)
            {
                return *(System6NativeLampState*)(ptr + (index * sizeof(System6NativeLampState)));
            }
        }
    }

    public void SetMatrixLamp(int index, System6NativeLampState value)
    {
        if ((uint)index >= MatrixLampCapacity) throw new ArgumentOutOfRangeException(nameof(index));
        unsafe
        {
            fixed (byte* ptr = MatrixLamps)
            {
                *(System6NativeLampState*)(ptr + (index * sizeof(System6NativeLampState))) = value;
            }
        }
    }

    public System6NativeAlphaSegmentedState GetAlphaSegmented(int index)
    {
        if ((uint)index >= AlphaDisplayCapacity) throw new ArgumentOutOfRangeException(nameof(index));
        unsafe
        {
            fixed (byte* ptr = AlphaSegmented)
            {
                return *(System6NativeAlphaSegmentedState*)(ptr + (index * sizeof(System6NativeAlphaSegmentedState)));
            }
        }
    }

    public void SetAlphaSegmented(int index, System6NativeAlphaSegmentedState value)
    {
        if ((uint)index >= AlphaDisplayCapacity) throw new ArgumentOutOfRangeException(nameof(index));
        unsafe
        {
            fixed (byte* ptr = AlphaSegmented)
            {
                *(System6NativeAlphaSegmentedState*)(ptr + (index * sizeof(System6NativeAlphaSegmentedState))) = value;
            }
        }
    }

    public System6NativeLedDisplayState GetLedDisplay(int index)
    {
        if ((uint)index >= LedDisplayCapacity) throw new ArgumentOutOfRangeException(nameof(index));
        unsafe
        {
            fixed (byte* ptr = LedDisplays)
            {
                return *(System6NativeLedDisplayState*)(ptr + (index * sizeof(System6NativeLedDisplayState)));
            }
        }
    }

    public void SetLedDisplay(int index, System6NativeLedDisplayState value)
    {
        if ((uint)index >= LedDisplayCapacity) throw new ArgumentOutOfRangeException(nameof(index));
        unsafe
        {
            fixed (byte* ptr = LedDisplays)
            {
                *(System6NativeLedDisplayState*)(ptr + (index * sizeof(System6NativeLedDisplayState))) = value;
            }
        }
    }
}
