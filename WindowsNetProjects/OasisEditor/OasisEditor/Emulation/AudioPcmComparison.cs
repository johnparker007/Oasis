namespace OasisEditor;

public sealed record AudioPcmComparisonResult(
    long? FirstDifferingFrame,
    long? FirstCandidateDiscontinuityFrame,
    int MaximumSampleDelta,
    long TotalFrameCountDifference,
    bool ChannelMismatch,
    IReadOnlyList<string> CandidateDuplicateOrMissingRuns);

public static class AudioPcmComparison
{
    public static AudioPcmComparisonResult Compare(
        ReadOnlySpan<short> expectedInterleaved,
        ReadOnlySpan<short> actualInterleaved,
        int expectedChannels,
        int actualChannels,
        int discontinuityThreshold = 12000)
    {
        if (expectedChannels <= 0) throw new ArgumentOutOfRangeException(nameof(expectedChannels));
        if (actualChannels <= 0) throw new ArgumentOutOfRangeException(nameof(actualChannels));

        var channelMismatch = expectedChannels != actualChannels;
        var channels = Math.Min(expectedChannels, actualChannels);
        var expectedFrames = expectedInterleaved.Length / expectedChannels;
        var actualFrames = actualInterleaved.Length / actualChannels;
        var comparableFrames = Math.Min(expectedFrames, actualFrames);
        long? firstDifference = null;
        long? firstDiscontinuity = null;
        var maxDelta = 0;

        for (var frame = 0; frame < comparableFrames; frame++)
        {
            for (var channel = 0; channel < channels; channel++)
            {
                var expected = expectedInterleaved[frame * expectedChannels + channel];
                var actual = actualInterleaved[frame * actualChannels + channel];
                var delta = Math.Abs(expected - actual);
                if (delta > maxDelta) maxDelta = delta;
                if (delta != 0 && firstDifference is null) firstDifference = frame;
            }

            if (frame > 0 && firstDiscontinuity is null)
            {
                for (var channel = 0; channel < actualChannels; channel++)
                {
                    var previous = actualInterleaved[(frame - 1) * actualChannels + channel];
                    var current = actualInterleaved[frame * actualChannels + channel];
                    if (Math.Abs(current - previous) >= discontinuityThreshold)
                    {
                        firstDiscontinuity = frame;
                        break;
                    }
                }
            }
        }

        return new(firstDifference, firstDiscontinuity, maxDelta, expectedFrames - actualFrames,
            channelMismatch, FindRuns(expectedInterleaved, actualInterleaved, expectedChannels, actualChannels));
    }

    private static IReadOnlyList<string> FindRuns(ReadOnlySpan<short> expected, ReadOnlySpan<short> actual, int expectedChannels, int actualChannels)
    {
        var results = new List<string>();
        if (expectedChannels != actualChannels || expectedChannels <= 0)
            return results;
        var frames = Math.Min(expected.Length, actual.Length) / expectedChannels;
        for (var frame = 1; frame < frames && results.Count < 8; frame++)
        {
            var sameAsPreviousExpected = true;
            var duplicatedActual = true;
            for (var channel = 0; channel < expectedChannels; channel++)
            {
                sameAsPreviousExpected &= expected[frame * expectedChannels + channel] == actual[(frame - 1) * actualChannels + channel];
                duplicatedActual &= actual[frame * actualChannels + channel] == actual[(frame - 1) * actualChannels + channel];
            }
            if (sameAsPreviousExpected)
                results.Add($"candidate missing actual frame before {frame}");
            if (duplicatedActual)
                results.Add($"candidate duplicated actual frame at {frame}");
        }
        return results;
    }
}
