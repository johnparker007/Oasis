using System.Runtime.InteropServices;

namespace OasisPlayer.Fabric
{
    internal static class FabricAbi
    {
        internal const uint Version = 0x00040000;
        internal const int IdentifierCapacity = 64;
        internal const int PathCapacity = 1024;
        internal const int CharacterCapacity = 16;
        internal const int SegmentCapacity = 16;
        internal const int DotMatrixMaxWidth = 128;
        internal const int DotMatrixMaxHeight = 16;
        internal const int DotMatrixMaxDots = 2048;
    }

    public enum FabricInputKind : uint { Digital = 0, Coin = 1 }

    [StructLayout(LayoutKind.Sequential)] internal unsafe struct FabricLaunchRequestNative { internal uint Size, Version; internal fixed byte BackendKind[64], MachineIdentifier[64], BackendPath[1024]; internal nint RomPaths; internal uint RomPathCount; internal nint Configuration; internal uint ConfigurationSize, Reserved; internal nint Resources; internal uint ResourceCount; }
    [StructLayout(LayoutKind.Sequential)] internal unsafe struct FabricRomResourceNative { internal uint Size, Version, Role, Slot; internal nint Path; internal fixed ulong Reserved[2]; }
    [StructLayout(LayoutKind.Sequential)] internal unsafe struct FabricCapabilitiesNative { internal uint Size, Version; internal ulong Flags; internal fixed ulong Reserved[4]; }
    [StructLayout(LayoutKind.Sequential)] internal unsafe struct FabricInputNative { internal uint Size, Version; internal fixed byte Identifier[64]; internal int NumericalIndex; internal FabricInputKind Kind; internal byte Active, CoinChannel, CoinValue; internal fixed byte Reserved[5]; }
    [StructLayout(LayoutKind.Sequential)] internal unsafe struct FabricLampNative { internal uint Size, Version; internal fixed byte Identifier[64]; internal int Index; internal byte LogicalState; internal fixed byte Reserved[3]; internal float Brightness; }
    [StructLayout(LayoutKind.Sequential)] internal unsafe struct FabricReelNative { internal uint Size, Version; internal fixed byte Identifier[64]; internal int Index, Position; }
    [StructLayout(LayoutKind.Sequential)] internal unsafe struct FabricCharacterDisplayNative { internal uint Size, Version; internal fixed byte Identifier[64]; internal uint Count, Capacity; internal fixed uint Characters[16]; internal fixed byte Attributes[16]; internal float Brightness; }
    [StructLayout(LayoutKind.Sequential)] internal unsafe struct FabricSegmentDisplayNative { internal uint Size, Version; internal fixed byte Identifier[64]; internal uint Count, Capacity; internal fixed ulong Masks[16]; }
    [StructLayout(LayoutKind.Sequential)] internal unsafe struct FabricDotMatrixDisplayNative { internal uint Size, Version; internal fixed byte Identifier[64]; internal uint Width, Height, DotCount, DotCapacity; internal fixed byte Dots[2048]; internal float Brightness; }
    [StructLayout(LayoutKind.Sequential)] internal struct FabricMachineSnapshotNative { internal uint Size, Version; internal ulong Sequence; internal nint Lamps; internal uint LampCapacity, LampCount; internal nint Reels; internal uint ReelCapacity, ReelCount; internal nint Characters; internal uint CharacterCapacity, CharacterCount; internal nint Segments; internal uint SegmentCapacity, SegmentCount; internal nint DotMatrices; internal uint DotMatrixCapacity, DotMatrixCount; }
    [StructLayout(LayoutKind.Sequential)] internal struct FabricAudioFormatNative { internal uint Size, Version, SampleRate; internal ushort Channels, BitsPerSample; internal byte Interleaved, Signed, LittleEndian, Reserved; }
}
