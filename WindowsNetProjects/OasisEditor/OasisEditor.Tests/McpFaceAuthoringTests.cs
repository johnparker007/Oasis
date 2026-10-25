using System.Text.Json;
using OasisEditor.Automation;
using SkiaSharp;
using Xunit;

namespace OasisEditor.Tests;

public sealed class McpFaceAuthoringTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "oasis-mcp-face-" + Guid.NewGuid().ToString("N"));
    private readonly McpEditorSession _session;

    public McpFaceAuthoringTests()
    {
        Directory.CreateDirectory(_root);
        _session = new McpEditorSession(_root);
        _session.CreateProject("TestProject");
    }

    [Fact]
    public void BlankFace_SaveAndReopen_PreservesAssetId()
    {
        var face = _session.CreateFace("Blank");
        Assert.Equal("Blank", face.Name);
        Assert.False(string.IsNullOrWhiteSpace(face.AssetId));
        var reopened = OpenNewSession().OpenFaceDocument(face.Path);
        Assert.Equal(face.AssetId, reopened.AssetId);
        Assert.Equal(face.Path, reopened.Path);
    }

    [Fact]
    public void CreateFace_RefusesExistingPackage()
    {
        _session.CreateFace("Existing");
        Assert.Throws<IOException>(() => _session.CreateFace("Existing"));
    }

    [Fact]
    public void SaveFace_RefusesExternalManifestOrAuthoredImageChanges()
    {
        var face = _session.CreateFace("ManifestEdit");
        File.AppendAllText(face.Path, " ");
        Assert.Throws<IOException>(() => _session.SaveFace(face.DocumentId));

        var image = CreatePng("source.png", 100, 80);
        var imageFace = _session.CreateFace("ImageEdit", image);
        Assert.NotNull(imageFace.ArtworkPath);
        var authored = Path.Combine(Path.GetDirectoryName(imageFace.Path)!, ProjectAssetPathService.FaceArtworkFileName);
        File.WriteAllBytes(authored, [1, 2, 3]);
        Assert.Throws<IOException>(() => _session.SaveFace(imageFace.DocumentId));
    }

    [Fact]
    public void AddShape_IsUndoableAndRedoable_AndInvalidQuadsDoNotMutate()
    {
        var panel = _session.CreatePanel("ShapePanel");
        var before = _session.InspectFaceSourceShapes(panel.DocumentId).Count;
        var shape = _session.AddFaceSourceShape(panel.DocumentId, "Glass");
        Assert.Equal(before + 1, _session.InspectFaceSourceShapes(panel.DocumentId).Count);
        _session.Undo(panel.DocumentId);
        Assert.Equal(before, _session.InspectFaceSourceShapes(panel.DocumentId).Count);
        _session.Redo(panel.DocumentId);
        Assert.Equal(before + 1, _session.InspectFaceSourceShapes(panel.DocumentId).Count);

        var count = _session.InspectFaceSourceShapes(panel.DocumentId).Count;
        var invalid = new FaceQuad(new(0, 0), new(100, 100), new(0, 100), new(100, 0));
        Assert.Throws<ArgumentException>(() => _session.AddFaceSourceShape(panel.DocumentId, "Bad", invalid));
        Assert.Equal(count, _session.InspectFaceSourceShapes(panel.DocumentId).Count);
        Assert.NotEmpty(shape.Id);
    }

    [Fact]
    public void GenerateFace_RejectsDirtySourcePanelBeforeCreatingOutput()
    {
        var panel = _session.CreatePanel("DirtyPanel");
        var shape = _session.AddFaceSourceShape(panel.DocumentId, "Glass");
        Assert.Throws<InvalidOperationException>(() => _session.GenerateFaceFromPanel(panel.DocumentId, shape.Id, "MustNotExist"));
        Assert.False(Directory.Exists(Path.Combine(ProjectRoot, "Assets", "Faces", "MustNotExist")));
        Assert.False(Directory.Exists(Path.Combine(ProjectRoot, "Generated", "Faces", "MustNotExist")));
    }

    [Fact]
    public void UndoAfterSavingShape_MarksPanelDirtyAndBlocksGenerationFromStaleSource()
    {
        var panel = _session.CreatePanel("UndoPanel");
        var first = _session.AddFaceSourceShape(panel.DocumentId, "First", RectQuad(0, 0, 50, 40));
        _session.AddFaceSourceShape(panel.DocumentId, "Second", RectQuad(0, 0, 80, 60));
        Assert.False(_session.SavePanel(panel.DocumentId).IsDirty);
        Assert.True(_session.Undo(panel.DocumentId).IsDirty);
        Assert.Single(_session.InspectFaceSourceShapes(panel.DocumentId));
        Assert.Throws<InvalidOperationException>(() => _session.GenerateFaceFromPanel(panel.DocumentId, first.Id, "StaleSource"));
    }

    [Fact]
    public void SavedPanelGeneration_ConvertsLampAndReopenKeepsSourceMetadata()
    {
        var panel = CreatePanelWithBackgroundAndLamp("SourcePanel");
        var shape = _session.AddFaceSourceShape(panel.DocumentId, "Glass", RectQuad(0, 0, 100, 80));
        _session.SavePanel(panel.DocumentId);
        var generated = _session.GenerateFaceFromPanel(panel.DocumentId, shape.Id, "GeneratedFace");
        Assert.Equal(1, generated.LampCount);
        var lamp = Assert.Single(generated.Face.Elements.Where(e => e.Kind == "FaceLampWindowElement"));
        Assert.Equal("lamp-17", lamp.SourceElementId);
        Assert.Equal(shape.Id, generated.Face.SourceShapeId);

        _session.SaveFace(generated.Face.DocumentId);
        var reopened = OpenNewSession().OpenFaceDocument(generated.Face.Path);
        Assert.Equal(shape.Id, reopened.SourceShapeId);
        Assert.Equal(panel.Path, reopened.SourcePanelPath);
    }

    [Fact]
    public void InvalidIdsEscapesAndOversizedRegionsAreRejectedWithoutMutation()
    {
        Assert.Throws<ArgumentException>(() => _session.InspectFace(Guid.NewGuid()));
        var panel = _session.CreatePanel("BoundsPanel");
        Assert.Throws<ArgumentException>(() => _session.AddFaceSourceShape(panel.DocumentId, "TooLarge", RectQuad(0, 0, 5000, 80)));
        Assert.Empty(_session.InspectFaceSourceShapes(panel.DocumentId));
        Assert.Throws<UnauthorizedAccessException>(() => _session.OpenFaceDocument(Path.Combine(_root, "..", "outside", "asset.face")));
        Assert.Throws<ArgumentException>(() => _session.CreateFace("..\\Escape"));
    }

    private PanelSnapshot CreatePanelWithBackgroundAndLamp(string name)
    {
        var panelDirectory = Path.Combine(ProjectRoot, "Assets", "Panel2D", name);
        Directory.CreateDirectory(panelDirectory);
        var panelPath = Path.Combine(panelDirectory, "asset.panel2d");
        var backgroundPath = Path.Combine(panelDirectory, "background.png");
        WritePng(backgroundPath, 100, 80);
        var manifest = Panel2DDocumentStorage.Serialize(name, "MCP test panel",
        [
            new PanelElementFile { ObjectId = "background", Name = "Background", Kind = "background", X = 0, Y = 0, Width = 100, Height = 80, AssetPath = $"Assets/Panel2D/{name}/background.png", IsVisible = true },
            new PanelElementFile { ObjectId = "lamp-17", Name = "Start Lamp", Kind = "lamp", X = 30, Y = 20, Width = 20, Height = 20, DisplayNumber = 17, LampNumber = 17, IsVisible = true }
        ]);
        File.WriteAllText(panelPath, manifest);
        return _session.OpenPanelDocument(panelPath);
    }

    private McpEditorSession OpenNewSession()
    {
        var session = new McpEditorSession(_root);
        session.OpenProject(Path.Combine(ProjectRoot, "TestProject.oasisproj"));
        return session;
    }

    private string CreatePng(string name, int width, int height)
    {
        var path = Path.Combine(_root, name);
        WritePng(path, width, height);
        return path;
    }

    private static void WritePng(string path, int width, int height)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(SKColors.DarkSlateGray);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.Create(path);
        data.SaveTo(stream);
    }

    private string ProjectRoot => Path.Combine(_root, "TestProject");
    private static FaceQuad RectQuad(double x, double y, double width, double height) =>
        new(new(x, y), new(x + width, y), new(x + width, y + height), new(x, y + height));

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); }
        catch (IOException) { }
    }
}
