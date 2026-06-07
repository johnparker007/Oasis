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
}
