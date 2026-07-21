using System.Windows;
using OasisEditor;
using Xunit;

namespace OasisEditor.Tests;

public sealed class FaceReelDisplayTests
{

    [Fact]
    public void Serialize_AndRead_RoundTripsReelDisplayElement()
    {
        var source = new FaceDocumentModel
        {
            Id = "face-reel-doc",
            Title = "Reel Face",
            Elements =
            [
                new FaceReelDisplayElement
                {
                    ObjectId = "face-reel-2",
                    Name = "Reel 2",
                    X = 1,
                    Y = 2,
                    Width = 50,
                    Height = 120,
                    IsVisible = true,
                    IsTransformLocked = true,
                    LinkedMachineObjectReference = MachineObjectReference.Reel(2),
                    LinkedPanel2DElementId = "panel-reel-2",
                    AssetPath = "Assets/Reels/reel2.png",
                    Stops = 16,
                    VisibleScale = 0.5d,
                    BandOffset = 0.25d,
                    IsReversed = true,
                    ReelSpecificationId = "jpm-standard"
                }
            ]
        };

        var json = FaceDocumentStorage.Serialize(source);
        Assert.True(FaceDocumentStorage.TryReadValidated(json, out var file, out var error), error);
        var saved = Assert.Single(file.Elements!);
        Assert.Equal("reelDisplay", saved.Kind);
        Assert.Equal("reel:2", saved.LinkedMachineObjectReference);
        Assert.Equal("panel-reel-2", saved.LinkedPanel2DElementId);
        Assert.Equal("Assets/Reels/reel2.png", saved.AssetPath);
        Assert.Equal(16, saved.Stops);
        Assert.Equal(0.5d, saved.VisibleScale);
        Assert.Equal(0.25d, saved.BandOffset);
        Assert.True(saved.IsReversed);
        Assert.Equal("jpm-standard", saved.ReelSpecificationId);

        var model = FaceDocumentStorage.ToModel(file);
        var element = Assert.IsType<FaceReelDisplayElement>(Assert.Single(model.Elements));
        Assert.Equal("reel:2", element.LinkedMachineObjectReference?.ToString());
        Assert.Equal("panel-reel-2", element.LinkedPanel2DElementId);
        Assert.Equal("Assets/Reels/reel2.png", element.AssetPath);
        Assert.Equal(16, element.Stops);
        Assert.Equal(0.5d, element.VisibleScale);
        Assert.Equal(0.25d, element.BandOffset);
        Assert.True(element.IsReversed);
        Assert.Equal("jpm-standard", element.ReelSpecificationId);
    }

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

        Assert.Equal(88.32d, position, 2);
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

}
