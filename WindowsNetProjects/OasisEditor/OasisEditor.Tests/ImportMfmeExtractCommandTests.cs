using OasisEditor.Features.MfmeImport;
using Xunit;

namespace OasisEditor.Tests;

public sealed class ImportMfmeExtractCommandTests
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

        var command = new ImportMfmeExtractCommand(
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
                    Height = 40
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
        Assert.Contains(document.GetPanelElements(), element => element.ObjectId == "import-a");
        Assert.Contains(document.GetPanelElements(), element => element.ObjectId == "import-b");
    }

    [Fact]
    public void Execute_WithNoElements_DoesNotMutateOrRecordHistory()
    {
        var document = new DocumentTabViewModel(EditorDocument.CreatePanel2DStub("Panel"));
        var command = new ImportMfmeExtractCommand(document.DocumentId, document, []);

        document.CommandService.Execute(command);

        Assert.Empty(document.GetPanelElements());
        Assert.Empty(document.CommandService.History.Entries);
    }

    [Fact]
    public void Execute_WithWrongDocumentId_ThrowsAndDoesNotMutateTargetDocument()
    {
        var document = new DocumentTabViewModel(EditorDocument.CreatePanel2DStub("Panel"));
        var command = new ImportMfmeExtractCommand(
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

        var command = new ImportMfmeExtractCommand(
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
}
