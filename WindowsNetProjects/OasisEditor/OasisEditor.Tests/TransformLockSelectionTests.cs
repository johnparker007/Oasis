using System.Windows;
using Xunit;

namespace OasisEditor.Tests;

public sealed class TransformLockSelectionTests
{
    [Fact]
    public void LockedPanelElements_RemainHitTestSelectable()
    {
        var elements = new[]
        {
            new PanelElementModel { ObjectId = "locked", Kind = PanelElementKind.Lamp, X = 0, Y = 0, Width = 10, Height = 10, IsVisible = true, IsLocked = true }
        };

        var selection = Panel2DSelectionService.SelectFromPoint(elements, new Point(5, 5));

        Assert.NotNull(selection);
        Assert.Equal("locked", selection!.Value.ObjectId);
    }

    [Fact]
    public void TransformLockedPanelElements_AreNotMoveOrResizeEligible()
    {
        var element = new PanelElementModel { ObjectId = "locked", IsLocked = true };

        Assert.True(element.IsTransformLocked);
        Assert.False(TransformLockInteractionService.CanMoveOrResize(element));
    }

    [Fact]
    public void LegacySerializedIsLocked_MapsToTransformLockCompatibilityApi()
    {
        const string json = """
        { "SchemaVersion": 2, "Title": "Panel", "Elements": [ { "ObjectId": "old", "Kind": "lamp", "Width": 1, "Height": 1, "IsLocked": true, "IsVisible": true } ] }
        """;

        var model = Panel2DDocumentStorage.DeserializeModel(json);

        var element = Assert.Single(model.Elements);
        Assert.True(element.IsLocked);
        Assert.True(element.IsTransformLocked);
    }
}
