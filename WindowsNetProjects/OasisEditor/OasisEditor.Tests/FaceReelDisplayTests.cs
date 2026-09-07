using System.Windows;
using OasisEditor;
using Xunit;

namespace OasisEditor.Tests;

public sealed class FaceReelDisplayTests
{


    [Fact]
    public void RuntimeResolver_UsesMachineReferenceAndIgnoresLinkedPanelElementId()
    {
        var runtimeState = new MachineRuntimeState();
        runtimeState.SetReelPositionIfChanged(MachineObjectReference.Reel(2), 83d);
        runtimeState.SetReelPositionIfChanged("panel-reel-2", 12d);
        var reel = new FaceReelDisplayElement
        {
            ObjectId = "face-reel-2",
            LinkedMachineObjectReference = MachineObjectReference.Reel(2),
            LinkedPanel2DElementId = "panel-reel-2"
        };

        var position = FaceRuntimeStateResolver.Instance.GetReelPosition(reel, runtimeState);

        Assert.Equal(83d, position);
    }

    [Fact]
    public void RuntimeResolver_ReturnsZeroWhenOnlyLinkedPanelElementIdHasState()
    {
        var runtimeState = new MachineRuntimeState();
        runtimeState.SetReelPositionIfChanged("panel-reel-2", 12d);
        var reel = new FaceReelDisplayElement
        {
            ObjectId = "face-reel-2",
            LinkedPanel2DElementId = "panel-reel-2"
        };

        var position = FaceRuntimeStateResolver.Instance.GetReelPosition(reel, runtimeState);

        Assert.Equal(0d, position);
    }
    [Fact]
    public void RuntimeResolver_AppliesPlatformAndStopOffsetToMachineReferencePosition()
    {
        var runtimeState = new MachineRuntimeState
        {
            FruitMachinePlatform = FruitMachinePlatformType.Impact
        };
        runtimeState.SetReelPositionIfChanged(MachineObjectReference.Reel(2), 0d);
        var reel = new FaceReelDisplayElement
        {
            ObjectId = "face-reel-2",
            LinkedMachineObjectReference = MachineObjectReference.Reel(2),
            Stops = 16
        };

        var position = FaceRuntimeStateResolver.Instance.GetReelPosition(reel, runtimeState);

        Assert.Equal(94.272d, position, 3);
    }

    [Fact]
    public void RuntimeResolver_AppliesMpu4PlatformReversalLikePanelReels()
    {
        var runtimeState = new MachineRuntimeState
        {
            FruitMachinePlatform = FruitMachinePlatformType.MPU4
        };
        runtimeState.SetReelPositionIfChanged(MachineObjectReference.Reel(2), 12d);
        var reel = new FaceReelDisplayElement
        {
            ObjectId = "face-reel-2",
            LinkedMachineObjectReference = MachineObjectReference.Reel(2),
            Stops = 16
        };

        var position = FaceRuntimeStateResolver.Instance.GetReelPosition(reel, runtimeState);

        Assert.Equal(79.2d, position, 2);
    }

    [Theory]
    [InlineData(false, 10d)]
    [InlineData(true, 86d)]
    public void RuntimeResolver_AppliesEpochPlatformReversalLikePanelReels(bool isReversed, double expected)
    {
        var runtimeState = new MachineRuntimeState
        {
            FruitMachinePlatform = FruitMachinePlatformType.Epoch
        };
        runtimeState.SetReelPositionIfChanged(MachineObjectReference.Reel(2), 86d);
        var reel = new FaceReelDisplayElement
        {
            ObjectId = "face-reel-2",
            LinkedMachineObjectReference = MachineObjectReference.Reel(2),
            Stops = 24,
            IsReversed = isReversed
        };

        var position = FaceRuntimeStateResolver.Instance.GetReelPosition(reel, runtimeState);

        Assert.Equal(expected, position);
    }

}
