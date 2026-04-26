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
}
