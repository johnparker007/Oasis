using System.Runtime.InteropServices;

namespace OasisEditor;

internal static class FabricAbi { internal const uint Version = 0x00030000; internal const int IdentifierCapacity=64, PathCapacity=1024, CharacterCapacity=16, SegmentCapacity=16; }

public enum FabricInputKind : uint { Digital = 0, Coin = 1 }

[StructLayout(LayoutKind.Sequential)] internal unsafe struct FabricLaunchRequestNative { internal uint Size,Version; internal fixed byte BackendKind[64],MachineIdentifier[64],BackendPath[1024]; internal nint RomPaths; internal uint RomPathCount; internal nint Configuration; internal uint ConfigurationSize,Reserved; internal nint Resources; internal uint ResourceCount; }
[StructLayout(LayoutKind.Sequential)] internal unsafe struct FabricRomResourceNative { internal uint Size,Version,Role,Slot; internal nint Path; internal fixed ulong Reserved[2]; }
[StructLayout(LayoutKind.Sequential)] internal unsafe struct FabricCapabilitiesNative { internal uint Size,Version; internal ulong Flags; internal fixed ulong Reserved[4]; }
[StructLayout(LayoutKind.Sequential)] internal unsafe struct FabricInputNative { internal uint Size,Version; internal fixed byte Identifier[64]; internal int NumericalIndex; internal FabricInputKind Kind; internal byte Active,CoinChannel,CoinValue; internal fixed byte Reserved[5]; }
[StructLayout(LayoutKind.Sequential)] internal unsafe struct FabricLampNative { internal uint Size,Version; internal fixed byte Identifier[64]; internal int Index; internal byte LogicalState; internal fixed byte Reserved[3]; internal float Brightness; }
[StructLayout(LayoutKind.Sequential)] internal unsafe struct FabricReelNative { internal uint Size,Version; internal fixed byte Identifier[64]; internal int Index,Position; }
[StructLayout(LayoutKind.Sequential)] internal unsafe struct FabricCharacterDisplayNative { internal uint Size,Version; internal fixed byte Identifier[64]; internal uint Count,Capacity; internal fixed uint Characters[16]; internal fixed byte Attributes[16]; internal float Brightness; }
[StructLayout(LayoutKind.Sequential)] internal unsafe struct FabricSegmentDisplayNative { internal uint Size,Version; internal fixed byte Identifier[64]; internal uint Count,Capacity; internal fixed ulong Masks[16]; }
[StructLayout(LayoutKind.Sequential)] internal struct FabricMachineSnapshotNative { internal uint Size,Version; internal ulong Sequence; internal nint Lamps; internal uint LampCapacity,LampCount; internal nint Reels; internal uint ReelCapacity,ReelCount; internal nint Characters; internal uint CharacterCapacity,CharacterCount; internal nint Segments; internal uint SegmentCapacity,SegmentCount; }
[StructLayout(LayoutKind.Sequential)] internal struct FabricAudioFormatNative { internal uint Size,Version,SampleRate; internal ushort Channels,BitsPerSample; internal byte Interleaved,Signed,LittleEndian,Reserved; }

[StructLayout(LayoutKind.Sequential)] internal struct AmberReelNative { internal uint Index,Enabled,Steps,OptoStart,OptoEnd,OptoInvert; }
[StructLayout(LayoutKind.Sequential)] internal unsafe struct AmberReelsNative { internal uint Size,Version,Count,ApplyMask; internal fixed byte Reels[8*24]; }
[StructLayout(LayoutKind.Sequential)] internal struct AmberCoinChannelNative { internal uint Index,Enabled,Value,LockoutInvert,Reserved; }
[StructLayout(LayoutKind.Sequential)] internal struct AmberCoinRouteNative { internal uint Index,Enabled,CounterIn,CounterOut,PortIndex,CoinCode,Level,FullLevel; }
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct AmberCoinsNative
{
    internal uint Size, Version, ChannelMask, RouteMask;
    internal uint CommunicationStyle, CommunicationInvert, PulseCycles, EdcEnabled;
    internal fixed byte Channels[6 * 20];
    internal fixed byte Routes[8 * 32];
}
[StructLayout(LayoutKind.Sequential)] internal unsafe struct FabricAmberConfigurationNative { internal uint Magic,Size,Version,Flags; internal AmberReelsNative Reels; internal AmberCoinsNative Coins; internal uint Percentage; internal fixed uint Reserved[3]; }
[StructLayout(LayoutKind.Sequential)] internal struct FabricAmberMpu5ReelConfigNative { internal uint ReelIndex,Steps,OptoStart,OptoEnd,OptoInvert; }
[StructLayout(LayoutKind.Sequential)] internal unsafe struct FabricAmberMpu5ReelConfigurationNative { internal uint Size,Version,Count,ApplyMask; internal fixed byte Reels[8*20]; }
[StructLayout(LayoutKind.Sequential)] internal struct FabricAmberMpu5CoinChannelConfigNative { internal uint ChannelIndex,Enabled,Value,LockoutInvert,Reserved; }
[StructLayout(LayoutKind.Sequential)] internal unsafe struct FabricAmberMpu5CoinConfigurationNative { internal uint Size,Version,Count,ApplyMask,CommunicationStyle,CommunicationInvert,PulseCycles,EdcEnabled; internal fixed byte Channels[6*20]; }
[StructLayout(LayoutKind.Sequential)] internal unsafe struct FabricAmberMpu5OptionsNative { internal uint Size,Version,ApplyMask,DipSwitchBits,Stake,Prize,Percentage,CharacteriserAddress,PicMode,SecFitted,HopperType,ReelJumperProfile0,ReelJumperProfile1; internal fixed uint Reserved[2]; }
[StructLayout(LayoutKind.Sequential)] internal struct FabricAmberMpu5ConfigurationNative { internal uint Magic,Size,Version,Flags; internal FabricAmberMpu5ReelConfigurationNative Reels; internal FabricAmberMpu5CoinConfigurationNative Coins; internal FabricAmberMpu5OptionsNative Options; }
