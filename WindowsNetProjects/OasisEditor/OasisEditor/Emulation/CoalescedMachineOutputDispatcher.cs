namespace OasisEditor;

internal sealed class CoalescedMachineOutputDispatcher
{
    private readonly Action<Action> _schedule;
    private readonly Action<MachineOutputBatch> _apply;
    private readonly object _gate = new();
    private readonly Dictionary<int, int> _lamps = [];
    private readonly Dictionary<int, int> _reels = [];
    private readonly Dictionary<SegmentKey, SegmentValue> _segments = [];
    private readonly Dictionary<int, double> _vfdBrightness = [];
    private bool _dispatchPending;
    private bool _detached;

    public CoalescedMachineOutputDispatcher(Action<Action> schedule, Action<MachineOutputBatch> apply)
    {
        _schedule = schedule ?? throw new ArgumentNullException(nameof(schedule));
        _apply = apply ?? throw new ArgumentNullException(nameof(apply));
    }

    public int PendingEntryCount { get { lock (_gate) return _lamps.Count + _reels.Count + _segments.Count + _vfdBrightness.Count; } }
    public bool DispatchPending { get { lock (_gate) return _dispatchPending; } }

    public void EnqueueLamp(int id, int value) { lock (_gate) { if (_detached) return; _lamps[id] = value; ScheduleLocked(); } }
    public void EnqueueReel(int id, int value) { lock (_gate) { if (_detached) return; _reels[id] = value; ScheduleLocked(); } }
    public void EnqueueSegment(int id, int mask, SegmentOutputType type) { lock (_gate) { if (_detached) return; _segments[new(id, type)] = new(id, mask, type); ScheduleLocked(); } }
    public void EnqueueVfdBrightness(int id, double value) { lock (_gate) { if (_detached) return; _vfdBrightness[id] = value; ScheduleLocked(); } }

    public void Detach()
    {
        lock (_gate)
        {
            _detached = true;
            _lamps.Clear(); _reels.Clear(); _segments.Clear(); _vfdBrightness.Clear();
        }
    }

    private void ScheduleLocked()
    {
        if (_dispatchPending) return;
        _dispatchPending = true;
        _schedule(ApplyPending);
    }

    private void ApplyPending()
    {
        MachineOutputBatch batch;
        lock (_gate)
        {
            if (_detached) { _dispatchPending = false; return; }
            batch = new(
                _lamps.Select(kv => new LampValue(kv.Key, kv.Value)).ToArray(),
                _reels.Select(kv => new ReelValue(kv.Key, kv.Value)).ToArray(),
                _segments.Values.ToArray(),
                _vfdBrightness.Select(kv => new VfdBrightnessValue(kv.Key, kv.Value)).ToArray());
            _lamps.Clear(); _reels.Clear(); _segments.Clear(); _vfdBrightness.Clear();
        }
        _apply(batch);
        lock (_gate)
        {
            _dispatchPending = false;
            if (!_detached && (_lamps.Count != 0 || _reels.Count != 0 || _segments.Count != 0 || _vfdBrightness.Count != 0))
                ScheduleLocked();
        }
    }

    private readonly record struct SegmentKey(int CellId, SegmentOutputType OutputType);
}

internal sealed record MachineOutputBatch(LampValue[] Lamps, ReelValue[] Reels, SegmentValue[] Segments, VfdBrightnessValue[] VfdBrightness);
internal readonly record struct LampValue(int Id, int Value);
internal readonly record struct ReelValue(int Id, int Value);
internal readonly record struct SegmentValue(int Id, int Mask, SegmentOutputType OutputType);
internal readonly record struct VfdBrightnessValue(int Id, double Value);
