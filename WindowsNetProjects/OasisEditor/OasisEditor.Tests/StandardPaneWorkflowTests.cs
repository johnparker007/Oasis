using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Dock.Avalonia.Controls;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm.Controls;
using OasisEditor.Avalonia;
using Xunit;

namespace OasisEditor.Avalonia.Tests;

[Collection("Avalonia headless UI")]
public sealed class StandardPaneWorkflowTests
{
    [Fact]
    public async Task StandardPanesCloseToHiddenAndReopenThroughAddTabWithoutLosingTheirContext()
    {
        await HeadlessTestHost.RunAsync(async () =>
        {
            var project = CreateProject();
            try
            {
                using var capture = new WorkspaceCapture(project.Session);
                var view = capture.View;
                await view.SynchronizeAssetDocumentsAsync();
                Assert.NotEmpty(capture.Capture());

                var dock = view.GetVisualDescendants().OfType<DockControl>().Single();
                var layout = Assert.IsAssignableFrom<IRootDock>(dock.Layout);
                var leaves = WorkspaceLayoutState.LeafMap(layout);
                var document = Assert.Single(leaves.Values.OfType<Document>().Where(item => item.Context is WorkspaceView));

                foreach (var id in WorkspaceView.StandardWorkspaceTabs)
                {
                    var pane = leaves[id];
                    var context = pane.Context;

                    view.ToggleTabMaximised(id);
                    var maximised = MenuItems(view.CreateTabContextMenu(id));
                    Assert.False(maximised.Single(item => Equals(item.Header, "Close Tab")).IsEnabled);
                    Assert.False(maximised.Single(item => Equals(item.Header, "Add Tab")).IsEnabled);
                    view.RestoreMaximisedTab();

                    var menu = MenuItems(view.CreateTabContextMenu(id));
                    var close = menu.Single(item => Equals(item.Header, "Close Tab"));
                    Assert.True(close.IsEnabled);
                    close.RaiseEvent(new global::Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent));
                    Dispatcher.UIThread.RunJobs();

                    Assert.Contains(pane, layout.HiddenDockables!);
                    Assert.Same(context, pane.Context);
                    Assert.Same(pane, WorkspaceLayoutState.LeafMap(layout)[id]);

                    var addTab = MenuItems(view.CreateTabContextMenu(document.Id!))
                        .Single(item => Equals(item.Header, "Add Tab"));
                    var reopen = addTab.Items.OfType<MenuItem>().Single(item => Equals(item.Header, id));
                    Assert.True(reopen.IsEnabled);
                    reopen.RaiseEvent(new global::Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent));
                    Dispatcher.UIThread.RunJobs();
                    Assert.NotEmpty(capture.Capture());

                    leaves = WorkspaceLayoutState.LeafMap(layout);
                    Assert.Same(pane, leaves[id]);
                    Assert.Same(context, pane.Context);
                    Assert.DoesNotContain(pane, layout.HiddenDockables!);
                    Assert.Same(document.Owner, pane.Owner);
                    Assert.Single(leaves.Values.Where(item => item.Id == id));
                }
            }
            finally { Directory.Delete(project.Root, recursive: true); }
        });
    }

    [Fact]
    public async Task AddTabMovesAnAlreadyVisibleSingletonInsteadOfCreatingADuplicate()
    {
        await HeadlessTestHost.RunAsync(async () =>
        {
            var project = CreateProject();
            try
            {
                using var capture = new WorkspaceCapture(project.Session);
                var view = capture.View;
                await view.SynchronizeAssetDocumentsAsync();
                Assert.NotEmpty(capture.Capture());
                var layout = Assert.IsAssignableFrom<IRootDock>(view.GetVisualDescendants().OfType<DockControl>().Single().Layout);
                var leaves = WorkspaceLayoutState.LeafMap(layout);
                var pane = leaves["Hierarchy"];
                var context = pane.Context;
                var document = Assert.Single(leaves.Values.OfType<Document>().Where(item => item.Context is WorkspaceView));
                var originalOwner = pane.Owner;
                Assert.NotSame(document.Owner, originalOwner);

                var addTab = MenuItems(view.CreateTabContextMenu(document.Id!))
                    .Single(item => Equals(item.Header, "Add Tab"));
                addTab.Items.OfType<MenuItem>().Single(item => Equals(item.Header, "Hierarchy"))
                    .RaiseEvent(new global::Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent));
                Dispatcher.UIThread.RunJobs();
                Assert.NotEmpty(capture.Capture());

                leaves = WorkspaceLayoutState.LeafMap(layout);
                Assert.Same(pane, leaves["Hierarchy"]);
                Assert.Same(context, pane.Context);
                Assert.Same(document.Owner, pane.Owner);
                Assert.NotSame(originalOwner, pane.Owner);
                Assert.Single(leaves.Values.Where(item => item.Id == "Hierarchy"));

                var output = leaves["Output"];
                var outputContext = output.Context;
                var inspectorPane = leaves["Inspector"];
                var originalOutputOwner = output.Owner;
                var inspectorAddTab = MenuItems(view.CreateTabContextMenu(inspectorPane.Id!))
                    .Single(item => Equals(item.Header, "Add Tab"));
                inspectorAddTab.Items.OfType<MenuItem>().Single(item => Equals(item.Header, "Output"))
                    .RaiseEvent(new global::Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent));
                Dispatcher.UIThread.RunJobs();
                Assert.NotEmpty(capture.Capture());
                leaves = WorkspaceLayoutState.LeafMap(layout);
                Assert.Same(output, leaves["Output"]);
                Assert.Same(outputContext, output.Context);
                Assert.Same(inspectorPane.Owner, output.Owner);
                Assert.NotSame(originalOutputOwner, output.Owner);
                Assert.Single(leaves.Values.Where(item => item.Id == "Output"));
            }
            finally { Directory.Delete(project.Root, recursive: true); }
        });
    }

    [Fact]
    public async Task ClosingFloatingStandardPaneRemovesItsWindowAndRetainsItForReopening()
    {
        await HeadlessTestHost.RunAsync(async () =>
        {
            var project = CreateProject();
            try
            {
                using var capture = new WorkspaceCapture(project.Session);
                var view = capture.View;
                await view.SynchronizeAssetDocumentsAsync();
                Assert.NotEmpty(capture.Capture());
                var dock = view.GetVisualDescendants().OfType<DockControl>().Single();
                var layout = Assert.IsAssignableFrom<IRootDock>(dock.Layout);
                var pane = WorkspaceLayoutState.LeafMap(layout)["Hierarchy"];
                var context = pane.Context;
                var factory = dock.Factory ?? layout.Factory;
                factory!.FloatDockable(pane);
                Dispatcher.UIThread.RunJobs();
                Assert.Single(view.FloatingWorkspaceWindows);
                Assert.NotEmpty(capture.CaptureFloatingWindows());

                var close = MenuItems(view.CreateTabContextMenu(pane.Id!))
                    .Single(item => Equals(item.Header, "Close Tab"));
                Assert.True(close.IsEnabled);
                close.RaiseEvent(new global::Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent));
                Dispatcher.UIThread.RunJobs();

                Assert.Empty(view.FloatingWorkspaceWindows);
                Assert.Contains(pane, layout.HiddenDockables!);
                Assert.Same(context, pane.Context);
                Assert.Same(pane, WorkspaceLayoutState.LeafMap(layout)["Hierarchy"]);
                Assert.NotEmpty(capture.Capture());

                var document = Assert.Single(WorkspaceLayoutState.LeafMap(layout).Values.OfType<Document>()
                    .Where(item => item.Context is WorkspaceView));
                var addTab = MenuItems(view.CreateTabContextMenu(document.Id!))
                    .Single(item => Equals(item.Header, "Add Tab"));
                addTab.Items.OfType<MenuItem>().Single(item => Equals(item.Header, "Hierarchy"))
                    .RaiseEvent(new global::Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent));
                Dispatcher.UIThread.RunJobs();
                Assert.NotEmpty(capture.Capture());

                Assert.Empty(view.FloatingWorkspaceWindows);
                Assert.DoesNotContain(pane, layout.HiddenDockables!);
                Assert.Same(context, pane.Context);
                Assert.Same(pane, WorkspaceLayoutState.LeafMap(layout)["Hierarchy"]);
            }
            finally { Directory.Delete(project.Root, recursive: true); }
        });
    }

    [Fact]
    public async Task HiddenStandardPaneSurvivesWorkspaceSaveAndRestoreAndCanBeReopened()
    {
        await HeadlessTestHost.RunAsync(async () =>
        {
            var project = CreateProject();
            try
            {
                using var capture = new WorkspaceCapture(project.Session);
                var view = capture.View;
                await view.SynchronizeAssetDocumentsAsync();
                Assert.NotEmpty(capture.Capture());
                var persistencePath = Path.Combine(project.Root, "workspace.json");
                view.ConfigureWorkspacePersistence(persistencePath, restore: false);

                var layout = Assert.IsAssignableFrom<IRootDock>(view.GetVisualDescendants().OfType<DockControl>().Single().Layout);
                var output = WorkspaceLayoutState.LeafMap(layout)["Output"];
                var close = MenuItems(view.CreateTabContextMenu("Output"))
                    .Single(item => Equals(item.Header, "Close Tab"));
                Assert.True(close.IsEnabled);
                close.RaiseEvent(new global::Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent));
                Dispatcher.UIThread.RunJobs();
                Assert.Contains(output, layout.HiddenDockables!);
                view.SaveWorkspace();

                view.RestoreWorkspace();
                await view.WaitForSceneAsync();
                Assert.NotEmpty(capture.Capture());
                layout = Assert.IsAssignableFrom<IRootDock>(view.GetVisualDescendants().OfType<DockControl>().Single().Layout);
                var restoredLeaves = WorkspaceLayoutState.LeafMap(layout);
                var restoredOutput = restoredLeaves["Output"];
                Assert.Contains(restoredOutput, layout.HiddenDockables!);

                var windowMenu = view.FindMenu("Window", "Output");
                Assert.True(windowMenu.IsEnabled);
                windowMenu.RaiseEvent(new global::Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent));
                Dispatcher.UIThread.RunJobs();
                Assert.NotEmpty(capture.Capture());
                Assert.Same(restoredOutput, WorkspaceLayoutState.LeafMap(layout)["Output"]);
                Assert.DoesNotContain(restoredOutput, layout.HiddenDockables!);
            }
            finally { Directory.Delete(project.Root, recursive: true); }
        });
    }

    [Theory]
    [InlineData("Hierarchy")]
    [InlineData("Inspector")]
    [InlineData("AssetDocument")]
    public async Task RestoringMaximisedPaneRestoresItsFocusAfterTheWorkspaceIsCaptured(string paneId)
    {
        await HeadlessTestHost.RunAsync(async () =>
        {
            var project = CreateProject();
            try
            {
                using var capture = new WorkspaceCapture(project.Session);
                var view = capture.View;
                await view.SynchronizeAssetDocumentsAsync();
                Assert.NotEmpty(capture.Capture());
                var layout = Assert.IsAssignableFrom<IRootDock>(view.GetVisualDescendants().OfType<DockControl>().Single().Layout);
                var leaves = WorkspaceLayoutState.LeafMap(layout);
                var pane = paneId == "AssetDocument"
                    ? Assert.Single(leaves.Values.OfType<Document>().Where(item => item.Context is WorkspaceView))
                    : leaves[paneId];

                view.ToggleTabMaximised(pane.Id!);
                Assert.NotEmpty(capture.Capture());
                Assert.Equal(pane.Id, view.InspectTabPresentation().MaximisedPaneId);
                view.RestoreMaximisedTab();
                await view.SynchronizeAssetDocumentsAsync();
                Assert.NotEmpty(capture.Capture());
                Dispatcher.UIThread.RunJobs();

                Assert.Null(view.InspectTabPresentation().MaximisedPaneId);
                var owner = Assert.IsAssignableFrom<IDock>(pane.Owner);
                Assert.Same(pane, layout.FocusedDockable);
                Assert.True(owner.IsActive);
                Assert.Same(pane, WorkspaceLayoutState.LeafMap(layout)[pane.Id!]);
            }
            finally { Directory.Delete(project.Root, recursive: true); }
        });
    }

    private static IReadOnlyList<MenuItem> MenuItems(ContextMenu menu)
        => menu.Items.OfType<MenuItem>().ToArray();

    private static (string Root, AuthoringSession Session) CreateProject()
    {
        var root = Path.Combine(Path.GetTempPath(), "OasisStandardPanes-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var session = new AuthoringSession(root);
        session.CreateProject("Standard panes");
        session.CreatePanel("Panel");
        session.Save();
        return (root, session);
    }
}
