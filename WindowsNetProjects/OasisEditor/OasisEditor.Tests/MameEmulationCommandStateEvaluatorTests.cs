using Xunit;

namespace OasisEditor.Tests;

public sealed class MameEmulationCommandStateEvaluatorTests
{
    [Fact]
    public void Evaluate_WhenNoProjectLoaded_StartIsDisabled()
    {
        var state = MameEmulationCommandStateEvaluator.Evaluate(false, MameEmulationState.Stopped);

        Assert.False(state.CanStart);
        Assert.False(state.CanStop);
        Assert.False(state.CanPause);
        Assert.False(state.CanResume);
    }

    [Fact]
    public void Evaluate_WhenStoppedWithProject_StartEnabledOnly()
    {
        var state = MameEmulationCommandStateEvaluator.Evaluate(true, MameEmulationState.Stopped);

        Assert.True(state.CanStart);
        Assert.False(state.CanStop);
        Assert.False(state.CanPause);
        Assert.False(state.CanResume);
    }

    [Fact]
    public void Evaluate_WhenRunning_StopAndPauseEnabled()
    {
        var state = MameEmulationCommandStateEvaluator.Evaluate(true, MameEmulationState.Running);

        Assert.False(state.CanStart);
        Assert.True(state.CanStop);
        Assert.True(state.CanPause);
        Assert.False(state.CanResume);
    }

    [Fact]
    public void Evaluate_WhenPaused_StopAndResumeEnabled()
    {
        var state = MameEmulationCommandStateEvaluator.Evaluate(true, MameEmulationState.Paused);

        Assert.False(state.CanStart);
        Assert.True(state.CanStop);
        Assert.False(state.CanPause);
        Assert.True(state.CanResume);
    }
}
