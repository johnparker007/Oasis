namespace OasisEditor.Tests;

public sealed class Panel2DRoundTripTests
{
    [Fact]
    public void BuildOpenDocumentData_AndBuildDocumentContent_RoundTripExistingPanelFile()
    {
        const string path = "C:/Repo/Assets/sample.panel2d";
        var sourceJson = """
        {
          "SchemaVersion": 1,
          "Title": "Sample Panel",
          "Summary": "Sample summary",
          "SavedAtUtc": "2026-01-15T12:34:56Z",
          "Elements": [
            {
              "ObjectId": "rect001",
              "Name": "Rect A",
              "Kind": "rectangle",
              "X": 12.5,
              "Y": 34.0,
              "Width": 100.0,
              "Height": 50.0
            },
            {
              "ObjectId": "img002",
              "Name": "Image B",
              "Kind": "image",
              "X": 200.0,
              "Y": 300.0,
              "Width": 64.0,
              "Height": 64.0
            }
          ]
        }
        """;

        var openData = DocumentWorkspaceViewModel.BuildOpenDocumentData(path, sourceJson);
        var document = new DocumentTabViewModel(
            EditorDocument.CreateFromFile(path, openData.Summary, openData.PanelTitle),
            openData.PanelLayoutJson);

        var savedContent = DocumentWorkspaceViewModel.BuildDocumentContent(document);

        Assert.True(Panel2DDocumentStorage.TryRead(savedContent, out var savedDocument));
        Assert.Equal(1, savedDocument.SchemaVersion);
        Assert.Equal("Sample Panel", savedDocument.Title);
        Assert.Equal("Sample summary", savedDocument.Summary);
        Assert.Collection(
            savedDocument.Elements,
            first =>
            {
                Assert.Equal("rect001", first.ObjectId);
                Assert.Equal("Rect A", first.Name);
                Assert.Equal("rectangle", first.Kind);
                Assert.Equal(12.5, first.X);
                Assert.Equal(34.0, first.Y);
                Assert.Equal(100.0, first.Width);
                Assert.Equal(50.0, first.Height);
            },
            second =>
            {
                Assert.Equal("img002", second.ObjectId);
                Assert.Equal("Image B", second.Name);
                Assert.Equal("image", second.Kind);
                Assert.Equal(200.0, second.X);
                Assert.Equal(300.0, second.Y);
                Assert.Equal(64.0, second.Width);
                Assert.Equal(64.0, second.Height);
            });
    }

    [Fact]
    public void BuildOpenDocumentData_WithInvalidPanelJson_FallsBackToPreview()
    {
        const string path = "C:/Repo/Assets/bad.panel2d";
        const string invalidJson = "{ not valid json";

        var openData = DocumentWorkspaceViewModel.BuildOpenDocumentData(path, invalidJson);

        Assert.Null(openData.PanelLayoutJson);
        Assert.Null(openData.PanelTitle);
        Assert.Contains("{ not valid json", openData.Summary);
    }

    [Fact]
    public void ToModel_AndToStorageElements_PreserveExplicitValues()
    {
        var file = new Panel2DDocumentFile
        {
            SchemaVersion = 1,
            Title = "Panel X",
            Summary = "Summary X",
            SavedAtUtc = DateTime.UtcNow,
            Elements =
            [
                new PanelElementFile
                {
                    ObjectId = "abc123",
                    Name = "Name 1",
                    Kind = "rectangle",
                    X = 1,
                    Y = 2,
                    Width = 3,
                    Height = 4
                }
            ]
        };

        var model = Panel2DDocumentStorage.ToModel(file);
        var storageElements = Panel2DDocumentStorage.ToStorageElements(model);

        var element = Assert.Single(storageElements);
        Assert.Equal("abc123", element.ObjectId);
        Assert.Equal("Name 1", element.Name);
        Assert.Equal("rectangle", element.Kind);
        Assert.Equal(1, element.X);
        Assert.Equal(2, element.Y);
        Assert.Equal(3, element.Width);
        Assert.Equal(4, element.Height);
    }

    [Fact]
    public void RenameCommand_MatchesByObjectId_WhenSelectionBoundsDiffer()
    {
        var document = CreatePanelDocument(
            new PanelElementModel
            {
                ObjectId = "first",
                Name = "First",
                Kind = PanelElementKind.Rectangle,
                X = 5,
                Y = 5,
                Width = 10,
                Height = 10
            },
            new PanelElementModel
            {
                ObjectId = "second",
                Name = "Second",
                Kind = PanelElementKind.Rectangle,
                X = 55,
                Y = 55,
                Width = 10,
                Height = 10
            });

        var selection = new PanelSelectionInfo("second", "rectangle", 5, 5, 10, 10);
        var renameCommand = CanvasMutationCommands.CreateRenameElementCommand(
            document.DocumentId,
            document,
            selection,
            "Renamed by Id");

        renameCommand.Execute();

        var byId = document.GetPanelElements().Single(e => e.ObjectId == "second");
        var other = document.GetPanelElements().Single(e => e.ObjectId == "first");
        Assert.Equal("Renamed by Id", byId.Name);
        Assert.Equal("First", other.Name);
    }

    [Fact]
    public void HierarchyViewModel_RefreshReflectsAddRenameDeleteMutations()
    {
        var document = CreatePanelDocument();
        DocumentTabViewModel? selectedDocument = document;
        var hierarchy = new HierarchyViewModel(
            () => selectedDocument,
            [new Panel2DHierarchyProvider()]);

        hierarchy.Refresh();
        Assert.Equal("Rectangles (0)", hierarchy.Items.Single(i => i.NodeKey == "group:rectangle").Label);

        var element = new PanelElementFile
        {
            ObjectId = "rect-id",
            Name = "Rect Original",
            Kind = "rectangle",
            X = 10,
            Y = 20,
            Width = 30,
            Height = 40
        };

        CanvasMutationCommands.CreateAddRectangleCommand(document.DocumentId, document, element).Execute();
        hierarchy.Refresh();
        var rectangleGroupAfterAdd = hierarchy.Items.Single(i => i.NodeKey == "group:rectangle");
        var addedItem = Assert.Single(rectangleGroupAfterAdd.Children);
        Assert.Equal("Rectangles (1)", rectangleGroupAfterAdd.Label);
        Assert.Equal("Rect Original", addedItem.Label);

        var selection = addedItem.PanelSelection!.Value;
        CanvasMutationCommands.CreateRenameElementCommand(document.DocumentId, document, selection, "Rect Renamed").Execute();
        hierarchy.Refresh();
        var rectangleGroupAfterRename = hierarchy.Items.Single(i => i.NodeKey == "group:rectangle");
        Assert.Equal("Rect Renamed", Assert.Single(rectangleGroupAfterRename.Children).Label);

        CanvasMutationCommands.CreateDeleteElementCommand(document.DocumentId, document, selection).Execute();
        hierarchy.Refresh();
        var rectangleGroupAfterDelete = hierarchy.Items.Single(i => i.NodeKey == "group:rectangle");
        Assert.Empty(rectangleGroupAfterDelete.Children);
        Assert.Equal("Rectangles (0)", rectangleGroupAfterDelete.Label);
    }

    private static DocumentTabViewModel CreatePanelDocument(params PanelElementModel[] elements)
    {
        var document = new DocumentTabViewModel(EditorDocument.CreatePanel2DStub("Panel"));
        document.SetPanelElements(elements);
        return document;
    }
}
