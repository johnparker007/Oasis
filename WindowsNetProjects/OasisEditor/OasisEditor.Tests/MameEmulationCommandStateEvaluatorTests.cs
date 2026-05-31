using Xunit;

namespace OasisEditor.Tests;

public sealed class MameEmulationCommandStateEvaluatorTests
{
    [Fact]
    public void Evaluate_WhenNoProjectLoaded_StartIsDisabled()
    {
        var state = MameEmulationCommandStateEvaluator.Evaluate(false, MameEmulationState.Stopped);

        Assert.False(state.CanStart);
        Assert.False(state.CanStartAndLoadState);
        Assert.False(state.CanStop);
        Assert.False(state.CanSaveStateAndExit);
        Assert.False(state.CanLoadState);
        Assert.False(state.CanSaveState);
        Assert.False(state.CanPause);
        Assert.False(state.CanResume);
        Assert.False(state.CanTogglePause);
        Assert.False(state.CanSetThrottle);
        Assert.False(state.CanReset);
    }

    [Fact]
    public void Evaluate_WhenStoppedWithProject_StartCommandsEnabledOnly()
    {
        var state = MameEmulationCommandStateEvaluator.Evaluate(true, MameEmulationState.Stopped);

        Assert.True(state.CanStart);
        Assert.True(state.CanStartAndLoadState);
        Assert.False(state.CanStop);
        Assert.False(state.CanSaveStateAndExit);
        Assert.False(state.CanLoadState);
        Assert.False(state.CanSaveState);
        Assert.False(state.CanPause);
        Assert.False(state.CanResume);
        Assert.False(state.CanTogglePause);
        Assert.False(state.CanSetThrottle);
        Assert.False(state.CanReset);
    }

    [Fact]
    public void Evaluate_WhenRunning_RuntimeControlsEnabled()
    {
        var state = MameEmulationCommandStateEvaluator.Evaluate(true, MameEmulationState.Running);

        Assert.False(state.CanStart);
        Assert.False(state.CanStartAndLoadState);
        Assert.True(state.CanStop);
        Assert.True(state.CanSaveStateAndExit);
        Assert.True(state.CanLoadState);
        Assert.True(state.CanSaveState);
        Assert.True(state.CanPause);
        Assert.False(state.CanResume);
        Assert.True(state.CanTogglePause);
        Assert.True(state.CanSetThrottle);
        Assert.True(state.CanReset);
    }

    [Fact]
    public void Evaluate_WhenPaused_RuntimeControlsAndResumeEnabled()
    {
        var state = MameEmulationCommandStateEvaluator.Evaluate(true, MameEmulationState.Paused);

        Assert.False(state.CanStart);
        Assert.False(state.CanStartAndLoadState);
        Assert.True(state.CanStop);
        Assert.True(state.CanSaveStateAndExit);
        Assert.True(state.CanLoadState);
        Assert.True(state.CanSaveState);
        Assert.False(state.CanPause);
        Assert.True(state.CanResume);
        Assert.True(state.CanTogglePause);
        Assert.True(state.CanSetThrottle);
        Assert.True(state.CanReset);
    }
}
