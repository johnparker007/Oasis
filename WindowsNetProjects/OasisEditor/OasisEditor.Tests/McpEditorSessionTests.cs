using OasisEditor.Automation;
using OasisEditor.Features.LayoutImport;
using Xunit;

namespace OasisEditor.Tests;

public sealed class McpEditorSessionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "OasisMcpTests", Guid.NewGuid().ToString("N"));

    public McpEditorSessionTests() => Directory.CreateDirectory(_root);

    [Fact]
    public void CreateSaveAndReopen_RoundTripsPanelThroughSecondSession()
    {
        var project = NewSession();
        var session = project;
        var created = session.CreatePanel("Main");
        Assert.False(created.IsDirty);

        var second = new McpEditorSession(_root, new FakeImporter(true));
        second.OpenProject(Path.Combine(_root, "Demo", "Demo.oasisproj"));
        var reopened = second.OpenPanelDocument(created.Path);
        Assert.Equal(created.ElementCount, reopened.ElementCount);
        Assert.Equal(created.Name, reopened.Name);
    }

    [Fact]
    public void Import_TracksUndoRedoAndSaveClearsDirtyState()
    {
        var session = NewSession();
        var panel = session.CreatePanel("Main");
        File.WriteAllText(Path.Combine(_root, "layout.fml"), "fake");
        var imported = session.ImportFml(panel.DocumentId, "layout.fml");
        Assert.True(imported.Succeeded);
        Assert.Equal(1, imported.Panel.ElementCount);
        Assert.True(imported.Panel.IsDirty);
        Assert.True(session.Undo(panel.DocumentId).CanRedo);
        Assert.Equal(0, session.InspectPanel(panel.DocumentId).ElementCount);
        Assert.Equal(1, session.Redo(panel.DocumentId).ElementCount);
        Assert.False(session.SavePanel(panel.DocumentId).IsDirty);
    }

    [Fact]
    public void FailedImport_ReportsDiagnosticsWithoutChangingDocument()
    {
        var session = NewSession(false);
        var panel = session.CreatePanel("Main");
        File.WriteAllText(Path.Combine(_root, "bad.fml"), "invalid");
        var result = session.ImportFml(panel.DocumentId, "bad.fml");
        Assert.False(result.Succeeded);
        Assert.Contains("import failed", result.Errors);
        Assert.Equal(panel.ElementCount, result.Panel.ElementCount);
        Assert.Equal(panel.IsDirty, result.Panel.IsDirty);
        Assert.Equal(panel.CanUndo, result.Panel.CanUndo);
    }

    [Fact]
    public void RejectsTraversalAndInvalidOrCrossSessionIds()
    {
        var session = NewSession();
        var panel = session.CreatePanel("Main");
        Assert.Throws<UnauthorizedAccessException>(() => session.ImportFml(panel.DocumentId, "../outside.fml"));
        Assert.Throws<ArgumentException>(() => session.InspectPanel(Guid.NewGuid()));
        var other = new McpEditorSession(_root, new FakeImporter(true));
        other.OpenProject(Path.Combine(_root, "Demo", "Demo.oasisproj"));
        Assert.Throws<ArgumentException>(() => other.InspectPanel(panel.DocumentId));
        Assert.Throws<ArgumentOutOfRangeException>(() => session.InspectPanel(panel.DocumentId, -1));
    }

    [Fact]
    public void ExistingPackageIsNotOverwritten()
    {
        var session = NewSession();
        var panel = session.CreatePanel("Main");
        var content = File.ReadAllText(panel.Path);
        Assert.Throws<IOException>(() => session.CreatePanel("Main"));
        Assert.Equal(content, File.ReadAllText(panel.Path));
    }

    [Fact]
    public void SaveRefusesExternalModification()
    {
        var session = NewSession();
        var panel = session.CreatePanel("Main");
        File.AppendAllText(panel.Path, " ");
        Assert.Throws<IOException>(() => session.SavePanel(panel.DocumentId));
    }

    private McpEditorSession NewSession(bool succeeds = true)
    {
        var session = new McpEditorSession(_root, new FakeImporter(succeeds));
        session.CreateProject("Demo");
        return session;
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private sealed class FakeImporter(bool succeeds) : IFmlAutomationImportService
    {
        public LayoutImportResult ImportFromFml(string fmlPath, string projectRootPath, string projectAssetsPath, bool copyAssets = true) => new()
        {
            ImportedElements = succeeds ? [new PanelElementModel { ObjectId = "rect-1", Name = "Rectangle", Kind = PanelElementKind.Rectangle, Width = 20, Height = 10 }] : [],
            CopiedAssetRelativePaths = [], InputDefinitions = [], UnsupportedComponentTypes = [], Warnings = [], Errors = succeeds ? [] : ["import failed"]
        };
    }
}
