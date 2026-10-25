using OasisEditor.Automation;
using OasisEditor.Features.LayoutImport;
using Xunit;

namespace OasisEditor.Tests;

public sealed class McpPanelEditingTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "OasisMcpPanelEditingTests", Guid.NewGuid().ToString("N"));

    public McpPanelEditingTests() => Directory.CreateDirectory(_root);

    [Fact]
    public void UpdateElement_AppliesGeometryNameAndVisibilityAsOneUndoableEdit()
    {
        var (session, panel) = CreatePanel();
        var before = session.InspectElement(panel.DocumentId, "rect-1");

        var edited = session.UpdateElement(panel.DocumentId, "rect-1", new PanelElementEdit(
            Name: "Prize panel", X: 12, Y: 14, Width: 32, Height: 18, IsVisible: false));

        Assert.Equal("Prize panel", edited.Element.Name);
        Assert.Equal((12d, 14d, 32d, 18d), (edited.Element.X, edited.Element.Y, edited.Element.Width, edited.Element.Height));
        Assert.False(edited.Element.IsVisible);
        Assert.True(edited.Panel.IsDirty);
        Assert.True(edited.Panel.CanUndo);

        var undone = session.Undo(panel.DocumentId);
        var restored = session.InspectElement(panel.DocumentId, "rect-1");
        Assert.Equal(before.Name, restored.Name);
        Assert.Equal((before.X, before.Y, before.Width, before.Height), (restored.X, restored.Y, restored.Width, restored.Height));
        Assert.Equal(before.IsVisible, restored.IsVisible);
        Assert.True(undone.CanRedo);

        var redone = session.Redo(panel.DocumentId);
        var redoneElement = session.InspectElement(panel.DocumentId, "rect-1");
        Assert.Equal("Prize panel", redoneElement.Name);
        Assert.Equal((12d, 14d, 32d, 18d), (redoneElement.X, redoneElement.Y, redoneElement.Width, redoneElement.Height));
        Assert.False(redoneElement.IsVisible);
        Assert.True(redone.IsDirty);
    }

    [Fact]
    public void NoOpUpdateAfterUndo_PreservesRedoHistoryAndDirtyState()
    {
        var (session, panel) = CreatePanel();
        session.UpdateElement(panel.DocumentId, "rect-1", new PanelElementEdit(Name: "Changed"));
        var afterUndo = session.Undo(panel.DocumentId);
        Assert.True(afterUndo.CanRedo);
        var original = session.InspectElement(panel.DocumentId, "rect-1");

        var noOp = session.UpdateElement(panel.DocumentId, "rect-1", new PanelElementEdit(Name: original.Name));

        Assert.True(noOp.Panel.CanRedo);
        Assert.Equal(afterUndo.IsDirty, noOp.Panel.IsDirty);
        Assert.Equal("Rectangle", noOp.Element.Name);
    }

    [Fact]
    public void LockedTransformRequiresSeparateUnlockBeforeGeometryEdit()
    {
        var (session, panel) = CreatePanel();
        session.UpdateElement(panel.DocumentId, "rect-1", new PanelElementEdit(IsTransformLocked: true));
        var locked = session.InspectPanel(panel.DocumentId);
        Assert.True(locked.CanUndo);

        Assert.Throws<InvalidOperationException>(() => session.UpdateElement(panel.DocumentId, "rect-1",
            new PanelElementEdit(X: 5, IsTransformLocked: false)));
        var unchanged = session.InspectElement(panel.DocumentId, "rect-1");
        Assert.True(unchanged.IsTransformLocked);
        Assert.Equal(0d, unchanged.X);
        Assert.Equal(locked.CanUndo, session.InspectPanel(panel.DocumentId).CanUndo);

        session.UpdateElement(panel.DocumentId, "rect-1", new PanelElementEdit(IsTransformLocked: false));
        var unlocked = session.UpdateElement(panel.DocumentId, "rect-1", new PanelElementEdit(X: 5));
        Assert.False(unlocked.Element.IsTransformLocked);
        Assert.Equal(5d, unlocked.Element.X);
    }

    [Fact]
    public void InvalidUpdateFields_RejectWholeEditWithoutPartialRename()
    {
        var (session, panel) = CreatePanel();
        var baseline = session.InspectPanel(panel.DocumentId);

        Assert.Throws<ArgumentException>(() => session.UpdateElement(panel.DocumentId, "rect-1",
            new PanelElementEdit(Name: "Renamed", X: double.NaN)));
        Assert.Throws<ArgumentException>(() => session.UpdateElement(panel.DocumentId, "rect-1",
            new PanelElementEdit(Name: "Renamed", Y: double.PositiveInfinity)));
        Assert.Throws<ArgumentException>(() => session.UpdateElement(panel.DocumentId, "rect-1",
            new PanelElementEdit(Name: "Renamed", Width: 0)));
        Assert.Throws<ArgumentException>(() => session.UpdateElement(panel.DocumentId, "rect-1",
            new PanelElementEdit(Name: "Renamed", Height: double.NegativeInfinity)));
        Assert.Throws<ArgumentException>(() => session.UpdateElement(panel.DocumentId, "rect-1",
            new PanelElementEdit(Name: "  ")));

        var unchanged = session.InspectElement(panel.DocumentId, "rect-1");
        Assert.Equal("Rectangle", unchanged.Name);
        Assert.Equal(baseline.IsDirty, session.InspectPanel(panel.DocumentId).IsDirty);
        Assert.Equal(baseline.CanUndo, session.InspectPanel(panel.DocumentId).CanUndo);
    }

    [Fact]
    public void SelectElements_ReplacesAndClearsSelectionAndValidatesBeforeMutation()
    {
        var (session, panel) = CreatePanel(twoElements: true);
        var initial = session.InspectPanel(panel.DocumentId);
        var selected = session.SelectElements(panel.DocumentId, ["rect-1"]);
        Assert.Equal(new[] { "rect-1" }, selected.SelectedElementIds);
        Assert.True(session.InspectElement(panel.DocumentId, "rect-1").IsSelected);
        Assert.False(session.InspectElement(panel.DocumentId, "rect-2").IsSelected);

        var replaced = session.SelectElements(panel.DocumentId, ["rect-2"]);
        Assert.Equal(new[] { "rect-2" }, replaced.SelectedElementIds);
        Assert.False(session.InspectElement(panel.DocumentId, "rect-1").IsSelected);
        Assert.True(session.InspectElement(panel.DocumentId, "rect-2").IsSelected);

        Assert.Throws<ArgumentException>(() => session.SelectElements(panel.DocumentId, ["rect-1", "missing"]));
        Assert.Equal(new[] { "rect-2" }, session.InspectPanel(panel.DocumentId).SelectedElementIds);
        var cleared = session.SelectElements(panel.DocumentId, []);
        Assert.Empty(cleared.SelectedElementIds);
        Assert.False(cleared.IsDirty);
        Assert.Equal(initial.CanUndo, cleared.CanUndo);
        Assert.False(cleared.CanRedo);
    }

    [Fact]
    public void SetPanelView_RejectsInvalidValuesWithoutChangingViewOrDocumentHistory()
    {
        var (session, panel) = CreatePanel();
        var baseline = session.InspectPanel(panel.DocumentId);
        Assert.Equal((1d, 0d, 0d), (baseline.View.Zoom, baseline.View.PanX, baseline.View.PanY));

        Assert.Throws<ArgumentException>(() => session.SetPanelView(panel.DocumentId, double.NaN));
        Assert.Throws<ArgumentException>(() => session.SetPanelView(panel.DocumentId, 0.009));
        Assert.Throws<ArgumentException>(() => session.SetPanelView(panel.DocumentId, 64.01));
        Assert.Throws<ArgumentException>(() => session.SetPanelView(panel.DocumentId, 1, double.PositiveInfinity, 0));
        Assert.Throws<ArgumentException>(() => session.SetPanelView(panel.DocumentId, 1, 0, double.NaN));

        var unchanged = session.InspectPanel(panel.DocumentId);
        Assert.Equal(baseline.View, unchanged.View);
        Assert.Equal(baseline.IsDirty, unchanged.IsDirty);
        Assert.Equal(baseline.CanUndo, unchanged.CanUndo);
        Assert.Equal(baseline.CanRedo, unchanged.CanRedo);
    }

    [Fact]
    public void SaveAndReopen_PersistsEditedElementProperties()
    {
        var (session, panel) = CreatePanel();
        session.UpdateElement(panel.DocumentId, "rect-1", new PanelElementEdit(
            Name: "Saved prize", X: 7, Y: 9, Width: 24, Height: 16, IsVisible: false, IsTransformLocked: true));
        var saved = session.SavePanel(panel.DocumentId);
        Assert.False(saved.IsDirty);

        var reopenedSession = new McpEditorSession(_root, new FakeImporter(false));
        reopenedSession.OpenProject(Path.Combine(_root, "Demo", "Demo.oasisproj"));
        var reopenedPanel = reopenedSession.OpenPanelDocument(panel.Path);
        var element = reopenedSession.InspectElement(reopenedPanel.DocumentId, "rect-1");

        Assert.Equal("Saved prize", element.Name);
        Assert.Equal((7d, 9d, 24d, 16d), (element.X, element.Y, element.Width, element.Height));
        Assert.False(element.IsVisible);
        Assert.True(element.IsTransformLocked);
        Assert.False(reopenedPanel.IsDirty);
    }

    private (McpEditorSession Session, PanelSnapshot Panel) CreatePanel(bool twoElements = false)
    {
        var session = new McpEditorSession(_root, new FakeImporter(twoElements));
        session.CreateProject("Demo");
        var panel = session.CreatePanel("Main");
        File.WriteAllText(Path.Combine(_root, "layout.fml"), "fake");
        session.ImportFml(panel.DocumentId, "layout.fml");
        panel = session.SavePanel(panel.DocumentId);
        return (session, panel);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private sealed class FakeImporter(bool twoElements) : IFmlAutomationImportService
    {
        public LayoutImportResult ImportFromFml(string fmlPath, string projectRootPath, string projectAssetsPath, bool copyAssets = true) => new()
        {
            ImportedElements = twoElements
                ? [Rectangle("rect-1", "Rectangle"), Rectangle("rect-2", "Second rectangle")]
                : [Rectangle("rect-1", "Rectangle")],
            CopiedAssetRelativePaths = [], InputDefinitions = [], UnsupportedComponentTypes = [], Warnings = [], Errors = []
        };

        private static PanelElementModel Rectangle(string id, string name) => new()
        {
            ObjectId = id, Name = name, Kind = PanelElementKind.Rectangle, Width = 20, Height = 10
        };
    }
}
