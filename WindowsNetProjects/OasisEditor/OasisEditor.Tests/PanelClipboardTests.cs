using System.Text.Json;
using OasisEditor;
using Xunit;

namespace OasisEditor.Avalonia.Tests;

public sealed class PanelClipboardTests
{
    [Fact]
    public void Clipboard_PreservesMetadataSupportsOtherPanelAndRecordsOneHistoryEntry()
    {
        using var project = new Fixture();
        var session = project.Open();
        var ids = session.Elements.Select(element => element.Id).Take(2).ToArray();
        session.SelectElements(ids);

        Assert.True(session.CopySelectedToClipboard());
        Assert.False(session.IsDirty);
        Assert.False(session.CanUndo);
        session.OpenPanel(project.SecondPanelPath);
        var pasted = session.PasteClipboard(5, 7);
        Assert.Equal(2, pasted.Count);
        Assert.Equal(pasted, session.SelectedIds);
        Assert.Equal(new[] { "target", "First", "middle" }, session.Elements.Select(element => element.Name));
        Assert.Equal(15, session.Elements[1].X);
        Assert.Equal(27, session.Elements[1].Y);
        Assert.Equal("Assets/Panel2D/Source/Images/shared.png", session.Elements[1].AssetPath);

        session.Save();
        var saved = JsonSerializer.Deserialize<Panel2DDocumentFile>(File.ReadAllText(project.SecondPanelPath))!;
        var clone = saved.Elements.Single(element => element.ObjectId == pasted[0]);
        Assert.Equal("fixture", clone.ImportSource?.Format);
        Assert.Equal("source/path", clone.ImportSource?.Reference);
        Assert.Equal(8, clone.Native?.Stops);
        Assert.Equal(7, clone.ReelLamps?.Single().LampNumber);

        session.Undo();
        Assert.Single(session.Elements);
        session.Redo();
        Assert.Equal(3, session.Elements.Count);
    }

    [Fact]
    public void CutAndArrange_AreUndoableAndPreserveRelativeSelectionOrder()
    {
        using var project = new Fixture();
        var session = project.Open();
        var selected = new[] { session.Elements[0].Id, session.Elements[2].Id };
        session.SelectElements(selected);
        Assert.True(session.CutSelectedToClipboard());
        Assert.Equal(new[] { "middle" }, session.Elements.Select(element => element.Name));
        session.Undo();
        Assert.Equal(new[] { "First", "middle", "Second" }, session.Elements.Select(element => element.Name));
        session.SelectElements(selected);

        Assert.True(session.SendSelectionToBack());
        Assert.Equal(new[] { "First", "Second", "middle" }, session.Elements.Select(element => element.Name));
        session.Undo();
        Assert.Equal(new[] { "First", "middle", "Second" }, session.Elements.Select(element => element.Name));
        session.SelectElements(selected);
        Assert.True(session.BringSelectionToFront());
        Assert.Equal(new[] { "middle", "First", "Second" }, session.Elements.Select(element => element.Name));
    }

    private sealed class Fixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "OasisPanelClipboardTests", Guid.NewGuid().ToString("N"));
        public string ProjectPath => Path.Combine(Root, "Clipboard.oasisproj");
        public string FirstPanelPath => Path.Combine(Root, "Assets", "Panel2D", "Source", "asset.panel2d");
        public string SecondPanelPath => Path.Combine(Root, "Assets", "Panel2D", "Target", "asset.panel2d");

        public Fixture()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FirstPanelPath)!);
            Directory.CreateDirectory(Path.GetDirectoryName(SecondPanelPath)!);
            Directory.CreateDirectory(Path.Combine(Root, "Generated"));
            File.WriteAllText(ProjectPath, """{"version":9,"name":"Clipboard","layout":{"assets":"Assets","generated":"Generated"}}""");
        var sourceImage = Path.Combine(Root, "Assets", "Panel2D", "Source", "Images", "shared.png");
        Directory.CreateDirectory(Path.GetDirectoryName(sourceImage)!);
        File.WriteAllBytes(sourceImage, [0]);
        File.WriteAllText(FirstPanelPath, JsonSerializer.Serialize(new Panel2DDocumentFile
            {
                SchemaVersion = Panel2DDocumentStorage.CurrentSchemaVersion,
                Title = "Source",
                Elements =
                [
                    new() { ObjectId = "first", Name = "First", Kind = "reel", X = 10, Y = 20, Width = 30, Height = 40,
                        AssetPath = "Assets/Panel2D/Source/Images/shared.png",
                        ImportSource = new PanelElementImportSourceFile { Format = "fixture", Reference = "source/path" },
                        Native = new PanelElementNativeFile { Stops = 8, Text = "Native" },
                        ReelLamps = [new PanelElementReelLampFile { Position = "top", LampNumber = 7 }] },
                    new() { ObjectId = "middle", Name = "middle", Kind = "rectangle", X = 30, Y = 20, Width = 20, Height = 20 },
                    new() { ObjectId = "second", Name = "Second", Kind = "rectangle", X = 60, Y = 20, Width = 20, Height = 20 }
                ]
            }));
            File.WriteAllText(SecondPanelPath, JsonSerializer.Serialize(new Panel2DDocumentFile
            {
                SchemaVersion = Panel2DDocumentStorage.CurrentSchemaVersion, Title = "Target",
                Elements = [new() { ObjectId = "target", Name = "target", Kind = "rectangle", X = 0, Y = 0, Width = 10, Height = 10 }]
            }));
        }

        public AuthoringSession Open()
        {
            var session = new AuthoringSession(Root);
            session.OpenProject(ProjectPath);
            session.OpenPanel(FirstPanelPath);
            return session;
        }

        public void Dispose() { if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true); }
    }
}
