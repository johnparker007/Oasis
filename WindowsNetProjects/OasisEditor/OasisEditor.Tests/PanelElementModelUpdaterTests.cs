using Xunit;

namespace OasisEditor.Tests;

public sealed class PanelElementModelUpdaterTests
{
    [Fact]
    public void Apply_UpdatesRequestedFields_AndCanClearNullableValues()
    {
        var source = new PanelElementModel
        {
            ObjectId = "element-1",
            Name = "Source",
            Kind = PanelElementKind.Lamp,
            X = 10,
            Y = 20,
            Width = 30,
            Height = 40,
            AssetPath = "Assets/lamp.png",
            SecondaryAssetPath = "Assets/mask.png",
            DisplayNumber = 7,
            OnColorHex = "#FFFFFF",
            OffColorHex = "#111111",
            TextColorHex = "#FF0000",
            DisplayText = "ABC",
            IsReversed = true,
            Stops = 24,
            VisibleScale = 1.5,
            IsLocked = false,
            IsVisible = true
        };

        var updated = PanelElementModelUpdater.Apply(
            source,
            new PanelElementModelUpdate
            {
                Name = "Updated",
                X = 99.5,
                AssetPath = (string?)null,
                DisplayNumber = (int?)null,
                IsLocked = true,
                IsVisible = false
            });

        Assert.Equal("element-1", updated.ObjectId);
        Assert.Equal(PanelElementKind.Lamp, updated.Kind);
        Assert.Equal("Updated", updated.Name);
        Assert.Equal(99.5, updated.X);
        Assert.Equal(20, updated.Y);
        Assert.Null(updated.AssetPath);
        Assert.Null(updated.DisplayNumber);
        Assert.True(updated.IsLocked);
        Assert.False(updated.IsVisible);
        Assert.Equal("#111111", updated.OffColorHex);
    }

    [Theory]
    [InlineData(1, 1, 1, 1, true)]
    [InlineData(double.NaN, 1, 1, 1, false)]
    [InlineData(1, double.PositiveInfinity, 1, 1, false)]
    [InlineData(1, 1, 0, 1, false)]
    [InlineData(1, 1, 1, -5, false)]
    public void IsValidForInspectorUpdate_ValidatesNumericRequirements(double x, double y, double width, double height, bool expected)
    {
        var element = new PanelElementModel
        {
            ObjectId = "element-1",
            Name = "Element",
            Kind = PanelElementKind.Rectangle,
            X = x,
            Y = y,
            Width = width,
            Height = height
        };

        Assert.Equal(expected, PanelElementValidation.IsValidForInspectorUpdate(element));
    }
}
