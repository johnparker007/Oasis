using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using OasisPlayer.Fabric;

namespace OasisPlayer.RuntimeBuild
{
    /// <summary>One native owner thread. Unity objects are never touched by this worker.</summary>
    public sealed class FabricMachineRunner : IDisposable
    {
        // The installed Amber JPM DLL owns a file-static board, not one board per session.
        private static readonly SemaphoreSlim ImpactLease = new SemaphoreSlim(1, 1);
        private readonly Func<string, IFabricRuntimeLibrary> _factory;
        private readonly ConcurrentQueue<FabricInput> _inputs = new ConcurrentQueue<FabricInput>();
        private readonly AutoResetEvent _wake = new AutoResetEvent(false);
        private readonly object _snapshotGate = new object();
        private readonly HashSet<int> _asserted = new HashSet<int>();
        private readonly Dictionary<string, FabricInput> _coins = new Dictionary<string, FabricInput>(StringComparer.Ordinal);
        private Thread _thread;
        private volatile bool _stop;
        private int _inputCount, _reset;
        private FabricMachineSnapshot _latest;
        private volatile Exception _failure;
        private long _slices, _snapshots, _audioFrames, _submitted, _rejected, _resets;
        public FabricMachineRunner(Func<string, IFabricRuntimeLibrary> factory = null) { _factory = factory ?? (path => new FabricRuntimeLibrary(path)); }
        public FabricAudioFormat AudioFormat { get; private set; }
        public FabricPcmBuffer Audio { get; private set; }
        public Exception Failure { get { return _failure; } }
        public long Slices { get { return Interlocked.Read(ref _slices); } }
        public long SnapshotCount { get { return Interlocked.Read(ref _snapshots); } }
        public long AudioFrames { get { return Interlocked.Read(ref _audioFrames); } }
        public long SubmittedInputs { get { return Interlocked.Read(ref _submitted); } }
        public long RejectedInputs { get { return Interlocked.Read(ref _rejected); } }
        public long ResetCount { get { return Interlocked.Read(ref _resets); } }
        public bool IsRunning { get { return _thread != null && _thread.IsAlive && !_stop && _failure == null; } }
        public Task StartAsync(string runtimePath, FabricLaunchRequest request)
        {
            if (_thread != null || _stop) throw new InvalidOperationException("Runner cannot start twice.");
            if (!ImpactLease.Wait(0)) throw new InvalidOperationException("This Amber JPM Impact provider supports one active board per process. Stop the existing Impact machine before starting another.");
            var ready = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _thread = new Thread(() => Run(runtimePath, request, ready)) { IsBackground = true, Name = "Oasis Fabric JPM Impact" };
            try { _thread.Start(); } catch { ImpactLease.Release(); _thread = null; throw; }
            return ready.Task;
        }
        public void SubmitInput(FabricInput input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (!IsRunning) return;
            if (Interlocked.Increment(ref _inputCount) > 1024) { Interlocked.Decrement(ref _inputCount); throw new InvalidOperationException("Fabric input queue is full."); }
            _inputs.Enqueue(input); _wake.Set();
        }
        public void RequestReset() { if (IsRunning) { Interlocked.Exchange(ref _reset, 1); _wake.Set(); } }
        public FabricMachineSnapshot LatestSnapshot { get { lock (_snapshotGate) return _latest; } }
        private void Publish(IFabricMachineSession session)
        {
            var snapshot = session.GetSnapshot();
            lock (_snapshotGate) _latest = snapshot;
            Interlocked.Increment(ref _snapshots);
        }
        private void Run(string runtimePath, FabricLaunchRequest request, TaskCompletionSource<bool> ready)
        {
            IFabricRuntimeLibrary runtime = null; IFabricMachineSession session = null;
            try
            {
                runtime = _factory(runtimePath); session = runtime.CreateSession(request); session.Initialise(); session.Reset();
                if (session.Capabilities.Has(FabricCapability.Audio))
                {
                    AudioFormat = session.GetAudioFormat();
                    if (AudioFormat.SampleRate < 8000 || AudioFormat.SampleRate > 192000 || AudioFormat.ChannelCount < 1 || AudioFormat.ChannelCount > 2 ||
                        AudioFormat.BitsPerSample != 16 || !AudioFormat.Interleaved || !AudioFormat.SignedSamples || !AudioFormat.LittleEndian)
                        throw new NotSupportedException("Fabric audio requires interleaved little-endian signed PCM16 mono/stereo at 8–192 kHz.");
                    Audio = new FabricPcmBuffer(checked(((int)AudioFormat.SampleRate / 4) * AudioFormat.ChannelCount));
                }
                var pcm = new short[2048 * Math.Max(1, (int)AudioFormat.ChannelCount)];
                Publish(session); ready.TrySetResult(true);
                var clock = Stopwatch.StartNew(); var ticksPerSlice = Math.Max(1L, Stopwatch.Frequency / 1000); var next = clock.ElapsedTicks;
                while (!_stop)
                {
                    var now = clock.ElapsedTicks;
                    if (now < next) { _wake.WaitOne(1); continue; }
                    if (now - next > Stopwatch.Frequency / 2) next = now; // Bound catch-up after exceptional host stalls.
                    var batch = Math.Min(64L, 1 + (now - next) / ticksPerSlice);
                    for (var i = 0; i < batch && !_stop; i++)
                    {
                        if (Interlocked.Exchange(ref _reset, 0) != 0)
                        {
                            ReleaseHeld(session);
                            while (_inputs.TryDequeue(out _)) Interlocked.Decrement(ref _inputCount);
                            session.Reset(); if (Audio != null) Audio.Clear(); Interlocked.Increment(ref _resets); Publish(session);
                        }
                        session.Advance(1_000_000); var count = Interlocked.Increment(ref _slices);
                        while (_inputs.TryDequeue(out var input))
                        {
                            Interlocked.Decrement(ref _inputCount);
                            var result = session.SubmitInput(input); Interlocked.Increment(ref _submitted);
                            if (result == FabricResult.InputRejected) Interlocked.Increment(ref _rejected);
                            if (input.Kind == FabricInputKind.Digital) { if (input.Active) _asserted.Add(input.NumericalIndex); else _asserted.Remove(input.NumericalIndex); }
                            else { var key = input.CoinChannel + ":" + input.CoinValue; if (input.Active && result == FabricResult.Ok) _coins[key] = input; else _coins.Remove(key); }
                        }
                        if (Audio != null)
                        {
                            var frames = session.ReadAudio(pcm, 2048);
                            if (frames > 0) { Audio.Write(pcm, frames * AudioFormat.ChannelCount); Interlocked.Add(ref _audioFrames, frames); }
                        }
                        if (count % 16 == 0) Publish(session);
                        next += ticksPerSlice;
                    }
                }
            }
            catch (Exception exception) { _failure = exception; ready.TrySetException(exception); }
            finally
            {
                try { if (session != null) { ReleaseHeld(session); session.Shutdown(); } }
                catch (Exception exception) { if (_failure == null) _failure = exception; }
                finally
                {
                    try { if (session != null) session.Dispose(); } catch (Exception exception) { if (_failure == null) _failure = exception; }
                    try { if (runtime != null) runtime.Dispose(); } catch (Exception exception) { if (_failure == null) _failure = exception; }
                    ImpactLease.Release();
                }
            }
        }
        private void ReleaseHeld(IFabricMachineSession session)
        {
            foreach (var index in _asserted) session.SubmitInput(new FabricInput("oasis.switch." + index, index, FabricInputKind.Digital, false));
            _asserted.Clear();
            foreach (var coin in _coins.Values) session.SubmitInput(new FabricInput(coin.Identifier, 0, FabricInputKind.Coin, false, coin.CoinChannel, coin.CoinValue));
            _coins.Clear();
        }
        public void Dispose()
        {
            if (_stop) return;
            _stop = true; _wake.Set();
            if (_thread != null && !_thread.Join(5000)) throw new TimeoutException("Fabric worker did not stop; native ownership retained rather than unloading a live DLL.");
            _wake.Dispose(); if (Audio != null) Audio.Clear();
        }
    }

    /// <summary>Bounded audio transfer. The audio callback consumes samples, never native calls.</summary>
    public sealed class FabricPcmBuffer
    {
        private readonly float[] _samples; private int _read, _count; private readonly object _gate = new object();
        public FabricPcmBuffer(int capacity) { if (capacity < 1 || capacity > 384000) throw new ArgumentOutOfRangeException(nameof(capacity)); _samples = new float[capacity]; }
        public void Write(short[] pcm, int count)
        {
            if (pcm == null || count < 0 || count > pcm.Length) throw new ArgumentOutOfRangeException(nameof(count));
            lock (_gate) for (var i = 0; i < count; i++) { if (_count == _samples.Length) { _read = (_read + 1) % _samples.Length; _count--; } _samples[(_read + _count) % _samples.Length] = pcm[i] / 32768f; _count++; }
        }
        public void Read(float[] destination)
        {
            lock (_gate) for (var i = 0; i < destination.Length; i++) { if (_count == 0) destination[i] = 0; else { destination[i] = _samples[_read]; _read = (_read + 1) % _samples.Length; _count--; } }
        }
        public void Clear() { lock (_gate) { _read = 0; _count = 0; } }
    }
}
