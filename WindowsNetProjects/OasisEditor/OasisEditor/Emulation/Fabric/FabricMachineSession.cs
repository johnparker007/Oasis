using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace OasisEditor;

internal sealed unsafe class FabricMachineSession : IFabricMachineSession
{
    private const int MaxLamps = 4096;
    private const int MaxReels = 64;
    private const int MaxCharacterDisplays = 64;
    private const int MaxSegmentDisplays = 256;

    private readonly FabricNativeExports _exports;
    private readonly Action _released;
    private FabricLampNative[] _lamps = [];
    private FabricReelNative[] _reels = [];
    private FabricCharacterDisplayNative[] _characters = [];
    private FabricSegmentDisplayNative[] _segments = [];
    private nint _handle;
    private ushort? _audioChannels;
    private bool _shutdown;
    private bool _ownsHandle;

    private FabricMachineSession(nint handle, FabricNativeExports exports, Action released)
    {
        _handle = handle;
        _exports = exports;
        _released = released;
    }

    internal static FabricMachineSession Create(nint handle, FabricNativeExports exports, Action released)
    {
        var session = new FabricMachineSession(handle, exports, released);
        session.Capabilities = session.QueryCapabilities();
        return session;
    }

    internal void ActivateOwnership() => _ownsHandle = true;

    public FabricCapabilities Capabilities { get; private set; }

    public void Initialise() => Check(_exports.Initialise(_handle), "FabricSessionInitialise");

    public void Reset() => Check(_exports.Reset(_handle), "FabricSessionReset");

    public void Advance(ulong elapsedNanoseconds)
    {
        Check(_exports.AdvanceFn(_handle, elapsedNanoseconds), "FabricSessionAdvance");
    }

    public FabricResult SubmitInput(FabricInput input)
    {
        var native = new FabricInputNative
        {
            Size = (uint)sizeof(FabricInputNative),
            Version = FabricAbi.Version,
            NumericalIndex = input.NumericalIndex,
            Kind = input.Kind,
            Active = input.Active ? (byte)1 : (byte)0,
            CoinChannel = input.CoinChannel,
            CoinValue = input.CoinValue
        };
        byte* identifier = native.Identifier;
        FabricRuntimeLibrary.WriteFixed(input.Identifier, identifier, FabricAbi.IdentifierCapacity);
        var result = _exports.SubmitInputFn(_handle, &native);
        if (result is not (FabricResult.Ok or FabricResult.InputRejected))
            Check(result, "FabricSessionSubmitInput");
        return result;
    }

    public FabricAudioFormat GetAudioFormat()
    {
        var native = new FabricAudioFormatNative
        {
            Size = (uint)sizeof(FabricAudioFormatNative),
            Version = FabricAbi.Version
        };
        Check(_exports.GetAudioFormatFn(_handle, &native), "FabricSessionGetAudioFormat");
        _audioChannels = native.Channels;
        return new(native.SampleRate, native.Channels, native.BitsPerSample,
            native.Interleaved != 0, native.Signed != 0, native.LittleEndian != 0);
    }

    /// <summary>Reads multi-channel frames into an interleaved sample span.</summary>
    public int ReadAudio(Span<short> samples, int frameCapacity)
    {
        if (frameCapacity < 0)
            throw new ArgumentOutOfRangeException(nameof(frameCapacity));
        var channels = _audioChannels ?? throw new InvalidOperationException("GetAudioFormat must be called before ReadAudio.");
        int requiredSamples;
        try
        {
            requiredSamples = checked(frameCapacity * channels);
        }
        catch (OverflowException exception)
        {
            throw new ArgumentOutOfRangeException(nameof(frameCapacity), frameCapacity,
                $"Audio sample capacity overflowed: {exception.Message}");
        }
        if (samples.Length < requiredSamples)
            throw new ArgumentException($"Audio span contains {samples.Length} samples but {requiredSamples} are required.", nameof(samples));

        fixed (short* pointer = samples)
        {
            Check(_exports.ReadAudioFn(_handle, pointer, (uint)frameCapacity, out var written), "FabricSessionReadAudio");
            if (written > (uint)frameCapacity)
                throw new InvalidDataException("Fabric returned more audio frames than requested.");
            return checked((int)written);
        }
    }

    public FabricMachineSnapshot GetSnapshot()
    {
        for (var attempt = 0; attempt < 4; attempt++)
        {
            fixed (FabricLampNative* lamps = _lamps)
            fixed (FabricReelNative* reels = _reels)
            fixed (FabricCharacterDisplayNative* characters = _characters)
            fixed (FabricSegmentDisplayNative* segments = _segments)
            {
                var native = new FabricMachineSnapshotNative
                {
                    Size = (uint)sizeof(FabricMachineSnapshotNative),
                    Version = FabricAbi.Version,
                    Lamps = (nint)lamps,
                    LampCapacity = (uint)_lamps.Length,
                    Reels = (nint)reels,
                    ReelCapacity = (uint)_reels.Length,
                    Characters = (nint)characters,
                    CharacterCapacity = (uint)_characters.Length,
                    Segments = (nint)segments,
                    SegmentCapacity = (uint)_segments.Length
                };
                var result = _exports.GetSnapshotFn(_handle, &native);
                if (result == FabricResult.BufferTooSmall)
                {
                    GrowBuffers(native);
                    continue;
                }
                Check(result, "FabricSessionGetSnapshot");
                ValidateSuccessfulSnapshot(native, (nint)lamps, (nint)reels, (nint)characters, (nint)segments);
                return ConvertSnapshot(native);
            }
        }
        throw new InvalidDataException("Fabric snapshot capacities did not stabilise after four attempts.");
    }

    public void Shutdown()
    {
        if (_handle == 0 || _shutdown)
            return;
        Check(_exports.Shutdown(_handle), "FabricSessionShutdown");
        _shutdown = true;
    }

    public void Dispose()
    {
        if (_handle == 0 || !_ownsHandle)
            return;
        try
        {
            if (!_shutdown)
                Shutdown();
        }
        catch
        {
            // Dispose and finalization must still destroy the opaque handle.
        }
        finally
        {
            try { _exports.DestroySessionFn(_handle); } catch { }
            _handle = 0;
            _released();
        }
        GC.SuppressFinalize(this);
    }

    ~FabricMachineSession() => Dispose();

    private FabricCapabilities QueryCapabilities()
    {
        var native = new FabricCapabilitiesNative
        {
            Size = (uint)sizeof(FabricCapabilitiesNative),
            Version = FabricAbi.Version
        };
        Check(_exports.GetCapabilitiesFn(_handle, &native), "FabricSessionGetCapabilities");
        ValidateHeader(native.Size, native.Version, (uint)sizeof(FabricCapabilitiesNative), "capabilities");
        return new(native.Flags);
    }

    private void GrowBuffers(FabricMachineSnapshotNative snapshot)
    {
        _lamps = Grow(_lamps, snapshot.LampCount, MaxLamps, "lamps");
        _reels = Grow(_reels, snapshot.ReelCount, MaxReels, "reels");
        _characters = Grow(_characters, snapshot.CharacterCount, MaxCharacterDisplays, "character displays");
        _segments = Grow(_segments, snapshot.SegmentCount, MaxSegmentDisplays, "segment displays");
        for (var index = 0; index < _lamps.Length; index++)
        {
            _lamps[index].Size = (uint)sizeof(FabricLampNative);
            _lamps[index].Version = FabricAbi.Version;
        }
        for (var index = 0; index < _reels.Length; index++)
        {
            _reels[index].Size = (uint)sizeof(FabricReelNative);
            _reels[index].Version = FabricAbi.Version;
        }
        for (var index = 0; index < _characters.Length; index++)
        {
            _characters[index].Size = (uint)sizeof(FabricCharacterDisplayNative);
            _characters[index].Version = FabricAbi.Version;
            _characters[index].Capacity = FabricAbi.CharacterCapacity;
        }
        for (var index = 0; index < _segments.Length; index++)
        {
            _segments[index].Size = (uint)sizeof(FabricSegmentDisplayNative);
            _segments[index].Version = FabricAbi.Version;
            _segments[index].Capacity = FabricAbi.SegmentCapacity;
        }
    }

    private static T[] Grow<T>(T[] current, uint required, int maximum, string name)
    {
        if (required > maximum)
            throw new InvalidDataException($"Fabric requested {required} {name}; managed safety limit is {maximum}.");
        if (required <= current.Length)
            return current;
        var geometric = Math.Max(4, current.Length * 2);
        return new T[Math.Min(maximum, Math.Max(checked((int)required), geometric))];
    }

    private void ValidateSuccessfulSnapshot(
        FabricMachineSnapshotNative snapshot,
        nint expectedLamps,
        nint expectedReels,
        nint expectedCharacters,
        nint expectedSegments)
    {
        ValidateHeader(snapshot.Size, snapshot.Version, (uint)sizeof(FabricMachineSnapshotNative), "snapshot");
        ValidateTopLevel(snapshot.LampCount, snapshot.LampCapacity, snapshot.Lamps, expectedLamps, _lamps.Length, "lamp");
        ValidateTopLevel(snapshot.ReelCount, snapshot.ReelCapacity, snapshot.Reels, expectedReels, _reels.Length, "reel");
        ValidateTopLevel(snapshot.CharacterCount, snapshot.CharacterCapacity, snapshot.Characters, expectedCharacters, _characters.Length, "character display");
        ValidateTopLevel(snapshot.SegmentCount, snapshot.SegmentCapacity, snapshot.Segments, expectedSegments, _segments.Length, "segment display");

        for (var index = 0; index < snapshot.LampCount; index++)
            ValidateHeader(_lamps[index].Size, _lamps[index].Version, (uint)sizeof(FabricLampNative), $"lamp {index}");
        for (var index = 0; index < snapshot.ReelCount; index++)
            ValidateHeader(_reels[index].Size, _reels[index].Version, (uint)sizeof(FabricReelNative), $"reel {index}");
        for (var index = 0; index < snapshot.CharacterCount; index++)
        {
            ref var display = ref _characters[index];
            ValidateHeader(display.Size, display.Version, (uint)sizeof(FabricCharacterDisplayNative), $"character display {index}");
            if (display.Capacity is 0 or > FabricAbi.CharacterCapacity || display.Count > display.Capacity)
                throw new InvalidDataException($"Fabric character display {index} returned invalid count/capacity {display.Count}/{display.Capacity}.");
        }
        for (var index = 0; index < snapshot.SegmentCount; index++)
        {
            ref var display = ref _segments[index];
            ValidateHeader(display.Size, display.Version, (uint)sizeof(FabricSegmentDisplayNative), $"segment display {index}");
            if (display.Capacity is 0 or > FabricAbi.SegmentCapacity || display.Count > display.Capacity)
                throw new InvalidDataException($"Fabric segment display {index} returned invalid count/capacity {display.Count}/{display.Capacity}.");
        }
    }

    private static void ValidateTopLevel(uint count, uint capacity, nint pointer, nint expectedPointer, int managedCapacity, string name)
    {
        if (capacity > managedCapacity || count > capacity)
            throw new InvalidDataException($"Fabric {name} count/capacity {count}/{capacity} exceeds the supplied buffer.");
        if (count != 0 && pointer == 0)
            throw new InvalidDataException($"Fabric returned {count} {name} entries with a null pointer.");
        if (pointer != expectedPointer)
            throw new InvalidDataException($"Fabric replaced the caller-owned {name} buffer pointer.");
    }

    private static void ValidateHeader(uint size, uint version, uint expectedSize, string name)
    {
        if (size < expectedSize || version != FabricAbi.Version)
            throw new InvalidDataException($"Fabric {name} returned invalid size/version {size}/0x{version:X8}.");
    }

    private FabricMachineSnapshot ConvertSnapshot(FabricMachineSnapshotNative snapshot)
    {
        var lamps = new List<FabricLamp>((int)snapshot.LampCount);
        for (var index = 0; index < snapshot.LampCount; index++)
        {
            ref var lamp = ref _lamps[index];
            fixed (byte* identifier = lamp.Identifier)
                lamps.Add(new(ReadIdentifier(identifier), lamp.Index, lamp.LogicalState != 0, lamp.Brightness));
        }

        var reels = new List<FabricReel>((int)snapshot.ReelCount);
        for (var index = 0; index < snapshot.ReelCount; index++)
        {
            ref var reel = ref _reels[index];
            fixed (byte* identifier = reel.Identifier)
                reels.Add(new(ReadIdentifier(identifier), reel.Index, reel.Position));
        }

        var characters = new List<FabricCharacterDisplay>((int)snapshot.CharacterCount);
        for (var index = 0; index < snapshot.CharacterCount; index++)
        {
            ref var display = ref _characters[index];
            characters.Add(ConvertCharacterDisplay(ref display, index));
        }

        var segments = new List<FabricSegmentDisplay>((int)snapshot.SegmentCount);
        for (var index = 0; index < snapshot.SegmentCount; index++)
        {
            ref var display = ref _segments[index];
            var masks = new ulong[display.Count];
            fixed (ulong* source = display.Masks)
                new ReadOnlySpan<ulong>(source, masks.Length).CopyTo(masks);
            fixed (byte* identifier = display.Identifier)
                segments.Add(new(ReadIdentifier(identifier), masks));
        }
        return new(snapshot.Sequence, lamps, reels, characters, segments);
    }

    internal static FabricCharacterDisplay ConvertCharacterDisplay(
        ref FabricCharacterDisplayNative display,
        int displayIndex = 0)
    {
        if (!float.IsFinite(display.Brightness))
            throw new InvalidDataException($"Fabric character display {displayIndex} returned non-finite brightness.");
        var values = new uint[display.Count];
        var attributes = new byte[display.Count];
        fixed (uint* source = display.Characters)
            new ReadOnlySpan<uint>(source, values.Length).CopyTo(values);
        fixed (byte* source = display.Attributes)
            new ReadOnlySpan<byte>(source, attributes.Length).CopyTo(attributes);
        fixed (byte* identifier = display.Identifier)
            return new(ReadIdentifier(identifier), values, attributes, display.Brightness);
    }

    private static string ReadIdentifier(byte* pointer)
    {
        var bytes = new ReadOnlySpan<byte>(pointer, FabricAbi.IdentifierCapacity);
        var terminator = bytes.IndexOf((byte)0);
        var bounded = terminator < 0 ? bytes : bytes[..terminator];
        try
        {
            return new UTF8Encoding(false, true).GetString(bounded);
        }
        catch (DecoderFallbackException exception)
        {
            throw new InvalidDataException("Fabric returned an invalid UTF-8 identifier.", exception);
        }
    }

    private void Check(FabricResult result, string operation)
    {
        if (result != FabricResult.Ok)
            throw FabricRuntimeLibrary.Error(result, operation, _handle, true, _exports);
    }
}
