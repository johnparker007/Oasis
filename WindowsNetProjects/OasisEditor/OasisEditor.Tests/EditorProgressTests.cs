using OasisEditor.Progress;
using Xunit;

namespace OasisEditor.Tests;

public sealed class EditorProgressTests
{
    [Fact]
    public void RequestNormalize_UsesSafeDefaults()
    {
        var request = new EditorProgressRequest("  ", "  Starting  ", ShowDelay: TimeSpan.FromMilliseconds(-10), MinimumDisplayDuration: TimeSpan.FromMilliseconds(-5));

        var normalized = request.Normalize();

        Assert.Equal("Working", normalized.Title);
        Assert.Equal("Starting", normalized.InitialMessage);
        Assert.Equal(TimeSpan.Zero, normalized.ShowDelay);
        Assert.Equal(TimeSpan.Zero, normalized.MinimumDisplayDuration);
    }

    [Theory]
    [InlineData(-0.5, 0.0)]
    [InlineData(0.25, 0.25)]
    [InlineData(1.5, 1.0)]
    [InlineData(double.NaN, 0.0)]
    [InlineData(double.PositiveInfinity, 1.0)]
    public void StateWithDeterminateProgress_ClampsValue(double input, double expected)
    {
        var state = EditorProgressState.FromRequest(new EditorProgressRequest("Import"));

        var updated = state.WithDeterminateProgress(input, "Copying");

        Assert.Equal(EditorProgressMode.Determinate, updated.Mode);
        Assert.Equal(expected, updated.Value);
        Assert.Equal("Copying", updated.Message);
    }

    [Fact]
    public void StateWithIndeterminateMessage_ClearsProgressValue()
    {
        var state = EditorProgressState.FromRequest(new EditorProgressRequest("Import", InitialMode: EditorProgressMode.Determinate))
            .WithDeterminateProgress(0.5, "Halfway");

        var updated = state.WithIndeterminateMessage("Waiting");

        Assert.Equal(EditorProgressMode.Indeterminate, updated.Mode);
        Assert.Null(updated.Value);
        Assert.Equal("Waiting", updated.Message);
    }

    [Fact]
    public void ReporterChild_MapsProgressIntoParentRange()
    {
        var states = new List<EditorProgressState>();
        var current = EditorProgressState.FromRequest(new EditorProgressRequest("Generate", InitialMode: EditorProgressMode.Determinate));
        var reporter = new EditorProgressReporter(current, state =>
        {
            current = state;
            states.Add(state);
        });

        var child = reporter.CreateChild(0.25, 0.75, "Runtime export");
        child.Report(0.5, "Writing textures");

        var updated = Assert.Single(states.Skip(1));
        Assert.Equal(EditorProgressMode.Determinate, updated.Mode);
        Assert.Equal(0.5, updated.Value);
        Assert.Equal("Runtime export: Writing textures", updated.Message);
    }

    [Fact]
    public async Task NoOpService_RunsOperationAndReturnsResult()
    {
        using var cancellationSource = new CancellationTokenSource();
        var service = NoOpProgressDialogService.Instance;

        var result = await service.RunAsync(
            new EditorProgressRequest("Open"),
            (progress, cancellationToken) =>
            {
                progress.ReportIndeterminate("Reading");
                Assert.Equal(cancellationSource.Token, cancellationToken);
                return Task.FromResult(42);
            },
            cancellationSource.Token);

        Assert.Equal(42, result);
        Assert.False(service.IsOperationActive);
    }

    [Fact]
    public async Task NoOpService_PropagatesOperationFailure()
    {
        var service = NoOpProgressDialogService.Instance;

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RunAsync(
            new EditorProgressRequest("Open"),
            (_, _) => throw new InvalidOperationException("Failed")));
    }
}
