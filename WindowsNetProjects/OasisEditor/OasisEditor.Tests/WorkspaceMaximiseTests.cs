using Avalonia.Headless;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Dock.Avalonia.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm.Controls;
using OasisEditor.Avalonia;
using Xunit;

namespace OasisEditor.Avalonia.Tests;

[Collection("Avalonia headless UI")]
public sealed class WorkspaceMaximiseTests
{
    [Theory]
    [InlineData("AssetDocument")]
    [InlineData("Project")]
    [InlineData("Hierarchy")]
    [InlineData("Inspector")]
    [InlineData("Output")]
    public async Task MaximisingDockedTabPreservesPaneLayoutContextAndAuthoringHistory(string paneId)
    {
        await HeadlessTestHost.RunAsync(async () =>
        {
            var project = CreateProject();
            try
            {
                using var capture = new WorkspaceCapture(project.Session);
                var view = capture.View;
                await view.SynchronizeAssetDocumentsAsync();
                var commandSession = view.CommandSession;
                var rectangle = commandSession.AddRectangle();
                commandSession.UpdateElement(rectangle, name: "Pending maximize edit");

                var dockControl = view.GetVisualDescendants().OfType<DockControl>().Single();
                var layout = dockControl.Layout!;
                var panes = WorkspaceLayoutState.LeafMap(layout);
                var document = Assert.Single(panes.Values.OfType<Document>().Where(item => item.Context is WorkspaceView));
                var pane = paneId == "AssetDocument" ? document : panes[paneId];
                var owner = pane.Owner;
                var context = pane.Context;
                var mainWindow = Assert.IsAssignableFrom<Window>(TopLevel.GetTopLevel(view));
                var initialBounds = mainWindow.ClientSize;
                Assert.NotEmpty(capture.Capture());

                var presentation = view.ToggleTabMaximised(pane.Id!);
                await view.SynchronizeAssetDocumentsAsync();
                Assert.Equal(pane.Id, presentation.MaximisedPaneId);
                Assert.False(presentation.WasFloating);
                Assert.Same(layout, dockControl.Layout);
                Assert.Same(owner, pane.Owner);
                Assert.Same(context, pane.Context);
                Assert.NotEmpty(capture.Capture());
                Assert.Same(mainWindow, TopLevel.GetTopLevel(Assert.IsAssignableFrom<Control>(context)));
                AssertAuthoringStatePreserved();

                var maximisedMenu = MenuItems(view.CreateTabContextMenu(pane.Id!));
                Assert.True(maximisedMenu.Single(item => Equals(item.Header, "Maximise")).IsChecked);
                Assert.False(maximisedMenu.Single(item => Equals(item.Header, "Close Tab")).IsEnabled);

                maximisedMenu.Single(item => Equals(item.Header, "Maximise"))
                    .RaiseEvent(new global::Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent));
                Assert.Null(view.InspectTabPresentation().MaximisedPaneId);
                Assert.Same(layout, dockControl.Layout);
                Assert.Same(owner, pane.Owner);
                Assert.Same(context, pane.Context);
                Assert.Equal(initialBounds, mainWindow.ClientSize);
                Assert.NotEmpty(capture.Capture());
                AssertAuthoringStatePreserved();

                var restoredMenu = MenuItems(view.CreateTabContextMenu(pane.Id!));
                Assert.False(restoredMenu.Single(item => Equals(item.Header, "Maximise")).IsChecked);
                Assert.Equal(pane is Document, restoredMenu.Single(item => Equals(item.Header, "Close Tab")).IsEnabled);

                view.CommandSession.Undo();
                Assert.Equal("Rectangle", view.CommandSession.Elements.Single(item => item.Id == rectangle).Name);
                Assert.True(view.CommandSession.IsDirty);
                view.CommandSession.Undo();
                Assert.DoesNotContain(view.CommandSession.Elements, item => item.Id == rectangle);

                void AssertAuthoringStatePreserved()
                {
                    Assert.True(commandSession.IsDirty);
                    Assert.Equal("Pending maximize edit", commandSession.Elements.Single(item => item.Id == rectangle).Name);
                    Assert.Contains(rectangle, commandSession.SelectedIds);
                }
            }
            finally { Directory.Delete(project.Root, recursive: true); }
        });
    }

    [Theory]
    [InlineData("AssetDocument")]
    [InlineData("Project")]
    public async Task MaximisingFloatingTabRestoresItsOriginalHostAndRetainsUndoHistory(string paneId)
    {
        await HeadlessTestHost.RunAsync(async () =>
        {
            var project = CreateProject();
            try
            {
                using var capture = new WorkspaceCapture(project.Session);
                var view = capture.View;
                await view.SynchronizeAssetDocumentsAsync();
                var commands = view.CommandSession;
                var rectangle = commands.AddRectangle();
                commands.UpdateElement(rectangle, name: "Floating maximize edit");
                var dockControl = view.GetVisualDescendants().OfType<DockControl>().Single();
                var rootLayout = dockControl.Layout!;
                var rootPanes = WorkspaceLayoutState.LeafMap(rootLayout);
                var pane = paneId == "AssetDocument" ? AssetDocument(view) : rootPanes[paneId];
                var floatingBounds = new WorkspaceWindowState(180, 160, 1020, 720);
                if (paneId == "AssetDocument")
                    await view.FloatDocumentAsync("Panel2D", project.PanelPath, floatingBounds);
                else
                {
                    var factory = dockControl.Factory ?? rootLayout.Factory;
                    factory!.FloatDockable(pane);
                    Dispatcher.UIThread.RunJobs();
                }

                var layout = dockControl.Layout!;
                pane = WorkspaceLayoutState.LeafMap(layout).Values.Single(item => item.Id == pane.Id);
                var owner = pane.Owner;
                var context = pane.Context;
                var host = Assert.IsAssignableFrom<Window>(TopLevel.GetTopLevel(Assert.IsAssignableFrom<Control>(context)));
                Assert.NotSame(TopLevel.GetTopLevel(view), host);
                Assert.Equal(WindowState.Normal, host.WindowState);
                Assert.NotEmpty(capture.CaptureFloatingWindows());
                var initialPosition = host.Position;
                var initialSize = host.ClientSize;
                var originalContent = host.Content;
                var initialSelection = commands.SelectedIds.ToArray();

                var presentation = view.ToggleTabMaximised(pane.Id!);
                await view.SynchronizeAssetDocumentsAsync();
                Assert.Equal(pane.Id, presentation.MaximisedPaneId);
                Assert.True(presentation.WasFloating);
                Assert.Same(layout, dockControl.Layout);
                Assert.Same(owner, pane.Owner);
                Assert.Same(context, pane.Context);
                Assert.NotEmpty(capture.CaptureFloatingWindows());
                await WaitForPresentationAsync(host,()=>ReferenceEquals(host,TopLevel.GetTopLevel(Assert.IsAssignableFrom<Control>(context))));
                Assert.Same(host, TopLevel.GetTopLevel(Assert.IsAssignableFrom<Control>(context)));
                Assert.Equal(WindowState.Maximized, host.WindowState);
                Assert.NotSame(originalContent, host.Content);
                Assert.NotEmpty(capture.CaptureFloatingWindows());

                var maximisedMenu = MenuItems(view.CreateTabContextMenu(pane.Id!));
                Assert.True(maximisedMenu.Single(item => Equals(item.Header, "Maximise")).IsChecked);
                Assert.False(maximisedMenu.Single(item => Equals(item.Header, "Close Tab")).IsEnabled);
                view.RestoreMaximisedTab();

                Assert.Null(view.InspectTabPresentation().MaximisedPaneId);
                Assert.Same(layout, dockControl.Layout);
                Assert.Same(owner, pane.Owner);
                Assert.Same(context, pane.Context);
                Assert.Same(originalContent, host.Content);
                Assert.Equal(WindowState.Normal, host.WindowState);
                Assert.Equal(initialPosition, host.Position);
                Assert.Equal(initialSize, host.ClientSize);
                Assert.Equal(initialSelection, commands.SelectedIds);
                Assert.True(commands.IsDirty);
                Assert.Equal("Floating maximize edit", commands.Elements.Single(item => item.Id == rectangle).Name);
                Assert.NotEmpty(capture.CaptureFloatingWindows());
                var restoredMenu = MenuItems(view.CreateTabContextMenu(pane.Id!));
                Assert.False(restoredMenu.Single(item => Equals(item.Header, "Maximise")).IsChecked);
                Assert.Equal(pane is Document, restoredMenu.Single(item => Equals(item.Header, "Close Tab")).IsEnabled);

                commands.Undo();
                Assert.Equal("Rectangle", commands.Elements.Single(item => item.Id == rectangle).Name);
                Assert.True(commands.IsDirty);
                commands.Undo();
                Assert.DoesNotContain(commands.Elements, item => item.Id == rectangle);
                Assert.Same(pane, WorkspaceLayoutState.LeafMap(layout).Values.Single(item => item.Id == pane.Id));
            }
            finally { Directory.Delete(project.Root, recursive: true); }
        });
    }

    [Theory]
    [InlineData("DocumentTabStripItem", "AssetDocument")]
    [InlineData("ToolTabStripItem", "Project")]
    [InlineData("ToolChromeGrip", "Project")]
    public async Task NativeTabDoubleClickMaximisesAndOverlayDoubleClickRestores(string targetKind, string paneId)
    {
        await HeadlessTestHost.RunAsync(async () =>
        {
            var project = CreateProject();
            try
            {
                using var capture = new WorkspaceCapture(project.Session);
                var view = capture.View;
                await view.SynchronizeAssetDocumentsAsync();
                Assert.NotEmpty(capture.Capture(1400, 1200));
                var dockControl = view.GetVisualDescendants().OfType<DockControl>().Single();
                var layout = dockControl.Layout!;
                var panes = WorkspaceLayoutState.LeafMap(layout);
                var document = Assert.Single(panes.Values.OfType<Document>().Where(item => item.Context is WorkspaceView));
                var pane = paneId == "AssetDocument" ? document : panes[paneId];
                var context = pane.Context;
                var owner = pane.Owner;
                var window = Assert.IsAssignableFrom<Window>(TopLevel.GetTopLevel(view));
                var target = FindGestureTarget(view, targetKind, pane.Id!);
                AssertHitTestableInWindow(target, window);

                DoubleClick(window, target);
                Assert.Equal(pane.Id, view.InspectTabPresentation().MaximisedPaneId);
                Assert.False(view.InspectTabPresentation().WasFloating);
                Assert.Empty(view.FloatingWorkspaceWindows);
                Assert.Same(layout, dockControl.Layout);
                Assert.Same(owner, pane.Owner);
                Assert.Same(context, pane.Context);
                Assert.NotEmpty(capture.Capture());

                var overlayHeader = Assert.Single(view.GetVisualDescendants().OfType<Control>().Where(control => control.Name == "MaximisedTabHeader"));
                DoubleClick(window, overlayHeader);
                Assert.Null(view.InspectTabPresentation().MaximisedPaneId);
                Assert.Empty(view.FloatingWorkspaceWindows);
                Assert.Same(layout, dockControl.Layout);
                Assert.Same(owner, pane.Owner);
                Assert.Same(context, pane.Context);
                Assert.NotEmpty(capture.Capture());
            }
            finally { Directory.Delete(project.Root, recursive: true); }
        });
    }

    [Theory]
    [InlineData("AssetDocument","DocumentTabStripItem")]
    [InlineData("Project","ToolChromeGrip")]
    public async Task FloatingTabDoubleClickUsesItsExistingHost(string paneId,string targetKind)
    {
        await HeadlessTestHost.RunAsync(async()=>
        {
            var project=CreateProject();
            try
            {
                using var capture=new WorkspaceCapture(project.Session);var view=capture.View;
                await view.SynchronizeAssetDocumentsAsync();
                var pane=paneId=="AssetDocument"?AssetDocument(view):WorkspaceLayoutState.LeafMap(view.GetVisualDescendants().OfType<DockControl>().Single().Layout!)[paneId];
                view.MoveWorkspacePane(pane.Id!,"float");capture.Capture();capture.CaptureFloatingWindows();
                var host=Assert.Single(view.FloatingWorkspaceWindows);var owner=pane.Owner;
                DoubleClick(host,FindGestureTarget(host,targetKind,pane.Id!));
                Assert.Equal(pane.Id,view.InspectTabPresentation().MaximisedPaneId);
                Assert.True(view.InspectTabPresentation().WasFloating);
                capture.CaptureFloatingWindows();
                await WaitForPresentationAsync(host,()=>host.GetVisualDescendants().OfType<Control>().Any(c=>c.Name=="MaximisedTabHeader"));
                DoubleClick(host,host.GetVisualDescendants().OfType<Control>().Single(c=>c.Name=="MaximisedTabHeader"));
                Assert.Null(view.InspectTabPresentation().MaximisedPaneId);
                Assert.Same(owner,pane.Owner);Assert.Same(host,Assert.Single(view.FloatingWorkspaceWindows));
                Assert.Equal(WindowState.Normal,host.WindowState);
            }
            finally{Directory.Delete(project.Root,true);}
        });
    }

    [Fact]
    public async Task DoubleClickingDocumentBodyDoesNotMaximiseOrFloatItsTab()
    {
        await HeadlessTestHost.RunAsync(async () =>
        {
            var project = CreateProject();
            try
            {
                using var capture = new WorkspaceCapture(project.Session);
                var view = capture.View;
                await view.SynchronizeAssetDocumentsAsync();
                Assert.NotEmpty(capture.Capture(1400, 1200));
                var dockControl = view.GetVisualDescendants().OfType<DockControl>().Single();
                var layout = dockControl.Layout!;
                var document = AssetDocument(view);
                var context = document.Context;
                var owner = document.Owner;
                var viewport = Assert.Single(view.GetVisualDescendants().OfType<PanelViewport>().Where(control => control.IsVisible));
                var window = Assert.IsAssignableFrom<Window>(TopLevel.GetTopLevel(view));

                DoubleClick(window, viewport);

                Assert.Null(view.InspectTabPresentation().MaximisedPaneId);
                Assert.Empty(view.FloatingWorkspaceWindows);
                Assert.Same(layout, dockControl.Layout);
                Assert.Same(owner, document.Owner);
                Assert.Same(context, document.Context);
                Assert.NotEmpty(capture.Capture());
            }
            finally { Directory.Delete(project.Root, recursive: true); }
        });
    }

    [Theory]
    [InlineData("DocumentTabStripItem", "AssetDocument")]
    [InlineData("ToolTabStripItem", "Project")]
    public async Task NativeTabRightClickKeepsDockMenuAndMaximiseRestoresFromOverlay(string targetKind, string paneId)
    {
        await HeadlessTestHost.RunAsync(async () =>
        {
            var project = CreateProject();
            try
            {
                using var capture = new WorkspaceCapture(project.Session);
                var view = capture.View;
                await view.SynchronizeAssetDocumentsAsync();
                Assert.NotEmpty(capture.Capture(1400, 1200));
                var panes = WorkspaceLayoutState.LeafMap(view.GetVisualDescendants().OfType<DockControl>().Single().Layout!);
                var pane = paneId == "AssetDocument" ? AssetDocument(view) : panes[paneId];
                var target = FindGestureTarget(view, targetKind, pane.Id!);
                var window = Assert.IsAssignableFrom<Window>(TopLevel.GetTopLevel(view));
                AssertHitTestableInWindow(target, window);

                RightClick(window, target);
                var menu = Assert.IsType<ContextMenu>(target.ContextMenu);
                var menuItems = MenuItems(menu);
                var maximise = menuItems.Single(item => item.Name == "OasisMaximiseTab");
                Assert.Contains(menuItems, item => Equals(item.Header, "Close Tab"));
                Assert.Contains(menuItems, item => Equals(item.Header, "Float"));
                Assert.All(menuItems.Where(i=>i.Items.Count==0 && i.IsVisible && i.Header?.ToString() is not ("Maximise" or "Close Tab" or "Float")),i=>Assert.NotNull(i.Command));
                maximise.RaiseEvent(new global::Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent));
                Assert.Equal(pane.Id, view.InspectTabPresentation().MaximisedPaneId);

                Assert.Single(view.GetVisualDescendants().OfType<Control>().Where(control => control.Name == "MaximisedTabHeader"));
                capture.Capture(1400,1200);
                var overlayHeader=view.GetVisualDescendants().OfType<Control>().Single(c=>c.Name=="MaximisedTabHeader");
                RightClick(window,overlayHeader);
                var overlayMenu=Assert.IsType<ContextMenu>(overlayHeader.ContextMenu);
                Assert.True(MenuItems(overlayMenu).Single(item => Equals(item.Header, "Maximise")).IsChecked);
                Assert.False(MenuItems(overlayMenu).Single(item => Equals(item.Header, "Close Tab")).IsEnabled);
                MenuItems(overlayMenu).Single(item => Equals(item.Header, "Maximise"))
                    .RaiseEvent(new global::Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent));
                Assert.Null(view.InspectTabPresentation().MaximisedPaneId);
                Assert.NotEmpty(capture.Capture());
            }
            finally { Directory.Delete(project.Root, recursive: true); }
        });
    }

    [Fact]
    public async Task FactoryCannotCloseAMaximisedCleanNativeDocument()
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
                var dockControl = view.GetVisualDescendants().OfType<DockControl>().Single();
                var layout = dockControl.Layout!;
                var pane = AssetDocument(view);
                view.ToggleTabMaximised(pane.Id!);
                (dockControl.Factory ?? layout.Factory)!.CloseDockable(pane);
                Assert.Equal(pane.Id, view.InspectTabPresentation().MaximisedPaneId);
                Assert.Same(pane, WorkspaceLayoutState.LeafMap(layout).Values.Single(item => item.Id == pane.Id));
                view.RestoreMaximisedTab();
                Assert.Same(pane, AssetDocument(view));
            }
            finally { Directory.Delete(project.Root, recursive: true); }
        });
    }

    [Fact]
    public async Task ClosingFloatingHostWhileMaximisedDoesNotCloseTheTab()
    {
        await HeadlessTestHost.RunAsync(async () =>
        {
            var project = CreateProject();
            try
            {
                using var capture = new WorkspaceCapture(project.Session);
                var view = capture.View;
                await view.SynchronizeAssetDocumentsAsync();
                await view.FloatDocumentAsync("Panel2D", project.PanelPath, new(180, 160, 1020, 720));
                Assert.NotEmpty(capture.CaptureFloatingWindows());
                var pane = AssetDocument(view);
                var context = pane.Context;
                var host = Assert.IsAssignableFrom<Window>(TopLevel.GetTopLevel(Assert.IsAssignableFrom<Control>(context)));
                var layout = view.GetVisualDescendants().OfType<DockControl>().Single().Layout!;

                view.ToggleTabMaximised(pane.Id!);
                Assert.Equal(pane.Id, view.InspectTabPresentation().MaximisedPaneId);
                host.Close();
                Dispatcher.UIThread.RunJobs();

                Assert.Equal(pane.Id,view.InspectTabPresentation().MaximisedPaneId);
                view.RestoreMaximisedTab();
                Assert.Contains(host, view.FloatingWorkspaceWindows);
                Assert.NotEmpty(capture.CaptureFloatingWindows());
                Assert.Same(context, pane.Context);
                await WaitForPresentationAsync(host,()=>ReferenceEquals(host,TopLevel.GetTopLevel(Assert.IsAssignableFrom<Control>(context))));
                Assert.True(ReferenceEquals(host,TopLevel.GetTopLevel(Assert.IsAssignableFrom<Control>(context))),
                    $"visible={host.IsVisible};content={host.Content?.GetType().Name};template={host.ContentTemplate?.GetType().Name};docks={host.GetVisualDescendants().OfType<DockControl>().Count()};leaves={WorkspaceLayoutState.LeafMap(layout).Count};parent={(context as Control)?.Parent?.GetType().Name}");
                Assert.Same(pane, WorkspaceLayoutState.LeafMap(layout).Values.Single(item => item.Id == pane.Id));
                Assert.NotEmpty(capture.CaptureFloatingWindows());
            }
            finally { Directory.Delete(project.Root, recursive: true); }
        });
    }

    private static async Task WaitForPresentationAsync(Window host,Func<bool> ready)
    {
        var deadline=DateTime.UtcNow.AddSeconds(2);
        while(DateTime.UtcNow<deadline)
        {
            host.UpdateLayout();Dispatcher.UIThread.RunJobs();AvaloniaHeadlessPlatform.ForceRenderTimerTick(2);
            if(ready())return;
            await Task.Delay(10);
        }
        Assert.True(ready(),"The deferred floating content did not become visible.");
    }

    private static IReadOnlyList<MenuItem> MenuItems(ContextMenu menu)
        => menu.Items.OfType<MenuItem>().ToArray();

    private static Control FindGestureTarget(Control view, string targetKind, string paneId)
    {
        var controls = view.GetVisualDescendants().OfType<Control>().ToArray();
        if (targetKind == "ToolChromeGrip")
            return controls.Single(control => control.Name == "PART_Grip"
                && Ancestors(control).OfType<Control>().Any(parent => parent.GetType().Name == "ToolChromeControl"
                    && DirectlyBelongsToPane(parent, paneId)));
        return controls.Single(control => control.GetType().Name == targetKind && DirectlyBelongsToPane(control, paneId));
    }

    private static bool DirectlyBelongsToPane(Control control, string paneId)
    {
        var pane = control.DataContext as IDockable;
        if (pane is IDock dock) pane = dock.ActiveDockable;
        return pane?.Id == paneId;
    }

    private static void AssertHitTestableInWindow(Control target, Window window)
    {
        var origin = target.TranslatePoint(new Point(0, 0), window);
        Assert.True(origin.HasValue, $"Could not map {target.GetType().Name} to its window.");
        var bounds = new Rect(origin!.Value, target.Bounds.Size);
        var client = new Rect(window.ClientSize);
        Assert.True(target.IsVisible && target.Bounds.Width > 0 && target.Bounds.Height > 0,
            $"{target.GetType().Name} is not laid out: bounds={target.Bounds}, visible={target.IsVisible}.");
        Assert.True(client.Contains(bounds),
            $"{target.GetType().Name} is clipped by the window: target={bounds}, client={client}, " +
            $"window={window.Bounds}, parent={target.GetVisualParent()?.GetType().Name}.");
        if (target.GetType().Name == "ToolTabStripItem")
        {
            var point = bounds.Center;
            var hit = window.InputHitTest(point) as Visual;
            Assert.True(hit is not null && (ReferenceEquals(hit, target) || Ancestors(hit).Contains(target)),
                $"Project tool tab is within window bounds but not under the pointer: point={point}, " +
                $"hit={hit?.GetType().Name ?? "none"}, target={bounds}, client={client}.");
        }
    }

    private static IEnumerable<Visual> Ancestors(Visual visual)
    {
        for (var parent = visual.GetVisualParent(); parent is not null; parent = parent.GetVisualParent())
            yield return parent;
    }

    private static void DoubleClick(Window window, Control target)
    {
        var point = target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), window);
        Assert.True(point.HasValue, $"Could not map {target.GetType().Name} to its window.");
        for (var click = 0; click < 2; click++)
        {
            window.MouseDown(point!.Value, MouseButton.Left);
            window.MouseUp(point.Value, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static void RightClick(Window window, Control target)
    {
        var point = target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), window);
        Assert.True(point.HasValue, $"Could not map {target.GetType().Name} to its window.");
        window.MouseDown(point!.Value, MouseButton.Right);
        window.MouseUp(point.Value, MouseButton.Right);
        Dispatcher.UIThread.RunJobs();
    }

    private static Document AssetDocument(WorkspaceView view)
        => WorkspaceLayoutState.LeafMap(view.GetVisualDescendants().OfType<DockControl>().Single().Layout!)
            .Values.OfType<Document>().Single(item => item.Context is WorkspaceView);

    private static TestProject CreateProject()
    {
        var root = Path.Combine(Path.GetTempPath(), "OasisWorkspaceMaximise-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var session = new AuthoringSession(root);
        session.CreateProject("Maximise");
        var panelPath = session.CreatePanel("Panel");
        session.AddRectangle();
        session.Save();
        return new TestProject(root, session, panelPath);
    }

    private sealed record TestProject(string Root, AuthoringSession Session, string PanelPath);
}
