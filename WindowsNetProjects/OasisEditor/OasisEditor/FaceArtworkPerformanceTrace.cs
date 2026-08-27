using System.Diagnostics;

namespace OasisEditor;

/// <summary>Opt-in developer timings; enable with OASIS_FACE_TIMING=1 before starting the Editor.</summary>
internal static class FaceArtworkPerformanceTrace
{
    private static readonly bool Enabled = Environment.GetEnvironmentVariable("OASIS_FACE_TIMING") == "1";

    public static Scope? Measure(string operation) => Enabled
        ? new Scope($"{operation} (processing workers: {ImageProcessingExecutionPolicy.Current.MaxDegreeOfParallelism})", Stopwatch.GetTimestamp())
        : null;

    internal sealed class Scope(string operation, long started) : IDisposable
    {
        private string? _operation = operation;

        public void Dispose()
        {
            if (_operation is null) return;
            var elapsed = Stopwatch.GetElapsedTime(started);
            Trace.WriteLine($"[Face artwork timing] {_operation}: {elapsed.TotalMilliseconds:F1} ms");
            _operation = null;
        }
    }
}
