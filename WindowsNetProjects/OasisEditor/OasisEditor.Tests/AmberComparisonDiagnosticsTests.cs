using Xunit;

namespace OasisEditor.Tests;

public sealed class AmberComparisonDiagnosticsTests
{
    [Fact]
    public void Preference_DefaultsToDisabled() =>
        Assert.False(new EditorPreferences().NativeEmulation.EnableAmberBackendComparisonLogging);

    [Fact]
    public void DisabledSession_IsSilent()
    {
        var lines = new List<string>();
        var session = new AmberComparisonSession(false, "direct", lines.Add, () => "test");
        session.Write("Advance");
        Assert.Empty(lines);
    }

    [Theory]
    [InlineData("direct")]
    [InlineData("fabric")]
    public void BothBackends_UseSharedSchema(string backend)
    {
        var lines = new List<string>();
        var session = new AmberComparisonSession(true, backend, lines.Add, () => "abcd1234");
        session.Write("ComparisonStart", "selected_backend:test");
        Assert.Contains("[AmberCompare] session=abcd1234", lines[0]);
        Assert.Contains($"backend={backend}", lines[0]);
        Assert.Contains("sequence=1", lines[0]);
        Assert.Contains("elapsed_ns=", lines[0]);
        Assert.Contains("thread=", lines[0]);
        Assert.Contains("operation=ComparisonStart", lines[0]);
        Assert.Contains("arguments=selected_backend:test", lines[0]);
        Assert.Contains("result=success", lines[0]);
        Assert.Contains("summary=none", lines[0]);
    }

    [Fact]
    public void Sessions_HaveUniqueIdentifiers()
    {
        var first = new AmberComparisonSession(true, "direct", _ => { });
        var second = new AmberComparisonSession(true, "direct", _ => { });
        Assert.NotEqual(first.SessionId, second.SessionId);
    }

    [Fact]
    public void HighFrequencyEvents_AreBoundedAndCountersArePerSession()
    {
        var firstLines = new List<string>();
        var first = new AmberComparisonSession(true, "direct", firstLines.Add, () => "first");
        for (var i = 0; i < 25; i++) first.WriteBounded("advance", 20, "Advance", $"elapsed_ns:{i}");
        Assert.Equal(20, firstLines.Count);

        var secondLines = new List<string>();
        new AmberComparisonSession(true, "fabric", secondLines.Add, () => "second")
            .WriteBounded("advance", 20, "Advance", "elapsed_ns:1");
        Assert.Single(secondLines);
    }

    [Fact]
    public void RomSummary_PreservesSparseSlotMetadataWithoutContents()
    {
        var summary = AmberComparisonSession.RomSummary("program", ["c:/roms/even.bin", "", "c:/roms/odd.bin"]);
        Assert.Contains("slot:0|configured_index:0|filename:even.bin|present:true", summary);
        Assert.Contains("slot:1|configured_index:1|filename:absent|present:false", summary);
        Assert.Contains("slot:2|configured_index:2|filename:odd.bin|present:true", summary);
        Assert.DoesNotContain("c:/roms", summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DiagnosticSinkFailures_DoNotEscape()
    {
        var session = new AmberComparisonSession(true, "direct", _ => throw new InvalidOperationException());
        session.Write("Advance");
    }

    [Fact]
    public void SnapshotTracking_DetectsChangesAndCountsStaticRuns()
    {
        var session = new AmberComparisonSession(true, "direct", _ => { });
        Assert.Equal("snapshot_changed:yes|consecutive_static:0", session.TrackSnapshot("one"));
        Assert.Equal("snapshot_changed:no|consecutive_static:1", session.TrackSnapshot("one"));
        Assert.Equal("snapshot_changed:no|consecutive_static:2", session.TrackSnapshot("one"));
        Assert.Equal("snapshot_changed:yes|consecutive_static:0", session.TrackSnapshot("two"));
    }

    [Fact]
    public async Task Writes_AreThreadSafeAndSequenced()
    {
        var lines = new List<string>();
        var session = new AmberComparisonSession(true, "fabric", lines.Add, () => "threaded");
        await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => Task.Run(() => session.Write("SubmitInput"))));
        Assert.Equal(32, lines.Count);
        Assert.Equal(32, lines.Select(line => line.Split("sequence=")[1].Split(' ')[0]).Distinct().Count());
    }
}
