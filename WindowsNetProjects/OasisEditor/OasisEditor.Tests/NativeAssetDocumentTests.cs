using Avalonia.VisualTree;
using Dock.Avalonia.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm.Controls;
using OasisEditor.Avalonia;
using Xunit;

namespace OasisEditor.Avalonia.Tests;

[Collection("Avalonia headless UI")]
public sealed class NativeAssetDocumentTests
{
    [Fact]
    public async Task OpenPanelDocumentsKeepSeparateSessionsHistoryAndSelectionAcrossFocus()
    {
        await HeadlessTestHost.RunAsync(async () =>
        {
            var project = CreateProjectWithTwoPanels();
            try
            {
                var session = OpenProject(project);
                using var capture = new WorkspaceCapture(session);
                var workspace = capture.View;
                await workspace.OpenNativeAssetAsync("Panel2D", project.PanelA);
                await workspace.OpenNativeAssetAsync("Panel2D", project.PanelB);
                await workspace.SynchronizeAssetDocumentsAsync();

                var documents = AssetDocuments(workspace);
                Assert.Equal(2, documents.Count);
                var docA = documents.Single(document => PathEquals(((WorkspaceView)document.Context!).CommandSession.PanelPath!, project.PanelA));
                var docB = documents.Single(document => PathEquals(((WorkspaceView)document.Context!).CommandSession.PanelPath!, project.PanelB));
                var editorA = Assert.IsType<WorkspaceView>(docA.Context);
                var editorB = Assert.IsType<WorkspaceView>(docB.Context);
                Assert.NotSame(editorA.CommandSession, editorB.CommandSession);
                Assert.NotSame(editorA, editorB);

                var aId = editorA.CommandSession.AddRectangle();
                editorA.CommandSession.UpdateElement(aId, name: "Pending A");
                editorA.CommandSession.SelectElements([aId]);
                var bId = editorB.CommandSession.AddRectangle();
                editorB.CommandSession.UpdateElement(bId, name: "Pending B");
                editorB.CommandSession.SelectElements([bId]);

                var dock = workspace.GetVisualDescendants().OfType<DockControl>().Single();
                var factory = dock.Factory ?? Assert.IsAssignableFrom<IDockable>(dock.Layout).Factory;
                factory!.SetActiveDockable(docA);
                Assert.Same(editorA, workspace.CommandView);
                Assert.Equal(new[] { aId }, editorA.CommandSession.SelectedIds);
                Assert.Equal(new[] { bId }, editorB.CommandSession.SelectedIds);

                factory.SetActiveDockable(docB);
                Assert.Same(editorB, workspace.CommandView);
                editorB.CommandSession.Undo();
                Assert.DoesNotContain(editorB.CommandSession.Elements, element => element.Name == "Pending B");
                Assert.Contains(editorA.CommandSession.Elements, element => element.Name == "Pending A");
                Assert.Equal(new[] { aId }, editorA.CommandSession.SelectedIds);
            }
            finally { Directory.Delete(project.Root, recursive: true); }
        });
    }

    [Fact]
    public async Task SplitPanelDocumentsRenderAsSeparateNativeDocumentOwners()
    {
        await HeadlessTestHost.RunAsync(async () =>
        {
            var project = CreateProjectWithTwoPanels();
            try
            {
                var session = OpenProject(project);
                using var capture = new WorkspaceCapture(session);
                var workspace = capture.View;
                await workspace.OpenNativeAssetAsync("Panel2D", project.PanelA);
                await workspace.OpenNativeAssetAsync("Panel2D", project.PanelB);
                await workspace.SynchronizeAssetDocumentsAsync();
                await workspace.SplitDocumentAsync("Panel2D", project.PanelA, "Panel2D", project.PanelB, "right");

                var documents = AssetDocuments(workspace);
                Assert.Equal(2, documents.Count);
                Assert.NotSame(documents[0].Owner, documents[1].Owner);
                Assert.All(documents,document=>Assert.IsAssignableFrom<Dock.Model.Controls.IDocumentDock>(document.Owner));
                Assert.NotEmpty(capture.Capture());
                var panels = workspace.GetVisualDescendants().OfType<PanelViewport>().Where(panel => panel.IsVisible).ToArray();
                Assert.Equal(2, panels.Length);
                Assert.NotEmpty(capture.Capture());
            }
            finally { Directory.Delete(project.Root, recursive: true); }
        });
    }

    [Fact]
    public async Task FloatingAndRedockingKeepsDocumentIdentityAndDirtyCloseGuard()
    {
        await HeadlessTestHost.RunAsync(async () =>
        {
            var project = CreateProjectWithTwoPanels();
            try
            {
                var session = OpenProject(project);
                using var capture = new WorkspaceCapture(session);
                var workspace = capture.View;
                await workspace.OpenNativeAssetAsync("Panel2D", project.PanelA);
                await workspace.OpenNativeAssetAsync("Panel2D", project.PanelB);
                await workspace.SynchronizeAssetDocumentsAsync();
                var document = AssetDocuments(workspace).Single(item =>
                    PathEquals(((WorkspaceView)item.Context!).CommandSession.PanelPath!, project.PanelA));
                var editor = Assert.IsType<WorkspaceView>(document.Context);
                var editorSession = editor.CommandSession;
                var id = editorSession.AddRectangle();
                editorSession.UpdateElement(id, name: "Retained through float");
                editorSession.SelectElements([id]);

                await workspace.FloatDocumentAsync("Panel2D", project.PanelA);
                Assert.Same(editor, workspace.GetFloatingDocumentView("Panel2D", project.PanelA));
                await workspace.DockDocumentAsync("Panel2D", project.PanelA);
                var redocked = AssetDocuments(workspace).Single(item => item.Id == document.Id);
                Assert.Same(document, redocked);
                Assert.Same(editor, redocked.Context);
                Assert.Same(editorSession, editor.CommandSession);
                Assert.Empty(workspace.FloatingWorkspaceWindows);
                Assert.Equal("Retained through float", editorSession.Elements.Single(item => item.Id == id).Name);
                Assert.Equal(new[] { id }, editorSession.SelectedIds);
                editorSession.Undo();
                Assert.DoesNotContain(editorSession.Elements, item => item.Name == "Retained through float");
                await Assert.ThrowsAsync<InvalidOperationException>(() => workspace.CloseDocumentAsync("Panel2D", project.PanelA));

                editorSession.Save();
                await workspace.CloseDocumentAsync("Panel2D", project.PanelA);
                Assert.Single(AssetDocuments(workspace));
                Assert.DoesNotContain(workspace.InspectOpenDocuments(), item => PathEquals(item.Path, project.PanelA));
                Assert.Contains(workspace.InspectOpenDocuments(), item => PathEquals(item.Path, project.PanelB));
            }
            finally { Directory.Delete(project.Root, recursive: true); }
        });
    }

    [Fact]
    public async Task ExplicitlyOpenedPanelsShareClipboardAndKeepPasteHistoryLocal()
    {
        await HeadlessTestHost.RunAsync(async()=>
        {
            var project=CreateProjectWithTwoPanels();
            try
            {
                using var capture=new WorkspaceCapture(OpenProject(project));
                var workspace=capture.View;
                await workspace.OpenNativeAssetAsync("Panel2D",project.PanelA);
                var first=workspace.CommandSession;var id=first.AddRectangle();first.SelectElements([id]);
                Assert.True(first.CopySelectedToClipboard());
                await workspace.OpenNativeAssetAsync("Panel2D",project.PanelB);
                var second=workspace.CommandSession;var before=second.Elements.Count;
                Assert.True(second.CanPasteClipboard);
                Assert.Single(second.PasteClipboard());Assert.Equal(before+1,second.Elements.Count);
                second.Undo();Assert.Equal(before,second.Elements.Count);
                Assert.Contains(first.Elements,element=>element.Id==id);
                Assert.Throws<InvalidOperationException>(()=>first.ShareClipboardWith(new AuthoringSession(project.Root)));
            }
            finally{Directory.Delete(project.Root,true);}
        });
    }

    [Fact]
    public async Task ClosingTheLastDocumentRestoresToolPanesAndAllowsAnotherAssetToOpen()
    {
        await HeadlessTestHost.RunAsync(async()=>
        {
            var project=CreateProjectWithTwoPanels();
            try
            {
                using var capture=new WorkspaceCapture(OpenProject(project));
                var workspace=capture.View;
                await workspace.OpenNativeAssetAsync("Panel2D",project.PanelA);
                var editor=workspace.CommandView;
                await workspace.CloseDocumentAsync("Panel2D",project.PanelA);
                Assert.Empty(workspace.InspectOpenDocuments());
                Assert.Same(workspace,workspace.CommandView);
                await workspace.OpenNativeAssetAsync("Panel2D",project.PanelB);
                Assert.NotSame(editor,workspace.CommandView);
                Assert.Equal(project.PanelB,workspace.CommandSession.PanelPath);
                Assert.NotEmpty(capture.Capture());
            }
            finally{Directory.Delete(project.Root,true);}
        });
    }

    private static List<Document> AssetDocuments(WorkspaceView workspace)
    {
        var dock = workspace.GetVisualDescendants().OfType<DockControl>().Single();
        Assert.NotNull(dock.Layout);
        return WorkspaceLayoutState.LeafMap(dock.Layout!).Values.OfType<Document>()
            .Where(document => document.Context is WorkspaceView)
            .ToList();
    }

    private static WorkspaceProject CreateProjectWithTwoPanels()
    {
        var root = Path.Combine(Path.GetTempPath(), "OasisNativeDocuments-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var author = new AuthoringSession(root);
        var projectFile = author.CreateProject("Native documents");
        var panelA = author.CreatePanel("A");
        author.AddRectangle();
        author.Save();
        var panelB = author.CreatePanel("B");
        author.AddRectangle();
        author.Save();
        return new WorkspaceProject(root, projectFile, panelA, panelB);
    }

    private static AuthoringSession OpenProject(WorkspaceProject project)
    {
        var session = new AuthoringSession(project.Root);
        session.OpenProject(project.ProjectFile);
        return session;
    }

    private static bool PathEquals(string left, string right)
        => string.Equals(Path.GetFullPath(left), Path.GetFullPath(right),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private sealed record WorkspaceProject(string Root, string ProjectFile, string PanelA, string PanelB);
}
