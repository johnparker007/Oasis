namespace OasisEditor;

public sealed partial class AuthoringSession
{
    internal int RenderRevision => _document is null ? 0 : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(_document);
    private Panel2DDocumentFile? _renderVersion;
    private Panel2DDocumentModel? _renderModel;
    /// <summary>Returns the full immutable authored model consumed by specialist renderers.</summary>
    public Panel2DDocumentModel? RenderDocument
    {
        get
        {
            if(_document is null)return null;
            if(!ReferenceEquals(_renderVersion,_document))
            {_renderModel=PanelHierarchy.ResolveDocument(Panel2DDocumentStorage.ToModel(_document));_renderVersion=_document;}
            return _renderModel;
        }
    }
}

/// <summary>Immutable view of real emulator outputs for a render frame.</summary>
public sealed class RuntimeOutputSnapshot
{
    public RuntimeOutputSnapshot(IReadOnlyDictionary<string, double>? lampIntensities = null,
        IReadOnlyDictionary<string, double>? reelPositions = null,
        IReadOnlyDictionary<string, double>? temporaryReelOffsets = null,
        IReadOnlyDictionary<string, IReadOnlyList<int>>? segmentMasks = null,
        IReadOnlyDictionary<string, IReadOnlyList<double>>? segmentBrightness = null,
        IReadOnlyDictionary<string, IReadOnlyList<int>>? vfdDots = null,
        FruitMachinePlatformType platform = FruitMachinePlatformType.None)
    {
        LampIntensities = Copy(lampIntensities);
        ReelPositions = Copy(reelPositions);
        TemporaryReelOffsets = Copy(temporaryReelOffsets);
        SegmentMasks = CopyLists(segmentMasks);
        SegmentBrightness = CopyLists(segmentBrightness);
        VfdDots = CopyLists(vfdDots);
        Platform = platform;
        Fingerprint = ComputeFingerprint();
    }

    public IReadOnlyDictionary<string, double> LampIntensities { get; }
    public IReadOnlyDictionary<string, double> ReelPositions { get; }
    public IReadOnlyDictionary<string, double> TemporaryReelOffsets { get; }
    public IReadOnlyDictionary<string, IReadOnlyList<int>> SegmentMasks { get; }
    public IReadOnlyDictionary<string, IReadOnlyList<double>> SegmentBrightness { get; }
    public IReadOnlyDictionary<string, IReadOnlyList<int>> VfdDots { get; }
    public FruitMachinePlatformType Platform { get; }
    public int Fingerprint { get; }

    public static RuntimeOutputSnapshot Empty { get; } = new();

    private static IReadOnlyDictionary<string, double> Copy(IReadOnlyDictionary<string, double>? values)
        => new System.Collections.ObjectModel.ReadOnlyDictionary<string, double>(
            values is null ? new Dictionary<string, double>() : new Dictionary<string, double>(values, StringComparer.Ordinal));

    private static IReadOnlyDictionary<string, IReadOnlyList<T>> CopyLists<T>(IReadOnlyDictionary<string, IReadOnlyList<T>>? values)
    {
        var copy = values is null ? new Dictionary<string, IReadOnlyList<T>>()
            : values.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<T>)Array.AsReadOnly(pair.Value.ToArray()), StringComparer.Ordinal);
        return new System.Collections.ObjectModel.ReadOnlyDictionary<string, IReadOnlyList<T>>(copy);
    }

    private int ComputeFingerprint()
    {
        var hash = new HashCode();
        hash.Add(Platform);
        AddDoubles(LampIntensities); AddDoubles(ReelPositions); AddDoubles(TemporaryReelOffsets);
        AddLists(SegmentMasks); AddLists(SegmentBrightness); AddLists(VfdDots);
        return hash.ToHashCode();

        void AddDoubles(IReadOnlyDictionary<string, double> values)
        { foreach (var item in values.OrderBy(pair => pair.Key, StringComparer.Ordinal)) { hash.Add(item.Key, StringComparer.Ordinal); hash.Add(item.Value); } }
        void AddLists<T>(IReadOnlyDictionary<string, IReadOnlyList<T>> values)
        { foreach (var item in values.OrderBy(pair => pair.Key, StringComparer.Ordinal)) { hash.Add(item.Key, StringComparer.Ordinal); foreach (var value in item.Value) hash.Add(value); } }
    }
}
