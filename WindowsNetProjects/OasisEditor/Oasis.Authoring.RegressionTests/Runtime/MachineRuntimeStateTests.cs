using OasisEditor;
using Xunit;

namespace OasisEditor.Tests;

public sealed class MachineRuntimeStateTests
{
    [Fact]
    public void LampIntensity_CanBeStoredByMachineReferenceWithoutPanelElementId()
    {
        var runtimeState = new MachineRuntimeState();
        var lampReference = MachineObjectReference.Lamp(17);

        var changed = runtimeState.SetLampIntensityIfChanged(lampReference, 255d);

        Assert.True(changed);
        Assert.Equal(1d, runtimeState.GetLampIntensity(lampReference));
        Assert.Equal(1d, runtimeState.LampIntensityByMachineObjectId["lamp:17"]);
    }

    [Fact]
    public void ReelPosition_CanBeStoredByMachineReferenceWithoutPanelElementId()
    {
        var runtimeState = new MachineRuntimeState();
        var reelReference = MachineObjectReference.Reel(2);

        var changed = runtimeState.SetReelPositionIfChanged(reelReference, 83d);

        Assert.True(changed);
        Assert.Equal(83d, runtimeState.GetReelPosition(reelReference));
    }

    [Fact]
    public void Clear_RemovesMachineReferenceState()
    {
        var runtimeState = new MachineRuntimeState();
        runtimeState.SetLampIntensityIfChanged(MachineObjectReference.Lamp(17), 1d);
        runtimeState.SetReelPositionIfChanged(MachineObjectReference.Reel(2), 83d);
        runtimeState.SetSegmentCellMasksIfChanged(MachineObjectReference.AlphaDisplay(0), [1, 2]);

        runtimeState.Clear();

        Assert.Empty(runtimeState.LampIntensityByMachineObjectId);
        Assert.Empty(runtimeState.ReelPositionByMachineObjectId);
        Assert.Empty(runtimeState.SegmentMasksByMachineObjectId);
    }
}
