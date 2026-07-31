namespace OasisEditor;

[Flags]
public enum FabricCapability : ulong { DigitalInput=1, Lamps=2, Reels=4, CharacterDisplays=8, SegmentDisplays=16, Audio=32 }
public enum FabricRomRole : uint { Other, Program, Sound }

public sealed record FabricRomResource(FabricRomRole Role, uint Slot, string Path);
public interface IFabricBackendConfiguration { byte[] ToNativeBytes(); }
public sealed record FabricLaunchRequest(string BackendKind, string MachineIdentifier, string BackendPath,
    IReadOnlyList<FabricRomResource> RomResources, IFabricBackendConfiguration? Configuration = null);
public sealed record FabricInput(string Identifier, int NumericalIndex, bool Active);
public readonly record struct FabricCapabilities(ulong Flags) { public bool Has(FabricCapability value) => (Flags & (ulong)value) != 0; }
public readonly record struct FabricLamp(string Identifier, int NumericalIndex, bool LogicalState, float Brightness);
public readonly record struct FabricReel(string Identifier, int NumericalIndex, int Position);
public sealed record FabricCharacterDisplay(string Identifier, uint[] Characters, byte[] Attributes, float Brightness);
public sealed record FabricSegmentDisplay(string Identifier, ulong[] SegmentMasks);
public sealed record FabricMachineSnapshot(ulong Sequence, IReadOnlyList<FabricLamp> Lamps, IReadOnlyList<FabricReel> Reels,
    IReadOnlyList<FabricCharacterDisplay> CharacterDisplays, IReadOnlyList<FabricSegmentDisplay> SegmentDisplays);
public readonly record struct FabricAudioFormat(uint SampleRate, ushort ChannelCount, ushort BitsPerSample,
    bool Interleaved, bool SignedSamples, bool LittleEndian);

public interface IFabricRuntimeLibrary : IDisposable { IFabricMachineSession CreateSession(FabricLaunchRequest request); }
public interface IFabricMachineSession : IDisposable
{
    FabricCapabilities Capabilities { get; }
    void Initialise(); void Reset(); void Advance(ulong elapsedNanoseconds); void SubmitInput(FabricInput input);
    FabricMachineSnapshot GetSnapshot(); FabricAudioFormat GetAudioFormat(); int ReadAudio(Span<short> samples, int frameCapacity); void Shutdown();
}

public enum FabricResult { Ok, InvalidArgument, UnsupportedVersion, NotFound, InvalidState, BufferTooSmall, NotSupported, BackendError, InternalError }
public sealed class FabricException : Exception
{
    public FabricException(FabricResult result, string operation, string? detail = null, Exception? inner = null)
        : base($"{operation} failed with Fabric result {(int)result} ({result}){(string.IsNullOrEmpty(detail) ? string.Empty : $":{Environment.NewLine}{detail}")}", inner)
    {
        Result = result;
        Operation = operation;
    }
    public FabricResult Result { get; }
    public string Operation { get; }
}
