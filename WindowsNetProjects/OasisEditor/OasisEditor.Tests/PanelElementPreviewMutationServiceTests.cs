using Xunit;

namespace OasisEditor.Tests;

public sealed class PanelElementPreviewMutationServiceTests
{
    [Fact]
    public void TryApplyPreview_UpdatesElementAndInspectorWithoutRecordingUndoHistoryOrDirtyingDocument()
    {
        var document = CreateDocument(new PanelElementModel
        {
            ObjectId = "lamp-1",
            Name = "Lamp 1",
            Kind = PanelElementKind.Lamp,
            X = 10,
            Y = 20,
            Width = 30,
            Height = 40,
            IsVisible = true
        });
        var events = new List<PanelChangeEvent>();
        document.PanelChanged += panelChange => events.Add(panelChange);

        var updated = PanelElementModelCloner.Clone(document.GetPanelElements().Single(), x: 25, y: 45);
        var applied = PanelElementPreviewMutationService.TryApplyPreview(document, "lamp-1", updated);

        Assert.True(applied);
        var element = document.GetPanelElements().Single();
        Assert.Equal(25, element.X);
        Assert.Equal(45, element.Y);
        Assert.False(document.IsDirty);
        Assert.Empty(document.CommandService.History.Entries);

        var panelChange = Assert.Single(events);
        Assert.Equal("lamp-1", panelChange.ObjectId);
        Assert.True(panelChange.ChangedProperties.HasFlag(PanelChangeProperties.Geometry));
        Assert.True(panelChange.AffectsCanvas);
        Assert.True(panelChange.AffectsInspectorRows);
        Assert.False(panelChange.AffectsHierarchy);
        Assert.False(panelChange.AffectsPersistence);
    }

    [Fact]
    public void PreviewedUpdateElementCommand_RecordsSingleUndoableActionFromOriginalToPreviewedPosition()
    {
        var document = CreateDocument(new PanelElementModel
        {
            ObjectId = "reel-1",
            Name = "Reel 1",
            Kind = PanelElementKind.Reel,
            X = 10,
            Y = 20,
            Width = 30,
            Height = 40,
            IsVisible = true
        });
        var original = PanelElementModelCloner.Clone(document.GetPanelElements().Single());
        var previewed = PanelElementModelCloner.Clone(original, x: 35, y: 55);

        Assert.True(PanelElementPreviewMutationService.TryApplyPreview(document, "reel-1", previewed));
        var command = CanvasMutationCommands.CreateUpdateElementCommand(
            document.DocumentId,
            document,
            "reel-1",
            previewed,
            original,
            "Move element");

        document.CommandService.Execute(command);

        Assert.Single(document.CommandService.History.Entries);
        Assert.True(document.IsDirty);
        Assert.Equal(35, document.GetPanelElements().Single().X);
        Assert.Equal(55, document.GetPanelElements().Single().Y);

        Assert.True(document.CommandService.TryUndo());
        Assert.Equal(10, document.GetPanelElements().Single().X);
        Assert.Equal(20, document.GetPanelElements().Single().Y);

        Assert.True(document.CommandService.TryRedo());
        Assert.Equal(35, document.GetPanelElements().Single().X);
        Assert.Equal(55, document.GetPanelElements().Single().Y);
    }

    private static DocumentTabViewModel CreateDocument(params PanelElementModel[] elements)
    {
        var document = new DocumentTabViewModel(EditorDocument.CreatePanel2DStub("Panel"));
        document.SetPanelElements(elements);
        return document;
    }
}
