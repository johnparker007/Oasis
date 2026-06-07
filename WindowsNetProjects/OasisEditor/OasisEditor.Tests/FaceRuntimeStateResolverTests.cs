using OasisEditor;
using Xunit;

namespace OasisEditor.Tests;

public sealed class FaceRuntimeStateResolverTests
{
    [Fact]
    public void GetLampIntensity_UsesMachineObjectReference()
    {
        var runtimeState = new MachineRuntimeState();
        runtimeState.SetLampIntensity(MachineObjectReference.Lamp(7), 1d);
        runtimeState.SetLampIntensity("panel-lamp-7", 0d);
        var lampWindow = new FaceLampWindowElement
        {
            ObjectId = "face-lamp-7",
            LinkedMachineObjectReference = MachineObjectReference.Lamp(7),
            LinkedPanel2DElementId = "panel-lamp-7"
        };

        var intensity = FaceRuntimeStateResolver.Instance.GetLampIntensity(lampWindow, runtimeState);

        Assert.Equal(1d, intensity);
    }

    [Fact]
    public void GetLampIntensity_IgnoresLinkedPanel2DElementIdWhenMachineReferenceIsMissing()
    {
        var runtimeState = new MachineRuntimeState();
        runtimeState.SetLampIntensity("panel-lamp-7", 1d);
        var lampWindow = new FaceLampWindowElement
        {
            ObjectId = "face-lamp-7",
            LinkedPanel2DElementId = "panel-lamp-7"
        };

        var intensity = FaceRuntimeStateResolver.Instance.GetLampIntensity(lampWindow, runtimeState);

        Assert.Equal(0d, intensity);
    }

    [Fact]
    public void GetSevenSegmentCellMasks_UsesMachineObjectReference()
    {
        var runtimeState = new MachineRuntimeState();
        runtimeState.SetSegmentCellMasksIfChanged(MachineObjectReference.SevenSegmentDisplay(3), [0x5B]);
        runtimeState.SetSegmentCellMasksIfChanged("panel-seven-3", [0x06]);
        var display = new FaceSevenSegmentDisplayElement
        {
            ObjectId = "face-seven-3",
            LinkedMachineObjectReference = MachineObjectReference.SevenSegmentDisplay(3),
            LinkedPanel2DElementId = "panel-seven-3"
        };

        var masks = FaceRuntimeStateResolver.Instance.GetSevenSegmentCellMasks(display, runtimeState);

        Assert.Single(masks);
        Assert.Equal(0x5B, masks[0]);
    }

    [Fact]
    public void GetSevenSegmentCellMasks_IgnoresLinkedPanel2DElementIdWhenMachineReferenceIsMissing()
    {
        var runtimeState = new MachineRuntimeState();
        runtimeState.SetSegmentCellMasksIfChanged("panel-seven-3", [0x06]);
        var display = new FaceSevenSegmentDisplayElement
        {
            ObjectId = "face-seven-3",
            LinkedPanel2DElementId = "panel-seven-3"
        };

        var masks = FaceRuntimeStateResolver.Instance.GetSevenSegmentCellMasks(display, runtimeState);

        Assert.Single(masks);
        Assert.Equal(0, masks[0]);
    }

    [Fact]
    public void GetAlphaCellMasks_UsesMachineObjectReference()
    {
        var runtimeState = new MachineRuntimeState();
        runtimeState.SetSegmentCellMasksIfChanged(MachineObjectReference.AlphaDisplay(10), [1, 2, 3]);
        runtimeState.SetSegmentCellMasksIfChanged("panel-alpha-10", [9, 9, 9]);
        var display = new FaceAlphaDisplayElement
        {
            ObjectId = "face-alpha-10",
            LinkedMachineObjectReference = MachineObjectReference.AlphaDisplay(10),
            LinkedPanel2DElementId = "panel-alpha-10"
        };

        var masks = FaceRuntimeStateResolver.Instance.GetAlphaCellMasks(display, runtimeState);

        Assert.Equal(new[] { 1, 2, 3 }, masks);
    }

    [Fact]
    public void GetAlphaCellMasks_IgnoresLinkedPanel2DElementIdWhenMachineReferenceIsMissing()
    {
        var runtimeState = new MachineRuntimeState();
        runtimeState.SetSegmentCellMasksIfChanged("panel-alpha-10", [9, 9, 9]);
        var display = new FaceAlphaDisplayElement
        {
            ObjectId = "face-alpha-10",
            LinkedPanel2DElementId = "panel-alpha-10"
        };

        var masks = FaceRuntimeStateResolver.Instance.GetAlphaCellMasks(display, runtimeState);

        Assert.Equal(16, masks.Length);
        Assert.All(masks, mask => Assert.Equal(0, mask));
    }

}
