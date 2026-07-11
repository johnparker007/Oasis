using OasisEditor.Features.LayoutImport;
using Xunit;

namespace OasisEditor.Tests;

public sealed class ImportPanelElementsCommandTests
{
    [Fact]
    public void Execute_WithImportedElements_AddsAllAsSingleUndoableMutation()
    {
        var document = new DocumentTabViewModel(EditorDocument.CreatePanel2DStub("Panel"));
        document.SetPanelElements(
            [
                new PanelElementModel
                {
                    ObjectId = "existing",
                    Name = "Existing",
                    Kind = PanelElementKind.Rectangle,
                    X = 0,
                    Y = 0,
                    Width = 20,
                    Height = 20
                }
            ]);

        var command = new ImportPanelElementsCommand(
            document.DocumentId,
            document,
            [
                new PanelElementModel
                {
                    ObjectId = "import-a",
                    Name = "Lamp 1",
                    Kind = PanelElementKind.Lamp,
                    X = 10,
                    Y = 20,
                    Width = 30,
                    Height = 40,
                    TextBoxFontName = "Lithograph",
                    TextBoxFontStyle = "Bold",
                    TextBoxFontSize = "12"
                },
                new PanelElementModel
                {
                    ObjectId = "import-b",
                    Name = "Reel 1",
                    Kind = PanelElementKind.Reel,
                    X = 50,
                    Y = 60,
                    Width = 70,
                    Height = 80
                }
            ]);

        document.CommandService.Execute(command);

        Assert.Equal(3, document.GetPanelElements().Count);
        Assert.Single(document.CommandService.History.Entries);

        Assert.True(document.CommandService.TryUndo());
        Assert.Single(document.GetPanelElements());

        Assert.True(document.CommandService.TryRedo());
        Assert.Equal(3, document.GetPanelElements().Count);
        var importedLamp = Assert.Single(document.GetPanelElements(), element => element.ObjectId == "import-a");
        Assert.Equal("Lithograph", importedLamp.TextBoxFontName);
        Assert.Equal("Bold", importedLamp.TextBoxFontStyle);
        Assert.Equal("12", importedLamp.TextBoxFontSize);
        Assert.Contains(document.GetPanelElements(), element => element.ObjectId == "import-b");
    }


    [Fact]
    public void Execute_WithImportedReelsAndAlphaDisplaysForImageBackedBackground_MovesThemBeforeBackgroundSoBackgroundDrawsInFront()
    {
        var document = new DocumentTabViewModel(EditorDocument.CreatePanel2DStub("Panel"));
        document.SetPanelElements(
            [
                new PanelElementModel
                {
                    ObjectId = "existing",
                    Name = "Existing",
                    Kind = PanelElementKind.Lamp,
                    Width = 1,
                    Height = 1
                }
            ]);

        var command = new ImportPanelElementsCommand(
            document.DocumentId,
            document,
            [
                new PanelElementModel
                {
                    ObjectId = "background",
                    Name = "Background",
                    Kind = PanelElementKind.Background,
                    Width = 4,
                    Height = 4,
                    AssetPath = "Assets/FmlImport/Layout/Background/bg.png"
                },
                new PanelElementModel
                {
                    ObjectId = "reel",
                    Name = "Reel",
                    Kind = PanelElementKind.Reel,
                    Width = 1,
                    Height = 1
                },
                new PanelElementModel
                {
                    ObjectId = "alpha",
                    Name = "Alpha",
                    Kind = PanelElementKind.Alpha,
                    Width = 1,
                    Height = 1
                }
            ]);

        document.CommandService.Execute(command);

        var elements = document.GetPanelElements();
        Assert.Equal("reel", elements[0].ObjectId);
        Assert.Equal("alpha", elements[1].ObjectId);
        Assert.Equal("existing", elements[2].ObjectId);
        Assert.Equal("background", elements[3].ObjectId);
    }


    [Fact]
    public void Execute_WithImportedSolidColourBackground_PreservesImporterOrderingSoReelsStayInFront()
    {
        var document = new DocumentTabViewModel(EditorDocument.CreatePanel2DStub("Panel"));

        var command = new ImportPanelElementsCommand(
            document.DocumentId,
            document,
            [
                new PanelElementModel
                {
                    ObjectId = "background",
                    Name = "Background",
                    Kind = PanelElementKind.Background,
                    Width = 4,
                    Height = 4,
                    OnColorHex = "#FFF0F0F0"
                },
                new PanelElementModel
                {
                    ObjectId = "reel",
                    Name = "Reel",
                    Kind = PanelElementKind.Reel,
                    Width = 1,
                    Height = 1
                },
                new PanelElementModel
                {
                    ObjectId = "seven",
                    Name = "Seven Segment",
                    Kind = PanelElementKind.SevenSegment,
                    Width = 1,
                    Height = 1
                },
                new PanelElementModel
                {
                    ObjectId = "alpha",
                    Name = "Alpha",
                    Kind = PanelElementKind.Alpha,
                    Width = 1,
                    Height = 1
                }
            ]);

        document.CommandService.Execute(command);

        var elements = document.GetPanelElements();
        Assert.Equal("background", elements[0].ObjectId);
        Assert.Equal("reel", elements[1].ObjectId);
        Assert.Equal("seven", elements[2].ObjectId);
        Assert.Equal("alpha", elements[3].ObjectId);
    }


    [Fact]
    public void Execute_WithSolidColourBackgroundAndBitmapOverlay_DoesNotTreatBitmapAsImageBackedBackground()
    {
        var document = new DocumentTabViewModel(EditorDocument.CreatePanel2DStub("Panel"));

        var command = new ImportPanelElementsCommand(
            document.DocumentId,
            document,
            [
                new PanelElementModel
                {
                    ObjectId = "background",
                    Name = "Background",
                    Kind = PanelElementKind.Background,
                    Width = 4,
                    Height = 4,
                    OnColorHex = "#FFF0F0F0",
                    ImportSource = new PanelElementImportSourceModel { Format = "FML", Reference = "Background:0" }
                },
                new PanelElementModel
                {
                    ObjectId = "reel",
                    Name = "Reel",
                    Kind = PanelElementKind.Reel,
                    Width = 1,
                    Height = 1,
                    ImportSource = new PanelElementImportSourceModel { Format = "FML", Reference = "Reel:1" }
                },
                new PanelElementModel
                {
                    ObjectId = "seven",
                    Name = "Seven Segment",
                    Kind = PanelElementKind.SevenSegment,
                    Width = 1,
                    Height = 1,
                    ImportSource = new PanelElementImportSourceModel { Format = "FML", Reference = "SevenSeg:2" }
                },
                new PanelElementModel
                {
                    ObjectId = "bitmap-overlay",
                    Name = "Bitmap Overlay",
                    Kind = PanelElementKind.Background,
                    Width = 1,
                    Height = 1,
                    AssetPath = "Assets/FmlImport/Layout/Background/overlay.png",
                    ImportSource = new PanelElementImportSourceModel { Format = "FML", Reference = "Bitmap:3" }
                }
            ]);

        document.CommandService.Execute(command);

        var elements = document.GetPanelElements();
        Assert.Equal("background", elements[0].ObjectId);
        Assert.Equal("reel", elements[1].ObjectId);
        Assert.Equal("seven", elements[2].ObjectId);
        Assert.Equal("bitmap-overlay", elements[3].ObjectId);
    }

    [Fact]
    public void Execute_WithNoElements_DoesNotMutateOrRecordHistory()
    {
        var document = new DocumentTabViewModel(EditorDocument.CreatePanel2DStub("Panel"));
        var command = new ImportPanelElementsCommand(document.DocumentId, document, []);

        document.CommandService.Execute(command);

        Assert.Empty(document.GetPanelElements());
        Assert.Empty(document.CommandService.History.Entries);
    }

    [Fact]
    public void Execute_WithWrongDocumentId_ThrowsAndDoesNotMutateTargetDocument()
    {
        var document = new DocumentTabViewModel(EditorDocument.CreatePanel2DStub("Panel"));
        var command = new ImportPanelElementsCommand(
            Guid.NewGuid(),
            document,
            [
                new PanelElementModel
                {
                    ObjectId = "import-a",
                    Name = "Background",
                    Kind = PanelElementKind.Background,
                    X = 0,
                    Y = 0,
                    Width = 100,
                    Height = 100
                }
            ]);

        Action execute = () => document.CommandService.Execute(command);
        Assert.Throws<InvalidOperationException>(execute);
        Assert.Empty(document.GetPanelElements());
        Assert.Empty(document.CommandService.History.Entries);
    }

    [Fact]
    public void Execute_WithConflictingSourceObjectId_RewritesIdAndKeepsItStableAcrossUndoRedo()
    {
        var document = new DocumentTabViewModel(EditorDocument.CreatePanel2DStub("Panel"));
        document.SetPanelElements(
            [
                new PanelElementModel
                {
                    ObjectId = "collision",
                    Name = "Existing",
                    Kind = PanelElementKind.Rectangle,
                    X = 1,
                    Y = 2,
                    Width = 3,
                    Height = 4
                }
            ]);

        var command = new ImportPanelElementsCommand(
            document.DocumentId,
            document,
            [
                new PanelElementModel
                {
                    ObjectId = "collision",
                    Name = "Imported",
                    Kind = PanelElementKind.Alpha,
                    X = 5,
                    Y = 6,
                    Width = 7,
                    Height = 8
                }
            ]);

        document.CommandService.Execute(command);
        var importedId = document.GetPanelElements().Single(element => element.Name == "Imported").ObjectId;
        Assert.NotEqual("collision", importedId);

        Assert.True(document.CommandService.TryUndo());
        Assert.DoesNotContain(document.GetPanelElements(), element => element.ObjectId == importedId);

        Assert.True(document.CommandService.TryRedo());
        var redoneImportedId = document.GetPanelElements().Single(element => element.Name == "Imported").ObjectId;
        Assert.Equal(importedId, redoneImportedId);
    }

    [Fact]
    public void Execute_IntoEmptyPanelDocument_PreservesNativeKindsAcrossHierarchyUndoRedoAndRoundTrip()
    {
        var document = new DocumentTabViewModel(EditorDocument.CreatePanel2DStub("Imported Panel"));
        var importedElements = new[]
        {
            new PanelElementModel
            {
                ObjectId = "background-1",
                Name = "Background",
                Kind = PanelElementKind.Background,
                X = 0,
                Y = 0,
                Width = 640,
                Height = 360,
                AssetPath = "Assets/FmlImport/Layout/Background/bg.png"
            },
            new PanelElementModel
            {
                ObjectId = "lamp-1",
                Name = "Lamp 5",
                Kind = PanelElementKind.Lamp,
                X = 24,
                Y = 48,
                Width = 32,
                Height = 32,
                DisplayNumber = 5,
                AssetPath = "Assets/FmlImport/Layout/Lamps/lamp.png"
            },
            new PanelElementModel
            {
                ObjectId = "reel-1",
                Name = "Reel 3",
                Kind = PanelElementKind.Reel,
                X = 120,
                Y = 80,
                Width = 96,
                Height = 128,
                DisplayNumber = 3,
                AssetPath = "Assets/FmlImport/Layout/Reels/reel.png"
            },
            new PanelElementModel
            {
                ObjectId = "seven-1",
                Name = "7 Segment 2",
                Kind = PanelElementKind.SevenSegment,
                X = 260,
                Y = 90,
                Width = 80,
                Height = 24,
                DisplayNumber = 2
            },
            new PanelElementModel
            {
                ObjectId = "alpha-1",
                Name = "Alpha",
                Kind = PanelElementKind.Alpha,
                X = 360,
                Y = 96,
                Width = 96,
                Height = 28,
                IsReversed = true
            }
        };

        var command = new ImportPanelElementsCommand(document.DocumentId, document, importedElements);
        document.CommandService.Execute(command);

        Assert.Equal(5, document.GetPanelElements().Count);
        Assert.Single(document.CommandService.History.Entries);

        var hierarchy = new Panel2DHierarchyProvider();
        var groups = hierarchy.Build(document);
        Assert.Equal("Backgrounds (1)", groups.Single(group => group.NodeKey == "group:background").DisplayName);
        Assert.Equal("Lamps (1)", groups.Single(group => group.NodeKey == "group:lamp").DisplayName);
        Assert.Equal("Reels (1)", groups.Single(group => group.NodeKey == "group:reel").DisplayName);
        Assert.Equal("Seven Segments (1)", groups.Single(group => group.NodeKey == "group:sevenSegment").DisplayName);
        Assert.Equal("Alphas (1)", groups.Single(group => group.NodeKey == "group:alpha").DisplayName);

        Assert.True(document.CommandService.TryUndo());
        Assert.Empty(document.GetPanelElements());

        Assert.True(document.CommandService.TryRedo());
        Assert.Equal(5, document.GetPanelElements().Count);
        Assert.Contains(document.GetPanelElements(), element => element.Kind == PanelElementKind.Background && element.AssetPath is not null);
        Assert.Contains(document.GetPanelElements(), element => element.Kind == PanelElementKind.Lamp && element.DisplayNumber == 5);
        Assert.Contains(document.GetPanelElements(), element => element.Kind == PanelElementKind.Reel && element.DisplayNumber == 3);
        Assert.Contains(document.GetPanelElements(), element => element.Kind == PanelElementKind.SevenSegment && element.DisplayNumber == 2);
        Assert.Contains(document.GetPanelElements(), element => element.Kind == PanelElementKind.Alpha && element.IsReversed == true);

        var savedContent = DocumentWorkspaceViewModel.BuildDocumentContent(document);
        Assert.True(Panel2DDocumentStorage.TryReadValidated(savedContent, out var parsed, out var error), error);
        Assert.Equal(5, parsed.Elements.Count());
        Assert.Contains(parsed.Elements, element => element.Kind == "background" && element.AssetPath == "Assets/FmlImport/Layout/Background/bg.png");
        Assert.Contains(parsed.Elements, element => element.Kind == "lamp" && element.DisplayNumber == 5);
        Assert.Contains(parsed.Elements, element => element.Kind == "reel" && element.DisplayNumber == 3);
        Assert.Contains(parsed.Elements, element => element.Kind == "sevenSegment" && element.DisplayNumber == 2);
        Assert.Contains(parsed.Elements, element => element.Kind == "alpha" && element.IsReversed == true);
    }
}
