using Xunit;
using OasisEditor;

namespace OasisEditor.Avalonia.Tests;

public sealed class PanelWorkflowTests
{
    [Fact]
    public void DuplicateSelected_PreservesFullElementMetadataAndCreatesOneUndoableEdit()
    {
        using var project = new Fixture(rich: true);
        var session = project.Open();
        var source = session.Elements[0];
        session.SelectElements([source.Id]);

        var copyId = Assert.Single(session.DuplicateSelected());
        Assert.Equal(2, session.Elements.Count);
        Assert.NotEqual(source.Id, copyId);
        Assert.Equal(source.X + 16, session.Elements[1].X);
        Assert.Equal(new[] { copyId }, session.SelectedIds);
        session.Save();
        var saved = Read(project.PanelPath);
        var original = saved.Elements.Single(element => element.ObjectId == source.Id);
        var copy = saved.Elements.Single(element => element.ObjectId == copyId);
        Assert.Equal(original.Kind, copy.Kind);
        Assert.Equal(original.AssetPath, copy.AssetPath);
        Assert.Equal(original.ImportSource?.Reference, copy.ImportSource?.Reference);
        Assert.Equal(original.Native?.Stops, copy.Native?.Stops);
        Assert.Equal(original.ReelLamps?.Single().LampNumber, copy.ReelLamps?.Single().LampNumber);

        session.Undo();
        Assert.Single(session.Elements);
        session.Redo();
        Assert.Equal(2, session.Elements.Count);
    }

    [Fact]
    public void MultiSelectionCommands_AreAtomicAndUndoRedoAsSingleEntries()
    {
        using var project = new Fixture(twoElements: true);
        var session = project.Open();
        var ids = session.Elements.Select(element => element.Id).ToArray();
        session.SelectElements(ids);
        Assert.False(session.IsDirty);
        Assert.False(session.CanUndo);

        Assert.Throws<KeyNotFoundException>(() => session.SelectElements([ids[0], "missing"]));
        Assert.Equal(ids, session.SelectedIds);
        Assert.Throws<InvalidOperationException>(() => session.MoveSelected(3, 4));
        Assert.Equal(10, session.Elements[0].X);
        Assert.Equal(40, session.Elements[1].X);
        Assert.False(session.CanUndo);

        session.SelectElements([ids[0]]);
        Assert.True(session.MoveSelected(3, 4));
        Assert.Equal(13, session.Elements[0].X);
        Assert.True(session.CanUndo);
        session.Undo();
        Assert.Equal(10, session.Elements[0].X);
        session.Redo();
        Assert.Equal(13, session.Elements[0].X);

        session.SelectElements(ids);
        Assert.True(session.DeleteSelected());
        Assert.Empty(session.Elements);
        Assert.Empty(session.SelectedIds);
        session.Undo();
        Assert.Equal(2, session.Elements.Count);
    }

    [Fact]
    public void SourceShapes_AddUpdateDelete_SaveAndReloadRetainFullGeometry()
    {
        using var project = new Fixture();
        var session = project.Open();
        var id = session.AddSourceShape("New shape");
        var shape = Assert.Single(session.SourceShapes);
        Assert.Equal(id, shape.Id);
        Assert.Equal("perspectiveRectangle", shape.Type);
        Assert.Equal(new[] { new PointSnapshot(10, 20), new PointSnapshot(74, 20), new PointSnapshot(74, 100), new PointSnapshot(10, 100) }, shape.Points);

        var points = new[] { new PointSnapshot(1, 2), new PointSnapshot(30, 4), new PointSnapshot(25, 40), new PointSnapshot(-2, 22) };
        Assert.True(session.UpdateSourceShape(id, "Updated", points));
        Assert.Throws<ArgumentException>(() => session.UpdateSourceShape(id, "Invalid", [new(0, 0), new(1, 1), new(2, 2), new(0, 2)]));
        Assert.Equal("Updated", Assert.Single(session.SourceShapes).Name);
        session.Save();
        var reloaded = project.Open();
        var persisted = Assert.Single(reloaded.SourceShapes);
        Assert.Equal(points, persisted.Points);
        Assert.Equal("Updated", persisted.Name);

        Assert.True(reloaded.DeleteSourceShape(id));
        Assert.Empty(reloaded.SourceShapes);
        reloaded.Undo();
        Assert.Equal(points, Assert.Single(reloaded.SourceShapes).Points);
    }

    private static Panel2DDocumentFile Read(string path)
    {
        Assert.True(Panel2DDocumentStorage.TryRead(File.ReadAllText(path), out var document));
        return document;
    }

    private sealed class Fixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "OasisPanelWorkflowTests", Guid.NewGuid().ToString("N"));
        public string ProjectPath => Path.Combine(Root, "Workflow.oasisproj");
        public string PanelPath => Path.Combine(Root, "Assets", "Panel2D", "Panel", "asset.panel2d");

        public Fixture(bool rich = false, bool twoElements = false)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PanelPath)!);
            Directory.CreateDirectory(Path.Combine(Root, "Generated"));
            File.WriteAllText(ProjectPath, """{"version":9,"name":"Workflow","layout":{"assets":"Assets","generated":"Generated"}}""");
            var first = new PanelElementFile
            {
                ObjectId = "element-a", Name = "Element A", Kind = rich ? "reel" : "rectangle", X = 10, Y = 20,
                Width = 64, Height = 80, AssetPath = rich ? "reel.png" : null,
                ImportSource = rich ? new PanelElementImportSourceFile { Format = "fixture", Reference = "source/path" } : null,
                Native = rich ? new PanelElementNativeFile { Stops = 8, Text = "native text" } : null,
                ReelLamps = rich ? [new PanelElementReelLampFile { Position = "top", LampNumber = 7 }] : null
            };
            PanelElementFile[] elements = twoElements
                ? [first, new PanelElementFile { ObjectId = "element-b", Name = "Element B", Kind = "rectangle", X = 40, Y = 20, Width = 10, Height = 10, LockTransform = true }]
                : [first];
            File.WriteAllText(PanelPath, System.Text.Json.JsonSerializer.Serialize(new Panel2DDocumentFile
            { SchemaVersion = Panel2DDocumentStorage.CurrentSchemaVersion, Title = "Panel", Summary = "Summary", Elements = elements }));
        }

        public AuthoringSession Open()
        {
            var session = new AuthoringSession(Root);
            session.OpenProject(ProjectPath);
            session.OpenPanel(PanelPath);
            return session;
        }

        public void Dispose()
        {
            if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
        }
    }
}
