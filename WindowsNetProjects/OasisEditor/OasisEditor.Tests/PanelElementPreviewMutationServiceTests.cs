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


    [Fact]
    public void ResizePreview_UpdatesGeometryWithoutRecordingUndoHistoryOrDirtyingDocument()
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
        var original = PanelElementModelCloner.Clone(document.GetPanelElements().Single());
        var resized = PanelElementModelCloner.Clone(original, x: 5, y: 15, width: 35, height: 45);

        Assert.True(PanelElementPreviewMutationService.TryApplyPreview(document, "lamp-1", resized));

        var element = document.GetPanelElements().Single();
        Assert.Equal(5, element.X);
        Assert.Equal(15, element.Y);
        Assert.Equal(35, element.Width);
        Assert.Equal(45, element.Height);
        Assert.False(document.IsDirty);
        Assert.Empty(document.CommandService.History.Entries);
    }

    [Fact]
    public void PreviewedResizeCommand_RecordsSingleUndoableActionFromOriginalToFinalGeometry()
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
        var previewed = PanelElementModelCloner.Clone(original, x: 5, y: 15, width: 35, height: 45);
        var final = PanelElementModelCloner.Clone(original, x: 1, y: 2, width: 39, height: 58);

        Assert.True(PanelElementPreviewMutationService.TryApplyPreview(document, "reel-1", previewed));
        var command = CanvasMutationCommands.CreateUpdateElementCommand(
            document.DocumentId,
            document,
            "reel-1",
            final,
            original,
            "Resize element");

        document.CommandService.Execute(command);

        Assert.Single(document.CommandService.History.Entries);
        Assert.Equal(1, document.GetPanelElements().Single().X);
        Assert.Equal(2, document.GetPanelElements().Single().Y);
        Assert.Equal(39, document.GetPanelElements().Single().Width);
        Assert.Equal(58, document.GetPanelElements().Single().Height);

        Assert.True(document.CommandService.TryUndo());
        Assert.Equal(10, document.GetPanelElements().Single().X);
        Assert.Equal(20, document.GetPanelElements().Single().Y);
        Assert.Equal(30, document.GetPanelElements().Single().Width);
        Assert.Equal(40, document.GetPanelElements().Single().Height);

        Assert.True(document.CommandService.TryRedo());
        Assert.Equal(1, document.GetPanelElements().Single().X);
        Assert.Equal(2, document.GetPanelElements().Single().Y);
        Assert.Equal(39, document.GetPanelElements().Single().Width);
        Assert.Equal(58, document.GetPanelElements().Single().Height);
    }

    [Fact]
    public void PreviewedResizeCommand_NoOpFinalGeometryDoesNotRecordUndoHistory()
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
        var original = PanelElementModelCloner.Clone(document.GetPanelElements().Single());
        var previewed = PanelElementModelCloner.Clone(original, x: 5, y: 15, width: 35, height: 45);

        Assert.True(PanelElementPreviewMutationService.TryApplyPreview(document, "lamp-1", previewed));
        Assert.True(PanelElementPreviewMutationService.TryApplyPreview(document, "lamp-1", original));

        var command = CanvasMutationCommands.CreateUpdateElementCommand(
            document.DocumentId,
            document,
            "lamp-1",
            original,
            original,
            "Resize element");
        document.CommandService.Execute(command);

        Assert.Empty(document.CommandService.History.Entries);
    }

    [Fact]
    public void CancelledResizePreview_RestoresOriginalWithoutRecordingUndoHistoryOrDirtyingDocument()
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
        var original = PanelElementModelCloner.Clone(document.GetPanelElements().Single());
        var previewed = PanelElementModelCloner.Clone(original, x: 5, y: 15, width: 35, height: 45);

        Assert.True(PanelElementPreviewMutationService.TryApplyPreview(document, "lamp-1", previewed));
        Assert.True(PanelElementPreviewMutationService.TryApplyPreview(document, "lamp-1", original));

        var element = document.GetPanelElements().Single();
        Assert.Equal(10, element.X);
        Assert.Equal(20, element.Y);
        Assert.Equal(30, element.Width);
        Assert.Equal(40, element.Height);
        Assert.False(document.IsDirty);
        Assert.Empty(document.CommandService.History.Entries);
    }

    [Fact]
    public void TryApplyPreviews_AppliesMultipleUpdatesWithOneBatchNotification()
    {
        var document = CreateDocument(
            CreateLamp("lamp-1", 10, 20),
            CreateLamp("lamp-2", 30, 40),
            CreateLamp("lamp-3", 50, 60));
        var events = new List<PanelChangeEvent>();
        document.PanelChanged += panelChange => events.Add(panelChange);

        var originals = document.GetPanelElements().ToDictionary(element => element.ObjectId);
        var updates = new Dictionary<string, PanelElementModel>
        {
            ["lamp-1"] = PanelElementModelCloner.Clone(originals["lamp-1"], x: 11, y: 21),
            ["lamp-2"] = PanelElementModelCloner.Clone(originals["lamp-2"], x: 31, y: 41),
            ["lamp-3"] = PanelElementModelCloner.Clone(originals["lamp-3"], x: 51, y: 61)
        };

        Assert.True(PanelElementPreviewMutationService.TryApplyPreviews(document, updates));

        Assert.Equal(11, document.GetPanelElements()[0].X);
        Assert.Equal(31, document.GetPanelElements()[1].X);
        Assert.Equal(51, document.GetPanelElements()[2].X);
        Assert.False(document.IsDirty);
        Assert.Empty(document.CommandService.History.Entries);

        var panelChange = Assert.Single(events);
        Assert.Null(panelChange.ObjectId);
        Assert.True(panelChange.ChangedProperties.HasFlag(PanelChangeProperties.Geometry));
        Assert.True(panelChange.AffectsCanvas);
        Assert.True(panelChange.AffectsInspectorRows);
        Assert.False(panelChange.AffectsHierarchy);
        Assert.False(panelChange.AffectsPersistence);
    }

    [Fact]
    public void TryApplyPreviews_IgnoresUnchangedInvalidIdsAndInvalidUpdatesWithoutBlockingValidUpdates()
    {
        var document = CreateDocument(
            CreateLamp("lamp-1", 10, 20),
            CreateLamp("lamp-2", 30, 40),
            CreateLamp("lamp-3", 50, 60),
            CreateLamp("lamp-4", 70, 80));
        var events = new List<PanelChangeEvent>();
        document.PanelChanged += panelChange => events.Add(panelChange);

        var originals = document.GetPanelElements().ToDictionary(element => element.ObjectId);
        var updates = new Dictionary<string, PanelElementModel>
        {
            ["lamp-1"] = PanelElementModelCloner.Clone(originals["lamp-1"], x: 15, y: 25),
            ["lamp-2"] = PanelElementModelCloner.Clone(originals["lamp-2"]),
            ["missing"] = PanelElementModelCloner.Clone(originals["lamp-3"], objectId: "missing", x: 55),
            ["lamp-3"] = new PanelElementModel
            {
                ObjectId = "lamp-3",
                Name = "Wrong kind",
                Kind = PanelElementKind.Reel,
                X = 55,
                Y = 65,
                Width = 10,
                Height = 10,
                IsVisible = true
            },
            ["lamp-4"] = PanelElementModelCloner.Clone(originals["lamp-4"], width: 0)
        };

        Assert.True(PanelElementPreviewMutationService.TryApplyPreviews(document, updates));

        Assert.Equal(15, document.GetPanelElements()[0].X);
        Assert.Equal(30, document.GetPanelElements()[1].X);
        Assert.Equal(50, document.GetPanelElements()[2].X);
        Assert.Equal(70, document.GetPanelElements()[3].X);
        Assert.False(document.IsDirty);
        Assert.Empty(document.CommandService.History.Entries);
        Assert.Single(events);
    }

    [Fact]
    public void TryApplyPreviews_ReturnsFalseAndDoesNotNotifyWhenNoElementsChange()
    {
        var document = CreateDocument(CreateLamp("lamp-1", 10, 20), CreateLamp("lamp-2", 30, 40));
        var events = new List<PanelChangeEvent>();
        document.PanelChanged += panelChange => events.Add(panelChange);
        var originals = document.GetPanelElements().ToDictionary(element => element.ObjectId);
        var updates = new Dictionary<string, PanelElementModel>
        {
            ["lamp-1"] = PanelElementModelCloner.Clone(originals["lamp-1"]),
            ["missing"] = PanelElementModelCloner.Clone(originals["lamp-2"], objectId: "missing", x: 35)
        };

        Assert.False(PanelElementPreviewMutationService.TryApplyPreviews(document, updates));

        Assert.Empty(events);
        Assert.False(document.IsDirty);
        Assert.Empty(document.CommandService.History.Entries);
    }

    [Fact]
    public void TryApplyPreviews_UpdatesNinetyLampsInOneBatch()
    {
        var elements = Enumerable.Range(0, 90)
            .Select(index => CreateLamp($"lamp-{index}", index, index + 100))
            .ToArray();
        var document = CreateDocument(elements);
        var events = new List<PanelChangeEvent>();
        document.PanelChanged += panelChange => events.Add(panelChange);
        var updates = document.GetPanelElements()
            .ToDictionary(
                element => element.ObjectId,
                element => PanelElementModelCloner.Clone(element, x: element.X + 7, y: element.Y + 9));

        Assert.True(PanelElementPreviewMutationService.TryApplyPreviews(document, updates));

        for (var i = 0; i < 90; i++)
        {
            Assert.Equal(i + 7, document.GetPanelElements()[i].X);
            Assert.Equal(i + 109, document.GetPanelElements()[i].Y);
        }

        Assert.False(document.IsDirty);
        Assert.Empty(document.CommandService.History.Entries);
        Assert.Single(events);
    }

    private static PanelElementModel CreateLamp(string objectId, double x, double y)
    {
        return new PanelElementModel
        {
            ObjectId = objectId,
            Name = objectId,
            Kind = PanelElementKind.Lamp,
            X = x,
            Y = y,
            Width = 10,
            Height = 10,
            IsVisible = true
        };
    }

    private static DocumentTabViewModel CreateDocument(params PanelElementModel[] elements)
    {
        var document = new DocumentTabViewModel(EditorDocument.CreatePanel2DStub("Panel"));
        document.SetPanelElements(elements);
        return document;
    }
}
