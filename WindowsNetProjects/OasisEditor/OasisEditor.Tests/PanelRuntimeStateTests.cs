using Xunit;

namespace OasisEditor.Tests;

public sealed class PanelRuntimeStateTests
{
    [Fact]
    public void GetEffectiveReelPosition_IncludesTemporaryOffset()
    {
        var runtimeState = new PanelRuntimeState();

        runtimeState.SetReelPositionIfChanged(" reel-1 ", 12d);
        runtimeState.SetTemporaryReelOffsetIfChanged("reel-1", 3.5d);

        Assert.Equal(15.5d, runtimeState.GetEffectiveReelPosition("reel-1"));
    }

    [Fact]
    public void ClearTemporaryReelOffsetIfChanged_RemovesOnlyTemporaryOffset()
    {
        var runtimeState = new PanelRuntimeState();
        runtimeState.SetReelPositionIfChanged("reel-1", 12d);
        runtimeState.SetTemporaryReelOffsetIfChanged("reel-1", 3.5d);

        var changed = runtimeState.ClearTemporaryReelOffsetIfChanged(" reel-1 ");

        Assert.True(changed);
        Assert.Equal(12d, runtimeState.GetReelPosition("reel-1"));
        Assert.Equal(12d, runtimeState.GetEffectiveReelPosition("reel-1"));
    }

    [Fact]
    public void Clear_RemovesTemporaryReelOffsets()
    {
        var runtimeState = new PanelRuntimeState();
        runtimeState.SetTemporaryReelOffsetIfChanged("reel-1", 3.5d);

        runtimeState.Clear();

        Assert.Empty(runtimeState.TemporaryReelOffsetByObjectId);
        Assert.Equal(0d, runtimeState.GetEffectiveReelPosition("reel-1"));
    }
}
