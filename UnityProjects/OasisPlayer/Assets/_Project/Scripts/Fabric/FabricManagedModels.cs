using System;
using System.Collections.Generic;

namespace OasisPlayer.Fabric
{
    [Flags]
    public enum FabricCapability : ulong { DigitalInput = 1, Lamps = 2, Reels = 4, CharacterDisplays = 8, SegmentDisplays = 16, Audio = 32, CoinInput = 1UL << 6, DotMatrixDisplays = 1UL << 7 }
    public enum FabricRomRole : uint { Other, Program, Sound }

    public sealed class FabricRomResource
    {
        public FabricRomResource(FabricRomRole role, uint slot, string path) { Role = role; Slot = slot; Path = path; }
        public FabricRomRole Role { get; private set; }
        public uint Slot { get; private set; }
        public string Path { get; private set; }
    }

    public interface IFabricBackendConfiguration { byte[] ToNativeBytes(); }

    public sealed class FabricByteConfiguration : IFabricBackendConfiguration
    {
        private readonly byte[] _bytes;
        public FabricByteConfiguration(byte[] bytes)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            _bytes = (byte[])bytes.Clone();
        }
        public byte[] ToNativeBytes() { return (byte[])_bytes.Clone(); }
    }

    public sealed class FabricLaunchRequest
    {
        public FabricLaunchRequest(string backendKind, string machineIdentifier, string backendPath, IReadOnlyList<FabricRomResource> romResources, IFabricBackendConfiguration configuration = null)
        { BackendKind = backendKind; MachineIdentifier = machineIdentifier; BackendPath = backendPath; RomResources = romResources; Configuration = configuration; }
        public string BackendKind { get; private set; }
        public string MachineIdentifier { get; private set; }
        public string BackendPath { get; private set; }
        public IReadOnlyList<FabricRomResource> RomResources { get; private set; }
        public IFabricBackendConfiguration Configuration { get; private set; }
    }

    public sealed class FabricInput
    {
        public FabricInput(string identifier, int numericalIndex, FabricInputKind kind, bool active, byte coinChannel = 0, byte coinValue = 0)
        { Identifier = identifier; NumericalIndex = numericalIndex; Kind = kind; Active = active; CoinChannel = coinChannel; CoinValue = coinValue; }
        public string Identifier { get; private set; }
        public int NumericalIndex { get; private set; }
        public FabricInputKind Kind { get; private set; }
        public bool Active { get; private set; }
        public byte CoinChannel { get; private set; }
        public byte CoinValue { get; private set; }
    }

    public struct FabricCapabilities
    {
        public FabricCapabilities(ulong flags) { Flags = flags; }
        public ulong Flags { get; private set; }
        public bool Has(FabricCapability value) { return (Flags & (ulong)value) != 0; }
    }
    public struct FabricLamp
    {
        public FabricLamp(string identifier, int numericalIndex, bool logicalState, float brightness) { Identifier = identifier; NumericalIndex = numericalIndex; LogicalState = logicalState; Brightness = brightness; }
        public string Identifier { get; private set; } public int NumericalIndex { get; private set; } public bool LogicalState { get; private set; } public float Brightness { get; private set; }
    }
    public struct FabricReel
    {
        public FabricReel(string identifier, int numericalIndex, int position) { Identifier = identifier; NumericalIndex = numericalIndex; Position = position; }
        public string Identifier { get; private set; } public int NumericalIndex { get; private set; } public int Position { get; private set; }
    }
    public sealed class FabricCharacterDisplay
    {
        public FabricCharacterDisplay(string identifier, uint[] characters, byte[] attributes, float brightness) { Identifier = identifier; Characters = characters; Attributes = attributes; Brightness = brightness; }
        public string Identifier { get; private set; } public uint[] Characters { get; private set; } public byte[] Attributes { get; private set; } public float Brightness { get; private set; }
    }
    public sealed class FabricSegmentDisplay
    {
        public FabricSegmentDisplay(string identifier, ulong[] segmentMasks) { Identifier = identifier; SegmentMasks = segmentMasks; }
        public string Identifier { get; private set; } public ulong[] SegmentMasks { get; private set; }
    }
    public sealed class FabricDotMatrixDisplay
    {
        public FabricDotMatrixDisplay(string identifier, int width, int height, int[] dots, float brightness) { Identifier = identifier; Width = width; Height = height; Dots = dots; Brightness = brightness; }
        public string Identifier { get; private set; } public int Width { get; private set; } public int Height { get; private set; } public int[] Dots { get; private set; } public float Brightness { get; private set; }
    }
    public sealed class FabricMachineSnapshot
    {
        public FabricMachineSnapshot(ulong sequence, IReadOnlyList<FabricLamp> lamps, IReadOnlyList<FabricReel> reels, IReadOnlyList<FabricCharacterDisplay> characterDisplays, IReadOnlyList<FabricSegmentDisplay> segmentDisplays, IReadOnlyList<FabricDotMatrixDisplay> dotMatrixDisplays = null)
        { Sequence = sequence; Lamps = lamps; Reels = reels; CharacterDisplays = characterDisplays; SegmentDisplays = segmentDisplays; DotMatrixDisplays = dotMatrixDisplays; }
        public ulong Sequence { get; private set; } public IReadOnlyList<FabricLamp> Lamps { get; private set; } public IReadOnlyList<FabricReel> Reels { get; private set; } public IReadOnlyList<FabricCharacterDisplay> CharacterDisplays { get; private set; } public IReadOnlyList<FabricSegmentDisplay> SegmentDisplays { get; private set; } public IReadOnlyList<FabricDotMatrixDisplay> DotMatrixDisplays { get; private set; }
    }
    public struct FabricAudioFormat
    {
        public FabricAudioFormat(uint sampleRate, ushort channelCount, ushort bitsPerSample, bool interleaved, bool signedSamples, bool littleEndian) { SampleRate = sampleRate; ChannelCount = channelCount; BitsPerSample = bitsPerSample; Interleaved = interleaved; SignedSamples = signedSamples; LittleEndian = littleEndian; }
        public uint SampleRate { get; private set; } public ushort ChannelCount { get; private set; } public ushort BitsPerSample { get; private set; } public bool Interleaved { get; private set; } public bool SignedSamples { get; private set; } public bool LittleEndian { get; private set; }
    }

    public interface IFabricRuntimeLibrary : IDisposable { IFabricMachineSession CreateSession(FabricLaunchRequest request); }
    public interface IFabricMachineSession : IDisposable
    {
        FabricCapabilities Capabilities { get; }
        void Initialise(); void Reset(); void Advance(ulong elapsedNanoseconds); FabricResult SubmitInput(FabricInput input);
        FabricMachineSnapshot GetSnapshot(); FabricAudioFormat GetAudioFormat(); int ReadAudio(Span<short> samples, int frameCapacity); void Shutdown();
    }

    public enum FabricResult { Ok, InvalidArgument, UnsupportedVersion, NotFound, InvalidState, BufferTooSmall, NotSupported, BackendError, InternalError, InputRejected }
    public sealed class FabricException : Exception
    {
        public FabricException(FabricResult result, string operation, string detail = null, Exception inner = null)
            : base(operation + " failed with Fabric result " + (int)result + " (" + result + ")" + (string.IsNullOrEmpty(detail) ? string.Empty : Environment.NewLine + detail), inner) { Result = result; Operation = operation; }
        public FabricResult Result { get; private set; } public string Operation { get; private set; }
    }
}
