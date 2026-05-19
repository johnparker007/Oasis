using System.Diagnostics;
using System.Text;

namespace OasisEditor.Rendering;

internal static class SkiaRenderDiagnostics
{
    private static readonly object Gate = new();
    private static readonly Dictionary<string, RendererWindowStats> StatsByView = new(StringComparer.Ordinal);

    public static bool IsEnabled { get; set; } = false;
    public static TimeSpan ReportInterval { get; set; } = TimeSpan.FromSeconds(3);

    public static event Action<string>? ReportReady;

    public static FrameScope BeginFrame(string viewName)
    {
        if (!IsEnabled)
        {
            return FrameScope.Disabled;
        }

        return new FrameScope(viewName, Stopwatch.StartNew());
    }

    private static void PublishFrame(FrameData data)
    {
        lock (Gate)
        {
            if (!StatsByView.TryGetValue(data.ViewName, out var stats))
            {
                stats = new RendererWindowStats();
                StatsByView[data.ViewName] = stats;
            }

            stats.Accumulate(data);
            if (stats.ShouldReport(ReportInterval))
            {
                ReportReady?.Invoke(stats.BuildReportAndReset(data.ViewName));
            }
        }
    }

    internal readonly record struct FrameData(
        string ViewName,
        TimeSpan Total,
        TimeSpan Lamps,
        TimeSpan TextLamps,
        TimeSpan Alpha,
        TimeSpan SevenSegment,
        TimeSpan Reels,
        int ElementCount,
        int LampCount,
        int TextLampCount,
        int AlphaCount,
        int SevenSegmentCount,
        int ReelCount,
        int TextLayoutCount,
        int TextDrawCount);

    internal readonly struct FrameScope(string viewName, Stopwatch stopwatch)
    {
        public static FrameScope Disabled => default;
        public bool Enabled => stopwatch is not null;

        public void Complete(FrameData data)
        {
            if (!Enabled)
            {
                return;
            }

            PublishFrame(data with { ViewName = viewName, Total = stopwatch.Elapsed });
        }
    }

    private sealed class RendererWindowStats
    {
        private DateTime _windowStartUtc = DateTime.UtcNow;
        private int _frames;
        private double _totalMs;
        private double _lampMs;
        private double _textLampMs;
        private double _alphaMs;
        private double _sevenMs;
        private double _reelMs;
        private int _elements;
        private int _lamps;
        private int _textLamps;
        private int _alphas;
        private int _sevens;
        private int _reels;
        private int _textLayouts;
        private int _textDraws;

        public void Accumulate(FrameData data)
        {
            _frames++;
            _totalMs += data.Total.TotalMilliseconds;
            _lampMs += data.Lamps.TotalMilliseconds;
            _textLampMs += data.TextLamps.TotalMilliseconds;
            _alphaMs += data.Alpha.TotalMilliseconds;
            _sevenMs += data.SevenSegment.TotalMilliseconds;
            _reelMs += data.Reels.TotalMilliseconds;
            _elements += data.ElementCount;
            _lamps += data.LampCount;
            _textLamps += data.TextLampCount;
            _alphas += data.AlphaCount;
            _sevens += data.SevenSegmentCount;
            _reels += data.ReelCount;
            _textLayouts += data.TextLayoutCount;
            _textDraws += data.TextDrawCount;
        }

        public bool ShouldReport(TimeSpan interval) => DateTime.UtcNow - _windowStartUtc >= interval && _frames > 0;

        public string BuildReportAndReset(string viewName)
        {
            var seconds = Math.Max(0.001, (DateTime.UtcNow - _windowStartUtc).TotalSeconds);
            var sb = new StringBuilder();
            sb.Append($"[SkiaDiag] {viewName}: fps~{_frames / seconds:F1}, frameAvg={_totalMs / _frames:F2}ms, frameMax={_totalMs:F2}ms/{_frames}f");
            sb.Append($", timing(ms): lamps={_lampMs / _frames:F2} textLamps={_textLampMs / _frames:F2} alpha={_alphaMs / _frames:F2} seg7={_sevenMs / _frames:F2} reels={_reelMs / _frames:F2}");
            sb.Append($", counts/frame: elems={_elements / (double)_frames:F1} lamps={_lamps / (double)_frames:F1} textLamps={_textLamps / (double)_frames:F1} alpha={_alphas / (double)_frames:F1} seg7={_sevens / (double)_frames:F1} reels={_reels / (double)_frames:F1}");
            sb.Append($", textWork/frame: layouts={_textLayouts / (double)_frames:F1} draws={_textDraws / (double)_frames:F1}");

            _windowStartUtc = DateTime.UtcNow;
            _frames = 0;
            _totalMs = _lampMs = _textLampMs = _alphaMs = _sevenMs = _reelMs = 0;
            _elements = _lamps = _textLamps = _alphas = _sevens = _reels = _textLayouts = _textDraws = 0;
            return sb.ToString();
        }
    }
}
