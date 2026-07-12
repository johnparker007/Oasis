using OasisEditor;
using System.Windows;
using Xunit;

namespace OasisEditor.Tests;

public sealed class TransformLockSelectionTests
{
    [Fact]
    public void TransformLockedPanelElements_RemainHitTestSelectable()
    {
        var elements = new[]
        {
            new PanelElementModel { ObjectId = "locked", Kind = PanelElementKind.Lamp, X = 0, Y = 0, Width = 10, Height = 10, IsVisible = true, IsTransformLocked = true }
        };

        var selection = Panel2DSelectionService.SelectFromPoint(elements, new Point(5, 5));

        Assert.NotNull(selection);
        Assert.Equal("locked", selection!.Value.ObjectId);
    }

    [Fact]
    public void TransformLockedPanelElements_AreNotMoveOrResizeEligible()
    {
        var element = new PanelElementModel { ObjectId = "locked", IsTransformLocked = true };

        Assert.True(element.IsTransformLocked);
        Assert.False(TransformLockInteractionService.CanMoveOrResize(element));
    }

    [Fact]
    public void SerializedLockTransform_MapsToTransformLockModel()
    {
        const string json = """
        { "SchemaVersion": 2, "Title": "Panel", "Elements": [ { "ObjectId": "old", "Kind": "lamp", "Width": 1, "Height": 1, "LockTransform": true, "IsVisible": true } ] }
        """;

        var model = Panel2DDocumentStorage.DeserializeModel(json);

        var element = Assert.Single(model.Elements);
        Assert.True(element.IsTransformLocked);
    }
}
