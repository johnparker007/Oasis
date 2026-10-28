using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Dock.Avalonia.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm.Controls;
using OasisEditor.Avalonia;
using Xunit;

namespace OasisEditor.Avalonia.Tests;

[Collection("Avalonia headless UI")]
public sealed class WorkspacePaneDockingTests
{
    [Theory]
    [InlineData("Project")]
    [InlineData("Hierarchy")]
    [InlineData("Inspector")]
    [InlineData("Output")]
    [InlineData("AssetDocument")]
    public async Task LiveDockableCanFloatRedockAndRenderAcrossThemes(string dockableId)
    {
        await HeadlessTestHost.RunAsync(async () =>
        {
            var root = Path.Combine(Path.GetTempPath(), "OasisPaneDocking-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var session = new AuthoringSession(root);
                session.CreateProject("Pane docking");
                session.CreatePanel("Panel");
                using var capture = new WorkspaceCapture(session);
                var view = capture.View;
                await view.SynchronizeAssetDocumentsAsync();
                var commandSession = view.CommandSession;
                var dockControl = view.GetVisualDescendants().OfType<DockControl>().Single();
                var layout = Assert.IsAssignableFrom<IDockable>(dockControl.Layout);
                var factory = dockControl.Factory ?? layout.Factory;
                Assert.NotNull(factory);
                var leaves = WorkspaceLayoutState.LeafMap(layout);
                var assetDocument = Assert.Single(leaves.Values.OfType<Document>().Where(document => document.Context is WorkspaceView));
                var dockable = dockableId == "AssetDocument" ? assetDocument : leaves[dockableId];
                var anchor = dockableId == "Project" ? assetDocument : leaves["Project"];
                var context = Assert.IsAssignableFrom<Control>(dockable.Context);
                var mainWindow = Assert.IsType<Window>(TopLevel.GetTopLevel(view));
                var rectangle = commandSession.AddRectangle();
                commandSession.UpdateElement(rectangle, name: "Pane move keeps authoring edits");
                commandSession.SelectElements([rectangle]);
                var initialSelection = commandSession.SelectedIds.ToArray();

                // Establish the rendered main host before invoking the same factory APIs
                // used by Dock.Avalonia's title-bar/drag operations.
                foreach (var theme in new[] { "System", "Dark", "Light" })
                    Assert.NotEmpty(capture.Capture(theme: theme));

                for (var cycle = 0; cycle < 3; cycle++)
                {
                    factory.FloatDockable(dockable);
                    Dispatcher.UIThread.RunJobs();
                    Assert.NotEmpty(view.FloatingWorkspaceWindows);
                    var floatingWindow = Assert.Single(view.FloatingWorkspaceWindows);
                    Assert.NotEmpty(capture.CaptureFloatingWindows());
                    var floatingContext = Assert.IsAssignableFrom<Control>(dockable.Context);
                    Assert.Same(floatingWindow, TopLevel.GetTopLevel(floatingContext));
                    Assert.NotSame(mainWindow, TopLevel.GetTopLevel(floatingContext));
                    if (dockableId == "AssetDocument") Assert.Same(context, floatingContext);
                    AssertAuthoringStatePreserved();

                    foreach (var theme in new[] { "Dark", "Light", "System" })
                    {
                        Assert.NotEmpty(capture.Capture(theme: theme));
                        Assert.NotEmpty(capture.CaptureFloatingWindows());
                        Assert.Same(floatingWindow, TopLevel.GetTopLevel(Assert.IsAssignableFrom<Control>(dockable.Context)));
                    }

                    var floatingOwner = Assert.IsAssignableFrom<IDock>(dockable.Owner);
                    var destinationOwner = Assert.IsAssignableFrom<IDock>(anchor.Owner);
                    factory.MoveDockable(floatingOwner, destinationOwner, dockable, anchor);
                    factory.SetActiveDockable(dockable);
                    Dispatcher.UIThread.RunJobs();
                    if (view.FloatingWorkspaceWindows.Count > 0)
                        Assert.NotEmpty(capture.CaptureFloatingWindows());
                    Assert.NotEmpty(capture.Capture(theme: cycle % 2 == 0 ? "Dark" : "Light"));
                    Assert.Same(destinationOwner, dockable.Owner);
                    Assert.Same(mainWindow, TopLevel.GetTopLevel(Assert.IsAssignableFrom<Control>(dockable.Context)));
                    if (dockableId == "AssetDocument") Assert.Same(context, dockable.Context);
                    AssertAuthoringStatePreserved();
                }

                void AssertAuthoringStatePreserved()
                {
                    Assert.True(commandSession.IsDirty);
                    Assert.Equal(initialSelection, commandSession.SelectedIds);
                    Assert.Equal("Pane move keeps authoring edits", commandSession.Elements.Single(item => item.Id == rectangle).Name);
                }
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        });
    }
}
