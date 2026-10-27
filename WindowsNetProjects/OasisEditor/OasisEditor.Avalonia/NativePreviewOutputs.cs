using OasisEditor.SegmentDisplays;

namespace OasisEditor.Avalonia;

/// <summary>Captures the latest real Fabric output events as immutable renderer snapshots.</summary>
public sealed class NativePreviewOutputs : IDisposable
{
    private const int MaxLampIds = 4096;
    private const int MaxReelIds = 256;
    private const int MaxSegmentCells = 65536;
    private const int MaxDotMatrixDisplays = 256;
    private const int CanonicalReelPositions = 96;
    private const int AlphaCellCount = 16;
    private const int DotMatrixWidth = 96;
    private const int DotMatrixHeight = 8;
    private readonly object _gate = new();
    private readonly IEmulationBackend _backend;
    private readonly MachineDocument _machine;
    private readonly BoundedLatestDictionary<int> _lamps = new(MaxLampIds);
    private readonly BoundedLatestDictionary<(int Position, ReelPositionConvention Convention)> _reels = new(MaxReelIds);
    private readonly BoundedLatestDictionary<int> _digitMasks = new(MaxSegmentCells);
    private readonly BoundedLatestDictionary<int> _alphaMasks = new(MaxSegmentCells);
    private readonly BoundedLatestDictionary<double> _alphaBrightness = new(MaxSegmentCells);
    private readonly BoundedLatestDictionary<int[]> _dotMatrices = new(MaxDotMatrixDisplays);
    private bool _disposed;

    public NativePreviewOutputs(IEmulationBackend backend, MachineDocument machine)
    {
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        _machine = machine ?? throw new ArgumentNullException(nameof(machine));
        _backend.LampChanged += OnLampChanged;
        _backend.ReelChanged += OnReelChanged;
        _backend.SegmentChanged += OnSegmentChanged;
        _backend.VfdBrightnessChanged += OnVfdBrightnessChanged;
        _backend.DotMatrixChanged += OnDotMatrixChanged;
    }

    public int LampCount { get { lock (_gate) return _lamps.Count; } }
    public int ReelCount { get { lock (_gate) return _reels.Count; } }
    public int SegmentCount { get { lock (_gate) return _digitMasks.Count + _alphaMasks.Count; } }

    /// <summary>Returns a self-contained copy of currently observed output values.</summary>
    public RuntimeOutputSnapshot Capture(Panel2DDocumentModel? panel, FaceDocumentModel? face)
    {
        Dictionary<int, int> lamps;
        Dictionary<int, (int Position, ReelPositionConvention Convention)> reels;
        Dictionary<int, int> digitMasks;
        Dictionary<int, int> alphaMasks;
        Dictionary<int, double> brightness;
        Dictionary<int, int[]> dots;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            lamps = _lamps.ToDictionary();
            reels = _reels.ToDictionary();
            digitMasks = _digitMasks.ToDictionary();
            alphaMasks = _alphaMasks.ToDictionary();
            brightness = _alphaBrightness.ToDictionary();
            dots = _dotMatrices.ToDictionary().ToDictionary(item => item.Key, item => item.Value.ToArray());
        }

        var lampOutputs = new Dictionary<string, double>(StringComparer.Ordinal);
        var reelOutputs = new Dictionary<string, double>(StringComparer.Ordinal);
        var segmentOutputs = new Dictionary<string, IReadOnlyList<int>>(StringComparer.Ordinal);
        var brightnessOutputs = new Dictionary<string, IReadOnlyList<double>>(StringComparer.Ordinal);
        var dotOutputs = new Dictionary<string, IReadOnlyList<int>>(StringComparer.Ordinal);
        var platform = _machine.EmulationRuntime.Platform;

        if (panel is not null)
        {
            foreach (var element in panel.Elements)
            {
                if (string.IsNullOrWhiteSpace(element.ObjectId)) continue;
                switch (element.Kind)
                {
                    case PanelElementKind.Lamp when element.DisplayNumber is int lampId && lamps.TryGetValue(lampId, out var rawLamp):
                        lampOutputs[element.ObjectId] = NormalizeLamp(rawLamp);
                        lampOutputs[MachineObjectReference.Lamp(lampId).ToString()] = NormalizeLamp(rawLamp);
                        break;
                    case PanelElementKind.Reel when element.DisplayNumber is int reelId && reels.TryGetValue(reelId, out var reel):
                        var canonical = NormalizeBackendReelPosition(reel.Convention, reel.Position, ResolveReelPositionCount(reelId));
                        reelOutputs[MachineObjectReference.Reel(reelId).ToString()] = canonical;
                        if (element.Stops is > 0)
                        {
                            var effective = ResolveEffectiveReelPosition(canonical, element.Stops.Value,
                                element.IsReversed == true, element.BandOffset ?? 0d, platform);
                            reelOutputs[element.ObjectId] = effective;
                        }
                        break;
                    case PanelElementKind.Alpha:
                    {
                        var baseCell = element.DisplayNumber.GetValueOrDefault(0);
                        var masks = BuildAlphaCells(baseCell, element.IsReversed == true, platform, alphaMasks);
                        if (masks is null) break;
                        segmentOutputs[element.ObjectId] = masks;
                        brightnessOutputs[element.ObjectId] = BuildBrightness(baseCell, brightness);
                        var reference = MachineObjectReference.AlphaDisplay(baseCell);
                        segmentOutputs[reference.ToString()] = masks;
                        brightnessOutputs[reference.ToString()] = brightnessOutputs[element.ObjectId];
                        break;
                    }
                    case PanelElementKind.SevenSegment when element.DisplayNumber is int displayId:
                    {
                        if (!digitMasks.TryGetValue(displayId, out var mask)) break;
                        var cells = new[] { mask };
                        var reference = MachineObjectReference.SevenSegmentDisplay(displayId);
                        segmentOutputs[element.ObjectId] = cells;
                        segmentOutputs[reference.ToString()] = cells;
                        brightnessOutputs[reference.ToString()] = [1d];
                        break;
                    }
                    case PanelElementKind.VfdDotMatrix when element.DisplayNumber is int dotDisplayId && dots.TryGetValue(dotDisplayId, out var matrix):
                        dotOutputs[element.ObjectId] = matrix;
                        break;
                }
            }
        }

        if (face is not null)
        {
            foreach (var window in face.Elements.OfType<FaceLampWindowElement>())
            {
                if (window.LinkedMachineObjectReference is not { Kind: MachineObjectKind.Lamp } reference
                    || !TryNumericId(reference, out var lampId) || !lamps.TryGetValue(lampId, out var rawLamp)) continue;
                lampOutputs[reference.ToString()] = NormalizeLamp(rawLamp);
            }

            foreach (var mount in face.Elements.OfType<FaceReelMount>())
            {
                if (mount.LinkedMachineObjectReference is not { Kind: MachineObjectKind.Reel } reference
                    || !TryNumericId(reference, out var reelId) || !reels.TryGetValue(reelId, out var reel)) continue;
                reelOutputs[reference.ToString()] = NormalizeBackendReelPosition(reel.Convention, reel.Position, ResolveReelPositionCount(reelId));
            }

            foreach (var display in face.Elements.OfType<FaceSevenSegmentDisplayElement>())
            {
                if (display.LinkedMachineObjectReference is not { Kind: MachineObjectKind.SevenSegmentDisplay } reference
                    || !TryNumericId(reference, out var baseCell)) continue;
                var count = Math.Max(1, display.DigitCount);
                var cells = new int[count];
                var hasLiveCell = false;
                for (var index = 0; index < count; index++)
                    if (digitMasks.TryGetValue(baseCell + index, out cells[index])) hasLiveCell = true;
                if (!hasLiveCell) continue;
                segmentOutputs[reference.ToString()] = cells;
                brightnessOutputs[reference.ToString()] = Enumerable.Repeat(1d, count).ToArray();
            }

            foreach (var display in face.Elements.OfType<FaceAlphaDisplayElement>())
            {
                if (display.LinkedMachineObjectReference is not { Kind: MachineObjectKind.AlphaDisplay } reference
                    || !TryNumericId(reference, out var baseCell)) continue;
                var masks = BuildAlphaCells(baseCell, display.IsReversed, platform, alphaMasks);
                if (masks is null) continue;
                segmentOutputs[reference.ToString()] = masks;
                brightnessOutputs[reference.ToString()] = BuildBrightness(baseCell, brightness);
            }
        }

        return new RuntimeOutputSnapshot(lampOutputs, reelOutputs, segmentMasks: segmentOutputs,
            segmentBrightness: brightnessOutputs, vfdDots: dotOutputs, platform: platform);
    }

    /// <summary>Discards observed values so a stopped preview renders its authored static state.</summary>
    public void Clear()
    {
        lock (_gate)
        {
            _lamps.Clear(); _reels.Clear(); _digitMasks.Clear(); _alphaMasks.Clear();
            _alphaBrightness.Clear(); _dotMatrices.Clear();
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _lamps.Clear(); _reels.Clear(); _digitMasks.Clear(); _alphaMasks.Clear();
            _alphaBrightness.Clear(); _dotMatrices.Clear();
        }
        _backend.LampChanged -= OnLampChanged;
        _backend.ReelChanged -= OnReelChanged;
        _backend.SegmentChanged -= OnSegmentChanged;
        _backend.VfdBrightnessChanged -= OnVfdBrightnessChanged;
        _backend.DotMatrixChanged -= OnDotMatrixChanged;
    }

    private void OnLampChanged(object? sender, MachineLampChangedEventArgs args)
    { lock (_gate) if (!_disposed) _lamps.Set(args.LampId, args.Value); }

    private void OnReelChanged(object? sender, MachineReelChangedEventArgs args)
    { lock (_gate) if (!_disposed) _reels.Set(args.ReelId, (args.Position, args.Convention)); }

    private void OnSegmentChanged(object? sender, MachineSegmentChangedEventArgs args)
    {
        lock (_gate)
        {
            if (_disposed) return;
            if (args.OutputType == SegmentOutputType.Digit) _digitMasks.Set(args.CellId, args.SegmentMask);
            else _alphaMasks.Set(args.CellId, args.SegmentMask);
        }
    }

    private void OnVfdBrightnessChanged(object? sender, MachineVfdBrightnessChangedEventArgs args)
    { lock (_gate) if (!_disposed) _alphaBrightness.Set(args.CellId, Math.Clamp(args.NormalizedBrightness, 0d, 1d)); }

    private void OnDotMatrixChanged(object? sender, MachineDotMatrixChangedEventArgs args)
    {
        if (args.Width != DotMatrixWidth || args.Height != DotMatrixHeight || args.Dots.Count != DotMatrixWidth * DotMatrixHeight) return;
        var dots = args.Dots.Select(value => value == 0 ? 0 : 1).ToArray();
        lock (_gate) if (!_disposed) _dotMatrices.Set(args.DisplayId, dots);
    }

    private int ResolveReelPositionCount(int reelId)
    {
        var settings = _machine.EmulationRuntime.PlatformSettings;
        var steps = _machine.EmulationRuntime.Platform switch
        {
            FruitMachinePlatformType.MPU5 when settings is Mpu5NativeRomSettings mpu5 => mpu5.Reels.FirstOrDefault(item => item.ReelIndex == reelId)?.Steps ?? System6ReelOptoSettings.DefaultSteps,
            FruitMachinePlatformType.Epoch when settings is EpochNativeRomSettings epoch => epoch.Reels.FirstOrDefault(item => item.ReelIndex == reelId)?.Steps ?? EpochReelSettings.DefaultSteps,
            FruitMachinePlatformType.Scorpion4 when settings is Scorpion4ProjectSettings scorpion => scorpion.Reels.FirstOrDefault(item => item.ReelIndex == reelId)?.Steps ?? System6ReelOptoSettings.DefaultSteps,
            // Machine preview reads its own selected-platform settings. The older WPF editor
            // provider fell back to its separate System6 settings list for MPU3/M1.
            FruitMachinePlatformType.MPU3 when settings is Mpu3ProjectSettings mpu3 => mpu3.Reels.FirstOrDefault(item => item.ReelIndex == reelId)?.Steps ?? Mpu3ReelSettings.DefaultSteps,
            FruitMachinePlatformType.MaygayM1 when settings is M1ProjectSettings m1 => m1.Reels.FirstOrDefault(item => item.ReelIndex == reelId)?.Steps ?? System6ReelOptoSettings.DefaultSteps,
            _ when settings is System6NativeRomSettings system6 => system6.ReelOptos.FirstOrDefault(item => item.ReelIndex == reelId)?.Steps ?? System6ReelOptoSettings.DefaultSteps,
            _ => System6ReelOptoSettings.DefaultSteps
        };
        return steps > 0 ? steps : CanonicalReelPositions;
    }

    private static double NormalizeLamp(int value) => value switch
    { <= 0 => 0d, <= 1 => 1d, _ => Math.Clamp(value / 255d, 0d, 1d) };

    private static double NormalizeBackendReelPosition(ReelPositionConvention convention, int value, int count)
    {
        var safeCount = count > 0 ? count : CanonicalReelPositions;
        var position = Wrap(value, safeCount);
        if (convention == ReelPositionConvention.Amber) position = position == 0 ? 0 : safeCount - position;
        return position * (double)CanonicalReelPositions / safeCount;
    }

    private static double ResolveEffectiveReelPosition(double canonical, int stops, bool reversed, double bandOffset, FruitMachinePlatformType platform)
    {
        var wrapped = ((canonical % CanonicalReelPositions) + CanonicalReelPositions) % CanonicalReelPositions;
        var directionAdjusted = (PlatformReelDirectionResolver.RequiresReversal(platform) ^ reversed) && wrapped != 0
            ? CanonicalReelPositions - wrapped : wrapped;
        var offset = InternalReelOffsetResolver.ResolveNormalizedOffset(platform, stops) + bandOffset;
        return ((directionAdjusted + offset * CanonicalReelPositions) % CanonicalReelPositions + CanonicalReelPositions) % CanonicalReelPositions;
    }

    private static IReadOnlyList<int>? BuildAlphaCells(int baseCell, bool reversed, FruitMachinePlatformType platform, IReadOnlyDictionary<int, int> masks)
    {
        var result = new int[AlphaCellCount];
        var found = false;
        for (var index = 0; index < result.Length; index++)
        {
            var sourceIndex = AlphaCellOrder.SourceIndexForCanonicalCell(index, result.Length, reversed, platform);
            if (masks.TryGetValue(baseCell + sourceIndex, out result[index])) found = true;
        }
        return found ? result : null;
    }

    private static IReadOnlyList<double> BuildBrightness(int baseCell, IReadOnlyDictionary<int, double> brightness)
    {
        var level = brightness.TryGetValue(baseCell, out var value) ? value : 1d;
        return Enumerable.Repeat(level, AlphaCellCount).ToArray();
    }

    private static bool TryNumericId(MachineObjectReference reference, out int id)
        => int.TryParse(reference.Id, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out id);

    private static int Wrap(int value, int count) => ((value % count) + count) % count;

    private sealed class BoundedLatestDictionary<TValue>(int capacity)
    {
        private readonly Dictionary<int, TValue> _values = [];
        private readonly LinkedList<int> _order = [];
        private readonly Dictionary<int, LinkedListNode<int>> _nodes = [];
        public int Count => _values.Count;
        public void Set(int key, TValue value)
        {
            _values[key] = value;
            if (_nodes.Remove(key, out var existing)) _order.Remove(existing);
            _nodes[key] = _order.AddLast(key);
            while (_values.Count > capacity)
            {
                var oldest = _order.First!;
                _order.RemoveFirst();
                _nodes.Remove(oldest.Value);
                _values.Remove(oldest.Value);
            }
        }
        public bool TryGetValue(int key, out TValue value) => _values.TryGetValue(key, out value!);
        public Dictionary<int, TValue> ToDictionary() => new(_values);
        public void Clear() { _values.Clear(); _order.Clear(); _nodes.Clear(); }
    }
}
