using System.Windows;
using Xunit;

namespace OasisEditor.Tests;

public sealed class AddPanel2DElementCommandTests
{
    public static IEnumerable<object[]> AddableElementCases =>
    [
        [AddablePanelElementKind.Lamp, PanelElementKind.Lamp, PanelElementModelFactory.NewLampWidth, PanelElementModelFactory.NewLampHeight],
        [AddablePanelElementKind.Reel, PanelElementKind.Reel, PanelElementModelFactory.NewReelWidth, PanelElementModelFactory.NewReelHeight],
        [AddablePanelElementKind.SevenSegmentDisplay, PanelElementKind.SevenSegment, PanelElementModelFactory.NewSevenSegmentWidth, PanelElementModelFactory.NewSevenSegmentHeight],
        [AddablePanelElementKind.SegmentAlpha, PanelElementKind.Alpha, PanelElementModelFactory.NewSegmentAlphaWidth, PanelElementModelFactory.NewSegmentAlphaHeight]
    ];

    [Theory]
    [MemberData(nameof(AddableElementCases))]
    public void CreateAddableElement_ReturnsValidVisibleElementAtRequestedPosition(
        object addableKindObject,
        object expectedKindObject,
        double expectedWidth,
        double expectedHeight)
    {
        var addableKind = (AddablePanelElementKind)addableKindObject;
        var expectedKind = (PanelElementKind)expectedKindObject;
        var panelPoint = new Point(123.5, 45.25);

        var element = PanelElementModelFactory.CreateAddableElement(addableKind, panelPoint);

        Assert.False(string.IsNullOrWhiteSpace(element.ObjectId));
        Assert.False(string.IsNullOrWhiteSpace(element.Name));
        Assert.Equal(expectedKind, Panel2DDocumentStorage.ParseElementKind(element.Kind));
        Assert.Equal(panelPoint.X, element.X);
        Assert.Equal(panelPoint.Y, element.Y);
        Assert.Equal(expectedWidth, element.Width);
        Assert.Equal(expectedHeight, element.Height);
        Assert.True(element.IsVisible.GetValueOrDefault());
    }

    [Fact]
    public void CreateAddableElement_UsesVisibleDefaultsForEachRealElementType()
    {
        var lamp = PanelElementModelFactory.CreateAddableElement(AddablePanelElementKind.Lamp, new Point());
        var reel = PanelElementModelFactory.CreateAddableElement(AddablePanelElementKind.Reel, new Point());
        var sevenSegment = PanelElementModelFactory.CreateAddableElement(AddablePanelElementKind.SevenSegmentDisplay, new Point());
        var alpha = PanelElementModelFactory.CreateAddableElement(AddablePanelElementKind.SegmentAlpha, new Point());

        Assert.Equal("#FF3030", lamp.OnColorHex);
        Assert.Equal("#2A0505", lamp.OffColorHex);
        Assert.Equal("Tahoma", lamp.TextBoxFontName);
        Assert.Equal(1, reel.DisplayNumber);
        Assert.Equal(24, reel.Stops);
        Assert.Equal(3d / 24d, reel.VisibleScale);
        Assert.Equal("#FF4040", sevenSegment.OnColorHex);
        Assert.Equal(1, sevenSegment.DisplayNumber);
        Assert.Equal("led16seg", alpha.SegmentDisplayType);
        Assert.Equal("#FFB000", alpha.OnColorHex);
    }

    [Theory]
    [MemberData(nameof(AddableElementCases))]
    public void AddPanelElementCommand_AddsSelectsUndoesAndRedoesSameElement(
        object addableKindObject,
        object expectedKindObject,
        double expectedWidth,
        double expectedHeight)
    {
        var addableKind = (AddablePanelElementKind)addableKindObject;
        var expectedKind = (PanelElementKind)expectedKindObject;
        var document = CreatePanelDocument();
        var changedEvents = new List<PanelChangeEvent>();
        document.PanelChanged += changedEvents.Add;
        var panelPoint = new Point(321.5, 654.25);
        var element = PanelElementModelFactory.CreateAddableElement(addableKind, panelPoint);
        var command = CanvasMutationCommands.CreateAddPanelElementCommand(document.DocumentId, document, element);

        document.CommandService.Execute(command);

        var added = Assert.Single(document.GetPanelElements());
        Assert.Equal(element.ObjectId, added.ObjectId);
        Assert.Equal(expectedKind, added.Kind);
        Assert.Equal(panelPoint.X, added.X);
        Assert.Equal(panelPoint.Y, added.Y);
        Assert.Equal(expectedWidth, added.Width);
        Assert.Equal(expectedHeight, added.Height);
        Assert.Equal(element.OnColorHex, added.OnColorHex);
        Assert.Equal(element.SegmentDisplayType, added.SegmentDisplayType);
        Assert.Single(document.CommandService.History.Entries);
        Assert.NotNull(document.HierarchySelectedPanelSelection);
        Assert.Equal(element.ObjectId, document.HierarchySelectedPanelSelection!.Value.ObjectId);
        var addEvent = Assert.Single(changedEvents);
        Assert.Equal(element.ObjectId, addEvent.ObjectId);
        Assert.True(addEvent.AffectsCanvas);
        Assert.True(addEvent.AffectsHierarchy);
        Assert.True(addEvent.AffectsInspectorRows);
        Assert.True(addEvent.AffectsPersistence);
        Assert.True(addEvent.ChangedProperties.HasFlag(PanelChangeProperties.Structure));

        Assert.True(document.CommandService.TryUndo());
        Assert.Empty(document.GetPanelElements());
        Assert.Null(document.HierarchySelectedPanelSelection);

        Assert.True(document.CommandService.TryRedo());
        var redone = Assert.Single(document.GetPanelElements());
        Assert.Equal(added.ObjectId, redone.ObjectId);
        Assert.Equal(added.Kind, redone.Kind);
        Assert.Equal(added.X, redone.X);
        Assert.Equal(added.Y, redone.Y);
        Assert.Equal(added.Width, redone.Width);
        Assert.Equal(added.Height, redone.Height);
        Assert.Equal(added.OnColorHex, redone.OnColorHex);
        Assert.Equal(added.SegmentDisplayType, redone.SegmentDisplayType);
        Assert.Equal(element.ObjectId, document.HierarchySelectedPanelSelection!.Value.ObjectId);
    }

    private static DocumentTabViewModel CreatePanelDocument(params PanelElementModel[] elements)
    {
        var document = new DocumentTabViewModel(EditorDocument.CreatePanel2DStub("Panel"));
        document.SetPanelElements(elements);
        return document;
    }
}
