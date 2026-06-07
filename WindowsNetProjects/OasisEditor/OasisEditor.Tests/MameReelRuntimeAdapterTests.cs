using OasisEditor;
using Xunit;

namespace OasisEditor.Tests;

public sealed class MameReelRuntimeAdapterTests
{
    [Theory]
    [InlineData(FruitMachinePlatformType.Impact, 12, -0.025d)]
    [InlineData(FruitMachinePlatformType.Impact, 16, -0.08d)]
    [InlineData(FruitMachinePlatformType.Impact, 24, 0d)]
    public void ResolvePlatformBandOffsetNormalized_Impact_UsesConfiguredStopOffsets(
        FruitMachinePlatformType platform,
        int stops,
        double expected)
    {
        var actual = MameReelRuntimeAdapter.ResolvePlatformBandOffsetNormalized(platform, stops);

        Assert.Equal(expected, actual, 6);
    }
    [Fact]
    public void ApplyReelState_UpdatesFaceReelDisplaysByMachineObjectReference()
    {
        var document = CreateFaceDocument();
        var dispatches = new List<Action>();
        var adapter = new MameReelRuntimeAdapter(
            () => [document],
            () => FruitMachinePlatformType.None,
            () => false,
            _ => { },
            action => dispatches.Add(action));
        FaceVisualStateChangedEvent? changedEvent = null;
        document.FaceVisualStateChanged += ev => changedEvent = ev;

        adapter.ApplyReelState(2, 83);

        var dispatch = Assert.Single(dispatches);
        dispatch();

        Assert.Equal(83d, document.RuntimeState.GetReelPosition(MachineObjectReference.Reel(2)));
        Assert.NotNull(changedEvent);
        Assert.Contains("face-reel-2", changedEvent!.ObjectIds);
    }

    [Fact]
    public void ApplyReelState_DoesNotUpdateFaceReelDisplayFromLinkedPanel2DElementIdOnly()
    {
        var document = CreateFaceDocument(machineReference: MachineObjectReference.Empty, linkedPanel2DElementId: "panel-reel-2");
        var dispatches = new List<Action>();
        var adapter = new MameReelRuntimeAdapter(
            () => [document],
            () => FruitMachinePlatformType.None,
            () => false,
            _ => { },
            action => dispatches.Add(action));
        var eventCount = 0;
        document.FaceVisualStateChanged += _ => eventCount++;

        adapter.ApplyReelState(2, 83);

        var dispatch = Assert.Single(dispatches);
        dispatch();

        Assert.Equal(83d, document.RuntimeState.GetReelPosition(MachineObjectReference.Reel(2)));
        Assert.Equal(0, eventCount);
    }

    private static DocumentTabViewModel CreateFaceDocument(MachineObjectReference? machineReference = null, string? linkedPanel2DElementId = null)
    {
        var faceDocument = EditorDocument.CreateFromFile(
            "face.face",
            "face",
            "face");
        var tab = new DocumentTabViewModel(faceDocument);
        tab.SetFaceElements(
        [
            new FaceReelDisplayElement
            {
                ObjectId = "face-reel-2",
                LinkedMachineObjectReference = machineReference ?? MachineObjectReference.Reel(2),
                LinkedPanel2DElementId = linkedPanel2DElementId
            }
        ]);
        return tab;
    }

}
