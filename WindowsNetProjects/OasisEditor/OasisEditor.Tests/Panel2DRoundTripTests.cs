using Xunit;
using System.Windows;
using System.Collections.ObjectModel;

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
    public void BuildOpenDocumentData_WithInvalidPanelJson_ReturnsClearErrorSummary()
    {
        const string path = "C:/Repo/Assets/bad.panel2d";
        const string invalidJson = "{ not valid json";

        var openData = DocumentWorkspaceViewModel.BuildOpenDocumentData(path, invalidJson);

        Assert.Null(openData.PanelLayoutJson);
        Assert.Equal("bad.panel2d", openData.PanelTitle);
        Assert.Contains("Malformed JSON", openData.Summary);
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
                    Kind = "lamp",
                    X = 1,
                    Y = 2,
                    Width = 3,
                    Height = 4,
                    AssetPath = "Assets/MfmeImport/layout/Lamps/lamp.png",
                    SecondaryAssetPath = "Assets/MfmeImport/layout/Lamps/lamp-off.png",
                    DisplayNumber = 8,
                    OnColorHex = "#FFFFFFFF",
                    OffColorHex = "#FF111111",
                    TextColorHex = "#FFFF0000",
                    DisplayText = "HOLD",
                    IsReversed = true,
                    Stops = 24,
                    VisibleScale = 0.75,
                    ImportSource = new PanelElementImportSourceFile
                    {
                        Format = "LegacyImport",
                        Reference = "layout.json#lamp-8"
                    }
                }
            ]
        };

        var model = Panel2DDocumentStorage.ToModel(file);
        var storageElements = Panel2DDocumentStorage.ToStorageElements(model);

        var element = Assert.Single(storageElements);
        Assert.Equal("abc123", element.ObjectId);
        Assert.Equal("Name 1", element.Name);
        Assert.Equal("lamp", element.Kind);
        Assert.Equal(1, element.X);
        Assert.Equal(2, element.Y);
        Assert.Equal(3, element.Width);
        Assert.Equal(4, element.Height);
        Assert.Equal("Assets/MfmeImport/layout/Lamps/lamp.png", element.AssetPath);
        Assert.Equal("Assets/MfmeImport/layout/Lamps/lamp-off.png", element.SecondaryAssetPath);
        Assert.Equal(8, element.DisplayNumber);
        Assert.Equal("#FFFFFFFF", element.OnColorHex);
        Assert.Equal("#FF111111", element.OffColorHex);
        Assert.Equal("#FFFF0000", element.TextColorHex);
        Assert.Equal("HOLD", element.DisplayText);
        Assert.True(element.IsReversed);
        Assert.Equal(24, element.Stops);
        Assert.Equal(0.75, element.VisibleScale);
        Assert.NotNull(element.ImportSource);
        Assert.Equal("LegacyImport", element.ImportSource!.Format);
        Assert.Equal("layout.json#lamp-8", element.ImportSource.Reference);
    }

    [Fact]
    public void Serialize_WithNativeElementMetadata_WritesSchemaVersion2()
    {
        var json = Panel2DDocumentStorage.Serialize(
            "Panel V2",
            "Native",
            [
                new PanelElementFile
                {
                    ObjectId = "lamp-v2",
                    Name = "Lamp V2",
                    Kind = "lamp",
                    X = 1,
                    Y = 2,
                    Width = 3,
                    Height = 4,
                    Native = new PanelElementNativeFile
                    {
                        Number = 12
                    }
                }
            ]);

        Assert.True(Panel2DDocumentStorage.TryRead(json, out var parsed));
        Assert.Equal(Panel2DDocumentStorage.CurrentSchemaVersion, parsed.SchemaVersion);
    }

    [Fact]
    public void ToStorageElement_WithLegacyRectangle_DoesNotEmitNativePayload()
    {
        var model = new PanelElementModel
        {
            ObjectId = "rect-1",
            Name = "Rect 1",
            Kind = PanelElementKind.Rectangle,
            X = 10,
            Y = 20,
            Width = 30,
            Height = 40
        };

        var storage = Panel2DDocumentStorage.ToStorageElement(model);

        Assert.Null(storage.Native);
    }

    [Fact]
    public void TryReadValidated_WithFutureSchemaVersion_ReturnsUnsupportedVersionError()
    {
        const string sourceJson = """
        {
          "SchemaVersion": 3,
          "Title": "Future Panel",
          "Summary": "Future",
          "Elements": []
        }
        """;

        var success = Panel2DDocumentStorage.TryReadValidated(sourceJson, out _, out var errorMessage);

        Assert.False(success);
        Assert.Contains("Unsupported schema version '3'", errorMessage);
    }

    [Fact]
    public void TryReadValidated_WithSchemaVersion1_MigratesToCurrentSchema()
    {
        const string sourceJson = """
        {
          "SchemaVersion": 1,
          "Title": "Legacy Panel",
          "Summary": "Legacy",
          "Elements": [
            {
              "ObjectId": "legacy-lamp",
              "Name": "Legacy Lamp",
              "Kind": "lamp",
              "X": 10,
              "Y": 20,
              "Width": 30,
              "Height": 40,
              "AssetPath": " Assets/legacy/lamp.png ",
              "DisplayNumber": 7,
              "OnColorHex": " #FFFFFFFF "
            }
          ]
        }
        """;

        var success = Panel2DDocumentStorage.TryReadValidated(sourceJson, out var parsed, out var errorMessage);

        Assert.True(success);
        Assert.Equal(string.Empty, errorMessage);
        Assert.Equal(Panel2DDocumentStorage.CurrentSchemaVersion, parsed.SchemaVersion);
        var element = Assert.Single(parsed.Elements);
        Assert.Equal("Assets/legacy/lamp.png", element.AssetPath);
        Assert.Equal(7, element.DisplayNumber);
        Assert.Equal("#FFFFFFFF", element.OnColorHex);
        Assert.NotNull(element.Native);
        Assert.Equal("Assets/legacy/lamp.png", element.Native!.AssetPath);
        Assert.Equal(7, element.Native.Number);
        Assert.Equal("#FFFFFFFF", element.Native.OnColorHex);
    }

    [Fact]
    public void TryReadValidated_WithInvalidElementKind_ReturnsValidationError()
    {
        const string sourceJson = """
        {
          "SchemaVersion": 1,
          "Title": "Invalid Kind Panel",
          "Summary": "Invalid",
          "Elements": [
            {
              "ObjectId": "obj-1",
              "Name": "Bad Kind",
              "Kind": "triangle",
              "X": 1,
              "Y": 2,
              "Width": 3,
              "Height": 4
            }
          ]
        }
        """;

        var success = Panel2DDocumentStorage.TryReadValidated(sourceJson, out _, out var errorMessage);

        Assert.False(success);
        Assert.Contains("unsupported kind", errorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryReadValidated_WithDuplicateObjectIds_ReturnsValidationError()
    {
        const string sourceJson = """
        {
          "SchemaVersion": 1,
          "Title": "Duplicate ObjectIds",
          "Summary": "Invalid",
          "Elements": [
            {
              "ObjectId": "dup-1",
              "Name": "A",
              "Kind": "rectangle",
              "X": 1,
              "Y": 2,
              "Width": 3,
              "Height": 4
            },
            {
              "ObjectId": "dup-1",
              "Name": "B",
              "Kind": "image",
              "X": 5,
              "Y": 6,
              "Width": 7,
              "Height": 8
            }
          ]
        }
        """;

        var success = Panel2DDocumentStorage.TryReadValidated(sourceJson, out _, out var errorMessage);

        Assert.False(success);
        Assert.Contains("duplicated", errorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryReadValidated_WithInvalidNativeMetadata_ReturnsValidationError()
    {
        const string sourceJson = """
        {
          "SchemaVersion": 1,
          "Title": "Invalid Native Metadata",
          "Summary": "Invalid",
          "Elements": [
            {
              "ObjectId": "lamp-1",
              "Name": "Lamp 1",
              "Kind": "lamp",
              "X": 1,
              "Y": 2,
              "Width": 3,
              "Height": 4,
              "DisplayNumber": -1
            }
          ]
        }
        """;

        var success = Panel2DDocumentStorage.TryReadValidated(sourceJson, out _, out var errorMessage);

        Assert.False(success);
        Assert.Contains("invalid display number", errorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryReadValidated_NormalizesOptionalNativeMetadata()
    {
        const string sourceJson = """
        {
          "SchemaVersion": 1,
          "Title": "Normalize Native Metadata",
          "Summary": "Normalize",
          "Elements": [
            {
              "ObjectId": "alpha-1",
              "Name": "Alpha",
              "Kind": "alpha",
              "X": 1,
              "Y": 2,
              "Width": 3,
              "Height": 4,
              "AssetPath": "  Assets/alpha.png  ",
              "DisplayText": "  HELLO  ",
              "ImportSource": {
                "Format": "  LegacyImport  ",
                "Reference": "  layout.json#alpha-1  "
              }
            }
          ]
        }
        """;

        var success = Panel2DDocumentStorage.TryReadValidated(sourceJson, out var normalized, out var errorMessage);

        Assert.True(success);
        Assert.Equal(string.Empty, errorMessage);
        var element = Assert.Single(normalized.Elements);
        Assert.Equal("Assets/alpha.png", element.AssetPath);
        Assert.Equal("HELLO", element.DisplayText);
        Assert.NotNull(element.ImportSource);
        Assert.Equal("LegacyImport", element.ImportSource!.Format);
        Assert.Equal("layout.json#alpha-1", element.ImportSource.Reference);
    }

    [Theory]
    [InlineData("background", "background")]
    [InlineData("lamp", "lamp")]
    [InlineData("reel", "reel")]
    [InlineData("sevenSegment", "sevenSegment")]
    [InlineData("alpha", "alpha")]
    public void ParseElementKind_WithNativeKinds_ReturnsExpectedKind(string serializedKind, string expectedRoundTripKind)
    {
        var parsedKind = Panel2DDocumentStorage.ParseElementKind(serializedKind);
        var roundTripKind = Panel2DDocumentStorage.SerializeElementKind(parsedKind);

        Assert.NotEqual(PanelElementKind.Unknown, parsedKind);
        Assert.Equal(expectedRoundTripKind, roundTripKind);
    }

    [Theory]
    [InlineData("background", "Background ")]
    [InlineData("lamp", "Lamp ")]
    [InlineData("reel", "Reel ")]
    [InlineData("sevenSegment", "7 Segment ")]
    [InlineData("alpha", "Alpha ")]
    public void CreateDefaultElementName_WithNativeKinds_UsesNativePrefix(string kind, string expectedPrefix)
    {
        var name = Panel2DDocumentStorage.CreateDefaultElementName(kind, "abcdef123456");

        Assert.StartsWith(expectedPrefix, name);
    }

    [Fact]
    public void TryReadValidated_WithMissingNameAndObjectId_NormalizesValues()
    {
        const string sourceJson = """
        {
          "SchemaVersion": 1,
          "Title": "Normalizable Panel",
          "Summary": "Normalizable",
          "Elements": [
            {
              "ObjectId": "",
              "Name": "",
              "Kind": "rectangle",
              "X": 1,
              "Y": 2,
              "Width": 30,
              "Height": 40
            }
          ]
        }
        """;

        var success = Panel2DDocumentStorage.TryReadValidated(sourceJson, out var parsed, out var errorMessage);

        Assert.True(success);
        Assert.Equal(string.Empty, errorMessage);
        var element = Assert.Single(parsed.Elements);
        Assert.False(string.IsNullOrWhiteSpace(element.ObjectId));
        Assert.StartsWith("Rectangle ", element.Name);
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
        Assert.Equal("Rectangles (0)", hierarchy.Items.Single(i => i.NodeKey == "group:rectangle").DisplayName);

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
        Assert.Equal("Rectangles (1)", rectangleGroupAfterAdd.DisplayName);
        Assert.Equal("Rect Original", addedItem.DisplayName);

        var selection = addedItem.PanelSelection!.Value;
        CanvasMutationCommands.CreateRenameElementCommand(document.DocumentId, document, selection, "Rect Renamed").Execute();
        hierarchy.Refresh();
        var rectangleGroupAfterRename = hierarchy.Items.Single(i => i.NodeKey == "group:rectangle");
        Assert.Equal("Rect Renamed", Assert.Single(rectangleGroupAfterRename.Children).DisplayName);

        CanvasMutationCommands.CreateDeleteElementCommand(document.DocumentId, document, selection).Execute();
        hierarchy.Refresh();
        var rectangleGroupAfterDelete = hierarchy.Items.Single(i => i.NodeKey == "group:rectangle");
        Assert.Empty(rectangleGroupAfterDelete.Children);
        Assert.Equal("Rectangles (0)", rectangleGroupAfterDelete.DisplayName);
    }

    [Fact]
    public void HierarchyProvider_IncludesNativeComponentGroups()
    {
        var document = CreatePanelDocument(
            new PanelElementModel
            {
                ObjectId = "background-1",
                Name = "Background",
                Kind = PanelElementKind.Background,
                X = 0,
                Y = 0,
                Width = 100,
                Height = 100
            },
            new PanelElementModel
            {
                ObjectId = "lamp-1",
                Name = "Lamp 1",
                Kind = PanelElementKind.Lamp,
                X = 10,
                Y = 12,
                Width = 20,
                Height = 24
            },
            new PanelElementModel
            {
                ObjectId = "reel-1",
                Name = "Reel 1",
                Kind = PanelElementKind.Reel,
                X = 32,
                Y = 16,
                Width = 26,
                Height = 60
            },
            new PanelElementModel
            {
                ObjectId = "seven-1",
                Name = "7 Segment 2",
                Kind = PanelElementKind.SevenSegment,
                X = 64,
                Y = 20,
                Width = 30,
                Height = 16
            },
            new PanelElementModel
            {
                ObjectId = "alpha-1",
                Name = "Alpha",
                Kind = PanelElementKind.Alpha,
                X = 96,
                Y = 24,
                Width = 34,
                Height = 18
            });

        var provider = new Panel2DHierarchyProvider();
        var groups = provider.Build(document);

        Assert.Equal("Backgrounds (1)", groups.Single(g => g.NodeKey == "group:background").DisplayName);
        Assert.Equal("Lamps (1)", groups.Single(g => g.NodeKey == "group:lamp").DisplayName);
        Assert.Equal("Reels (1)", groups.Single(g => g.NodeKey == "group:reel").DisplayName);
        Assert.Equal("Seven Segments (1)", groups.Single(g => g.NodeKey == "group:sevenSegment").DisplayName);
        Assert.Equal("Alphas (1)", groups.Single(g => g.NodeKey == "group:alpha").DisplayName);
    }

    [Fact]
    public void NativeImportedKinds_SupportSelectionBasedRenameUndoRedo_AndSaveLoadRoundTrip()
    {
        var document = CreatePanelDocument(
            new PanelElementModel
            {
                ObjectId = "background-1",
                Name = "Background",
                Kind = PanelElementKind.Background,
                X = 0,
                Y = 0,
                Width = 200,
                Height = 100
            },
            new PanelElementModel
            {
                ObjectId = "lamp-1",
                Name = "Lamp 1",
                Kind = PanelElementKind.Lamp,
                X = 10,
                Y = 20,
                Width = 30,
                Height = 40,
                DisplayNumber = 1
            },
            new PanelElementModel
            {
                ObjectId = "reel-1",
                Name = "Reel 2",
                Kind = PanelElementKind.Reel,
                X = 50,
                Y = 60,
                Width = 70,
                Height = 80,
                DisplayNumber = 2
            },
            new PanelElementModel
            {
                ObjectId = "seven-1",
                Name = "7 Segment 3",
                Kind = PanelElementKind.SevenSegment,
                X = 90,
                Y = 30,
                Width = 45,
                Height = 20,
                DisplayNumber = 3
            },
            new PanelElementModel
            {
                ObjectId = "alpha-1",
                Name = "Alpha",
                Kind = PanelElementKind.Alpha,
                X = 130,
                Y = 50,
                Width = 55,
                Height = 25,
                IsReversed = true
            });
        var workspace = CreateWorkspace(document, document);

        var provider = new Panel2DHierarchyProvider();
        var hierarchyItems = provider.Build(document)
            .Where(group => group.NodeKey is "group:background" or "group:lamp" or "group:reel" or "group:sevenSegment" or "group:alpha")
            .SelectMany(group => group.Children)
            .ToList();
        Assert.Equal(5, hierarchyItems.Count);

        foreach (var item in hierarchyItems)
        {
            Assert.NotNull(item.PanelSelection);

            var selection = item.PanelSelection!.Value;
            var renameCommand = CanvasMutationCommands.CreateRenameElementCommand(
                document.DocumentId,
                document,
                selection,
                $"{item.DisplayName} Renamed");

            Assert.True(workspace.ExecuteDocumentCanvasCommand(document.DocumentId, renameCommand));
        }

        Assert.Equal(5, document.CommandService.History.Entries.Count);
        Assert.All(document.GetPanelElements(), element => Assert.EndsWith("Renamed", element.Name));

        for (var index = 0; index < 5; index++)
        {
            Assert.True(workspace.UndoActiveDocument());
        }

        Assert.Collection(
            document.GetPanelElements().OrderBy(element => element.ObjectId),
            element => Assert.Equal("Alpha", element.Name),
            element => Assert.Equal("Background", element.Name),
            element => Assert.Equal("Lamp 1", element.Name),
            element => Assert.Equal("Reel 2", element.Name),
            element => Assert.Equal("7 Segment 3", element.Name));

        for (var index = 0; index < 5; index++)
        {
            Assert.True(document.CommandService.TryRedo());
        }

        Assert.All(document.GetPanelElements(), element => Assert.EndsWith("Renamed", element.Name));

        var savedContent = DocumentWorkspaceViewModel.BuildDocumentContent(document);
        Assert.True(Panel2DDocumentStorage.TryReadValidated(savedContent, out var roundTripped, out var error), error);
        Assert.Equal(5, roundTripped.Elements.Count());
        Assert.Contains(roundTripped.Elements, element => element.Kind == "background");
        Assert.Contains(roundTripped.Elements, element => element.Kind == "lamp");
        Assert.Contains(roundTripped.Elements, element => element.Kind == "reel");
        Assert.Contains(roundTripped.Elements, element => element.Kind == "sevenSegment");
        Assert.Contains(roundTripped.Elements, element => element.Kind == "alpha");
    }

    [Fact]
    public void DeleteElementCommand_TracksExecutionAndSupportsUndoRedo()
    {
        var document = CreatePanelDocument(
            new PanelElementModel
            {
                ObjectId = "rect-1",
                Name = "Rect 1",
                Kind = PanelElementKind.Rectangle,
                X = 12,
                Y = 18,
                Width = 24,
                Height = 30
            });

        var selection = new PanelSelectionInfo("rect-1", "rectangle", 12, 18, 24, 30);
        var command = CanvasMutationCommands.CreateDeleteElementCommand(document.DocumentId, document, selection);

        Assert.IsAssignableFrom<Commands.IExecutionTrackedCommand>(command);
        var tracked = (Commands.IExecutionTrackedCommand)command;

        command.Execute();
        Assert.True(tracked.WasExecuted);
        Assert.Empty(document.GetPanelElements());

        command.Undo();
        var restored = Assert.Single(document.GetPanelElements());
        Assert.Equal("rect-1", restored.ObjectId);

        command.Execute();
        Assert.True(tracked.WasExecuted);
        Assert.Empty(document.GetPanelElements());
    }

    [Fact]
    public void DeleteElementCommand_NoMatchingSelection_DoesNotExecute()
    {
        var document = CreatePanelDocument(
            new PanelElementModel
            {
                ObjectId = "rect-1",
                Name = "Rect 1",
                Kind = PanelElementKind.Rectangle,
                X = 10,
                Y = 10,
                Width = 20,
                Height = 20
            });

        var missingSelection = new PanelSelectionInfo("missing-id", "rectangle", 10, 10, 20, 20);
        var command = CanvasMutationCommands.CreateDeleteElementCommand(document.DocumentId, document, missingSelection);
        var tracked = Assert.IsAssignableFrom<Commands.IExecutionTrackedCommand>(command);

        command.Execute();

        Assert.False(tracked.WasExecuted);
        Assert.Single(document.GetPanelElements());
    }

    [Fact]
    public void ExecuteDocumentCanvasCommand_RenameNoOp_DoesNotRecordHistory()
    {
        var document = CreatePanelDocument(
            new PanelElementModel
            {
                ObjectId = "rect-1",
                Name = "Rect 1",
                Kind = PanelElementKind.Rectangle,
                X = 10,
                Y = 10,
                Width = 20,
                Height = 20
            });
        var workspace = CreateWorkspace(document, document);

        var noOpRename = CanvasMutationCommands.CreateRenameElementCommand(
            document.DocumentId,
            document,
            new PanelSelectionInfo("rect-1", "rectangle", 10, 10, 20, 20),
            "  Rect 1  ");

        var executed = workspace.ExecuteDocumentCanvasCommand(document.DocumentId, noOpRename);

        Assert.False(executed);
        Assert.Empty(document.CommandService.History.Entries);
        Assert.Equal("Rect 1", document.GetPanelElements().Single().Name);
    }

    [Fact]
    public void ExecuteDocumentCanvasCommand_DeleteMissingSelection_DoesNotRecordHistory()
    {
        var document = CreatePanelDocument(
            new PanelElementModel
            {
                ObjectId = "rect-1",
                Name = "Rect 1",
                Kind = PanelElementKind.Rectangle,
                X = 10,
                Y = 10,
                Width = 20,
                Height = 20
            });
        var workspace = CreateWorkspace(document, document);

        var deleteMissing = CanvasMutationCommands.CreateDeleteElementCommand(
            document.DocumentId,
            document,
            new PanelSelectionInfo("missing-id", "rectangle", 10, 10, 20, 20));

        var executed = workspace.ExecuteDocumentCanvasCommand(document.DocumentId, deleteMissing);

        Assert.False(executed);
        Assert.Empty(document.CommandService.History.Entries);
        Assert.Single(document.GetPanelElements());
    }

    [Fact]
    public void ExecuteDocumentCanvasCommand_DuplicateMissingSelection_DoesNotRecordHistory()
    {
        var document = CreatePanelDocument(
            new PanelElementModel
            {
                ObjectId = "rect-1",
                Name = "Rect 1",
                Kind = PanelElementKind.Rectangle,
                X = 10,
                Y = 10,
                Width = 20,
                Height = 20
            });
        var workspace = CreateWorkspace(document, document);

        var duplicateMissing = CanvasMutationCommands.CreateDuplicateElementCommand(
            document.DocumentId,
            document,
            new PanelSelectionInfo("missing-id", "rectangle", 10, 10, 20, 20));

        var executed = workspace.ExecuteDocumentCanvasCommand(document.DocumentId, duplicateMissing);

        Assert.False(executed);
        Assert.Empty(document.CommandService.History.Entries);
        Assert.Single(document.GetPanelElements());
    }

    [Fact]
    public void DuplicateElementCommand_CreatesOffsetElementWithNewObjectIdAndCopyName()
    {
        var document = CreatePanelDocument(
            new PanelElementModel
            {
                ObjectId = "source-id",
                Name = "Rect Original",
                Kind = PanelElementKind.Rectangle,
                X = 30,
                Y = 40,
                Width = 50,
                Height = 60
            });

        var selection = new PanelSelectionInfo("source-id", "rectangle", 30, 40, 50, 60);
        var command = CanvasMutationCommands.CreateDuplicateElementCommand(document.DocumentId, document, selection);
        var tracked = Assert.IsAssignableFrom<Commands.IExecutionTrackedCommand>(command);

        command.Execute();

        Assert.True(tracked.WasExecuted);
        Assert.Equal(2, document.GetPanelElements().Count);

        var source = document.GetPanelElements().Single(element => element.ObjectId == "source-id");
        var duplicate = document.GetPanelElements().Single(element => element.ObjectId != "source-id");
        Assert.Equal("Rect Original Copy", duplicate.Name);
        Assert.Equal(source.Kind, duplicate.Kind);
        Assert.Equal(source.X + 10, duplicate.X);
        Assert.Equal(source.Y + 10, duplicate.Y);
        Assert.Equal(source.Width, duplicate.Width);
        Assert.Equal(source.Height, duplicate.Height);
    }

    [Fact]
    public void DuplicateElementCommand_UsesIncrementedCopyNameWhenNeeded()
    {
        var document = CreatePanelDocument(
            new PanelElementModel
            {
                ObjectId = "source-id",
                Name = "Rect Original",
                Kind = PanelElementKind.Rectangle,
                X = 10,
                Y = 20,
                Width = 30,
                Height = 40
            },
            new PanelElementModel
            {
                ObjectId = "existing-copy",
                Name = "Rect Original Copy",
                Kind = PanelElementKind.Rectangle,
                X = 100,
                Y = 100,
                Width = 30,
                Height = 40
            });

        var selection = new PanelSelectionInfo("source-id", "rectangle", 10, 20, 30, 40);
        var command = CanvasMutationCommands.CreateDuplicateElementCommand(document.DocumentId, document, selection);

        command.Execute();

        var duplicate = document.GetPanelElements().Single(element =>
            element.ObjectId != "source-id"
            && element.ObjectId != "existing-copy");
        Assert.Equal("Rect Original Copy 2", duplicate.Name);
    }

    [Fact]
    public void PasteElementCommand_CreatesOffsetElementWithNewObjectIdAndSupportsUndo()
    {
        var document = CreatePanelDocument(
            new PanelElementModel
            {
                ObjectId = "source-id",
                Name = "Rect Original",
                Kind = PanelElementKind.Rectangle,
                X = 30,
                Y = 40,
                Width = 50,
                Height = 60
            });

        var source = document.GetPanelElements().Single();
        var command = CanvasMutationCommands.CreatePasteElementCommand(document.DocumentId, document, source);
        var tracked = Assert.IsAssignableFrom<Commands.IExecutionTrackedCommand>(command);

        command.Execute();

        Assert.True(tracked.WasExecuted);
        Assert.Equal(2, document.GetPanelElements().Count);
        var pasted = document.GetPanelElements().Single(element => element.ObjectId != "source-id");
        Assert.Equal("Rect Original (2)", pasted.Name);
        Assert.Equal(40, pasted.X);
        Assert.Equal(50, pasted.Y);
        Assert.Equal(50, pasted.Width);
        Assert.Equal(60, pasted.Height);

        command.Undo();
        Assert.Single(document.GetPanelElements());
    }

    [Fact]
    public void ExecuteDocumentCanvasCommand_PasteCommand_GeneratesUniqueObjectIdPerExecution()
    {
        var document = CreatePanelDocument(
            new PanelElementModel
            {
                ObjectId = "source-id",
                Name = "Rect Original",
                Kind = PanelElementKind.Rectangle,
                X = 30,
                Y = 40,
                Width = 50,
                Height = 60
            });
        var workspace = CreateWorkspace(document, document);
        var source = document.GetPanelElements().Single();

        var firstPaste = CanvasMutationCommands.CreatePasteElementCommand(document.DocumentId, document, source);
        var secondPaste = CanvasMutationCommands.CreatePasteElementCommand(document.DocumentId, document, source);

        Assert.True(workspace.ExecuteDocumentCanvasCommand(document.DocumentId, firstPaste));
        Assert.True(workspace.ExecuteDocumentCanvasCommand(document.DocumentId, secondPaste));

        var ids = document.GetPanelElements().Select(element => element.ObjectId).ToList();
        Assert.Equal(3, ids.Count);
        Assert.Equal(3, ids.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(2, document.CommandService.History.Entries.Count);
    }

    [Fact]
    public void ExecuteDocumentCanvasCommand_DuplicateCommand_MarksDirtyOnlyForRealMutation()
    {
        var document = CreatePanelDocument(
            new PanelElementModel
            {
                ObjectId = "rect-1",
                Name = "Rect 1",
                Kind = PanelElementKind.Rectangle,
                X = 10,
                Y = 10,
                Width = 20,
                Height = 20
            });
        var workspace = CreateWorkspace(document, document);

        var missingSelectionDuplicate = CanvasMutationCommands.CreateDuplicateElementCommand(
            document.DocumentId,
            document,
            new PanelSelectionInfo("missing-id", "rectangle", 10, 10, 20, 20));
        var noOpExecuted = workspace.ExecuteDocumentCanvasCommand(document.DocumentId, missingSelectionDuplicate);

        Assert.False(noOpExecuted);
        Assert.False(document.IsDirty);
        Assert.Empty(document.CommandService.History.Entries);

        var validDuplicate = CanvasMutationCommands.CreateDuplicateElementCommand(
            document.DocumentId,
            document,
            new PanelSelectionInfo("rect-1", "rectangle", 10, 10, 20, 20));
        var duplicateExecuted = workspace.ExecuteDocumentCanvasCommand(document.DocumentId, validDuplicate);

        Assert.True(duplicateExecuted);
        Assert.True(document.IsDirty);
        Assert.Single(document.CommandService.History.Entries);
    }

    [Fact]
    public void ExecuteDocumentCanvasCommand_PasteCommand_ReExecutingSameCommandDoesNotRecordNoOp()
    {
        var document = CreatePanelDocument(
            new PanelElementModel
            {
                ObjectId = "source-id",
                Name = "Rect Original",
                Kind = PanelElementKind.Rectangle,
                X = 30,
                Y = 40,
                Width = 50,
                Height = 60
            });
        var workspace = CreateWorkspace(document, document);
        var source = document.GetPanelElements().Single();
        var pasteCommand = CanvasMutationCommands.CreatePasteElementCommand(document.DocumentId, document, source);

        var firstExecution = workspace.ExecuteDocumentCanvasCommand(document.DocumentId, pasteCommand);
        var secondExecution = workspace.ExecuteDocumentCanvasCommand(document.DocumentId, pasteCommand);

        Assert.True(firstExecution);
        Assert.False(secondExecution);
        Assert.True(document.IsDirty);
        Assert.Single(document.CommandService.History.Entries);
        Assert.Equal(2, document.GetPanelElements().Count);
    }

    [Fact]
    public void UpdateElementCommand_UpdatesTargetedElementAndSupportsUndoRedo()
    {
        var document = CreatePanelDocument(
            new PanelElementModel
            {
                ObjectId = "target-id",
                Name = "Target",
                Kind = PanelElementKind.Rectangle,
                X = 10,
                Y = 20,
                Width = 30,
                Height = 40
            },
            new PanelElementModel
            {
                ObjectId = "other-id",
                Name = "Other",
                Kind = PanelElementKind.Rectangle,
                X = 1,
                Y = 2,
                Width = 3,
                Height = 4
            });
        var workspace = CreateWorkspace(document, document);
        var updated = new PanelElementModel
        {
            ObjectId = "target-id",
            Name = "Target Renamed",
            Kind = PanelElementKind.Rectangle,
            X = 50,
            Y = 60,
            Width = 70,
            Height = 80
        };

        var command = CanvasMutationCommands.CreateUpdateElementCommand(
            document.DocumentId,
            document,
            "target-id",
            updated,
            "Inspector update");

        Assert.True(workspace.ExecuteDocumentCanvasCommand(document.DocumentId, command));

        var mutatedTarget = document.GetPanelElements().Single(element => element.ObjectId == "target-id");
        var untouched = document.GetPanelElements().Single(element => element.ObjectId == "other-id");
        Assert.Equal("Target Renamed", mutatedTarget.Name);
        Assert.Equal(50, mutatedTarget.X);
        Assert.Equal(60, mutatedTarget.Y);
        Assert.Equal(70, mutatedTarget.Width);
        Assert.Equal(80, mutatedTarget.Height);
        Assert.Equal("Other", untouched.Name);

        Assert.True(workspace.UndoActiveDocument());
        var restoredTarget = document.GetPanelElements().Single(element => element.ObjectId == "target-id");
        Assert.Equal("Target", restoredTarget.Name);
        Assert.Equal(10, restoredTarget.X);
        Assert.Equal(20, restoredTarget.Y);
        Assert.Equal(30, restoredTarget.Width);
        Assert.Equal(40, restoredTarget.Height);

        Assert.True(workspace.RedoActiveDocument());
        var redoneTarget = document.GetPanelElements().Single(element => element.ObjectId == "target-id");
        Assert.Equal("Target Renamed", redoneTarget.Name);
    }

    [Fact]
    public void ExecuteDocumentCanvasCommand_UpdateElementNoOp_DoesNotRecordHistory()
    {
        var document = CreatePanelDocument(
            new PanelElementModel
            {
                ObjectId = "rect-1",
                Name = "Rect 1",
                Kind = PanelElementKind.Rectangle,
                X = 10,
                Y = 10,
                Width = 20,
                Height = 20
            });
        var workspace = CreateWorkspace(document, document);
        var unchanged = document.GetPanelElements().Single();

        var command = CanvasMutationCommands.CreateUpdateElementCommand(
            document.DocumentId,
            document,
            "rect-1",
            PanelElementModelCloner.Clone(unchanged),
            "Inspector update");

        var executed = workspace.ExecuteDocumentCanvasCommand(document.DocumentId, command);

        Assert.False(executed);
        Assert.Empty(document.CommandService.History.Entries);
        Assert.Equal("Rect 1", document.GetPanelElements().Single().Name);
    }

    [Fact]
    public void ExecuteDocumentCanvasCommand_UpdateElementMissingObject_DoesNotRecordHistory()
    {
        var document = CreatePanelDocument(
            new PanelElementModel
            {
                ObjectId = "rect-1",
                Name = "Rect 1",
                Kind = PanelElementKind.Rectangle,
                X = 10,
                Y = 10,
                Width = 20,
                Height = 20
            });
        var workspace = CreateWorkspace(document, document);
        var updated = new PanelElementModel
        {
            ObjectId = "missing-id",
            Name = "Renamed",
            Kind = PanelElementKind.Rectangle,
            X = 1,
            Y = 2,
            Width = 3,
            Height = 4
        };

        var command = CanvasMutationCommands.CreateUpdateElementCommand(
            document.DocumentId,
            document,
            "missing-id",
            updated,
            "Inspector update");

        var executed = workspace.ExecuteDocumentCanvasCommand(document.DocumentId, command);

        Assert.False(executed);
        Assert.Empty(document.CommandService.History.Entries);
    }

    [Fact]
    public void ExecuteDocumentCanvasCommand_UpdateElementInvalidWidth_DoesNotExecute()
    {
        var document = CreatePanelDocument(
            new PanelElementModel
            {
                ObjectId = "rect-1",
                Name = "Rect 1",
                Kind = PanelElementKind.Rectangle,
                X = 10,
                Y = 10,
                Width = 20,
                Height = 20
            });
        var workspace = CreateWorkspace(document, document);

        var invalid = new PanelElementModel
        {
            ObjectId = "rect-1",
            Name = "Rect Invalid",
            Kind = PanelElementKind.Rectangle,
            X = 10,
            Y = 10,
            Width = 0,
            Height = 20
        };
        var command = CanvasMutationCommands.CreateUpdateElementCommand(
            document.DocumentId,
            document,
            "rect-1",
            invalid,
            "Inspector update");

        var executed = workspace.ExecuteDocumentCanvasCommand(document.DocumentId, command);

        Assert.False(executed);
        Assert.Empty(document.CommandService.History.Entries);
        Assert.Equal(20, document.GetPanelElements().Single().Width);
    }

    [Fact]
    public void UpdateElementCommand_GeometryOnlyChange_EmitsPanelChangeWithoutHierarchyRefreshFlag()
    {
        var document = CreatePanelDocument(
            new PanelElementModel
            {
                ObjectId = "rect-1",
                Name = "Rect 1",
                Kind = PanelElementKind.Rectangle,
                X = 10,
                Y = 10,
                Width = 20,
                Height = 20
            });
        var workspace = CreateWorkspace(document, document);
        var events = new List<PanelChangeEvent>();
        document.PanelChanged += panelChange => events.Add(panelChange);

        var updated = PanelElementModelCloner.Clone(document.GetPanelElements().Single(), x: 42);
        var command = CanvasMutationCommands.CreateUpdateElementCommand(
            document.DocumentId,
            document,
            "rect-1",
            updated,
            "Inspector update");

        Assert.True(workspace.ExecuteDocumentCanvasCommand(document.DocumentId, command));
        var panelChange = Assert.Single(events);
        Assert.Equal("rect-1", panelChange.ObjectId);
        Assert.True(panelChange.ChangedProperties.HasFlag(PanelChangeProperties.Geometry));
        Assert.False(panelChange.AffectsHierarchy);
    }

    [Fact]
    public void UpdateElementCommand_NameChange_EmitsPanelChangeWithHierarchyRefreshFlag()
    {
        var document = CreatePanelDocument(
            new PanelElementModel
            {
                ObjectId = "rect-1",
                Name = "Rect 1",
                Kind = PanelElementKind.Rectangle,
                X = 10,
                Y = 10,
                Width = 20,
                Height = 20
            });
        var workspace = CreateWorkspace(document, document);
        var events = new List<PanelChangeEvent>();
        document.PanelChanged += panelChange => events.Add(panelChange);

        var updated = PanelElementModelCloner.Clone(document.GetPanelElements().Single(), name: "Rect Renamed");
        var command = CanvasMutationCommands.CreateUpdateElementCommand(
            document.DocumentId,
            document,
            "rect-1",
            updated,
            "Inspector update");

        Assert.True(workspace.ExecuteDocumentCanvasCommand(document.DocumentId, command));
        var panelChange = Assert.Single(events);
        Assert.Equal("rect-1", panelChange.ObjectId);
        Assert.True(panelChange.ChangedProperties.HasFlag(PanelChangeProperties.Name));
        Assert.True(panelChange.AffectsHierarchy);
    }

    [Fact]
    public void ExecuteDocumentCanvasCommand_TargetingInactiveDocument_ReturnsFalseAndDoesNotMutate()
    {
        var firstDocument = CreatePanelDocument(
            new PanelElementModel
            {
                ObjectId = "first-rect",
                Name = "First",
                Kind = PanelElementKind.Rectangle,
                X = 0,
                Y = 0,
                Width = 10,
                Height = 10
            });
        var secondDocument = CreatePanelDocument(
            new PanelElementModel
            {
                ObjectId = "second-rect",
                Name = "Second",
                Kind = PanelElementKind.Rectangle,
                X = 20,
                Y = 20,
                Width = 10,
                Height = 10
            });

        var workspace = CreateWorkspace(selectedDocument: firstDocument, firstDocument, secondDocument);

        var selectionInSecondDocument = new PanelSelectionInfo("second-rect", "rectangle", 20, 20, 10, 10);
        var command = CanvasMutationCommands.CreateRenameElementCommand(
            secondDocument.DocumentId,
            secondDocument,
            selectionInSecondDocument,
            "Should Not Apply");

        var executed = workspace.ExecuteDocumentCanvasCommand(secondDocument.DocumentId, command);

        Assert.False(executed);
        Assert.Equal("Second", secondDocument.GetPanelElements().Single().Name);
        Assert.Empty(secondDocument.CommandService.History.Entries);
    }

    [Fact]
    public void UndoActiveDocument_OnlyUndoesActiveDocumentHistory()
    {
        var firstDocument = CreatePanelDocument(
            new PanelElementModel
            {
                ObjectId = "first-rect",
                Name = "First",
                Kind = PanelElementKind.Rectangle,
                X = 0,
                Y = 0,
                Width = 10,
                Height = 10
            });
        var secondDocument = CreatePanelDocument(
            new PanelElementModel
            {
                ObjectId = "second-rect",
                Name = "Second",
                Kind = PanelElementKind.Rectangle,
                X = 20,
                Y = 20,
                Width = 10,
                Height = 10
            });

        var workspace = CreateWorkspace(selectedDocument: firstDocument, firstDocument, secondDocument);

        var firstRename = CanvasMutationCommands.CreateRenameElementCommand(
            firstDocument.DocumentId,
            firstDocument,
            new PanelSelectionInfo("first-rect", "rectangle", 0, 0, 10, 10),
            "First Renamed");
        var secondRename = CanvasMutationCommands.CreateRenameElementCommand(
            secondDocument.DocumentId,
            secondDocument,
            new PanelSelectionInfo("second-rect", "rectangle", 20, 20, 10, 10),
            "Second Renamed");

        Assert.True(workspace.ExecuteDocumentCanvasCommand(firstDocument.DocumentId, firstRename));
        workspace = CreateWorkspace(selectedDocument: secondDocument, firstDocument, secondDocument);
        Assert.True(workspace.ExecuteDocumentCanvasCommand(secondDocument.DocumentId, secondRename));

        workspace = CreateWorkspace(selectedDocument: firstDocument, firstDocument, secondDocument);
        Assert.True(workspace.UndoActiveDocument());

        Assert.Equal("First", firstDocument.GetPanelElements().Single().Name);
        Assert.Equal("Second Renamed", secondDocument.GetPanelElements().Single().Name);
    }

    [Theory]
    [InlineData(0.1, 9.9, 0.0, 10.0)]
    [InlineData(14.9, 15.1, 10.0, 20.0)]
    [InlineData(25.0, 35.0, 20.0, 40.0)]
    [InlineData(-4.9, -5.1, 0.0, -10.0)]
    public void SnapPointToGrid_RoundsToNearestTenPixels(double x, double y, double expectedX, double expectedY)
    {
        var snapped = PanelToolPlacementController.SnapPointToGrid(new Point(x, y));

        Assert.Equal(expectedX, snapped.X);
        Assert.Equal(expectedY, snapped.Y);
    }

    [Fact]
    public void SnapPointToGrid_PreservesExactGridCoordinates()
    {
        var point = new Point(40.0, -30.0);

        var snapped = PanelToolPlacementController.SnapPointToGrid(point);

        Assert.Equal(40.0, snapped.X);
        Assert.Equal(-30.0, snapped.Y);
    }

    private static DocumentTabViewModel CreatePanelDocument(params PanelElementModel[] elements)
    {
        var document = new DocumentTabViewModel(EditorDocument.CreatePanel2DStub("Panel"));
        document.SetPanelElements(elements);
        return document;
    }

    private static DocumentWorkspaceViewModel CreateWorkspace(
        DocumentTabViewModel selectedDocument,
        params DocumentTabViewModel[] openDocuments)
    {
        var loadedProject = new EditorProject
        {
            Name = "TestProject",
            ProjectFilePath = "C:/Repo/TestProject/TestProject.oasisproj",
            ProjectDirectory = "C:/Repo/TestProject",
            AssetsDirectory = "C:/Repo/TestProject/Assets",
            MachinesDirectory = "C:/Repo/TestProject/Machines",
            GeneratedDirectory = "C:/Repo/TestProject/Generated"
        };
        var documents = new ObservableCollection<DocumentTabViewModel>(openDocuments);
        var currentSelection = selectedDocument;

        return new DocumentWorkspaceViewModel(
            () => loadedProject,
            project => loadedProject = project,
            documents,
            () => currentSelection,
            document => currentSelection = document,
            () => { },
            _ => { },
            (_, _) => { });
    }
}
