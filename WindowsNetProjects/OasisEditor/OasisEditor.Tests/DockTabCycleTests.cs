using Avalonia.Headless;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Dock.Avalonia.Controls;
using Dock.Model.Core;
using Dock.Model.Controls;
using Dock.Model.Mvvm.Controls;
using OasisEditor.Avalonia;
using Xunit;

namespace OasisEditor.Avalonia.Tests;

[Collection("Avalonia headless UI")]
public sealed class DockTabCycleTests
{
    private static readonly string[] ExpectedHeaders = ["Maximise", "Add Tab", "<separator>", "Close Tab", "Float"];

    [Theory]
    [InlineData("AssetDocument", "DocumentTabStripItem", false)]
    [InlineData("AssetDocument", "DocumentTabStripItem", true)]
    [InlineData("Hierarchy", "ToolChromeGrip", false)]
    [InlineData("Hierarchy", "ToolChromeGrip", true)]
    [InlineData("Inspector", "ToolChromeGrip", false)]
    [InlineData("Project", "ToolChromeGrip", true)]
    [InlineData("Output", "ToolChromeGrip", false)]
    public async Task FreshlyFloatedNativeTabMenusAndFirstDoubleClickRemainConsistent(string paneId, string targetKind, bool persistedFirstFloat)
    {
        await HeadlessTestHost.RunAsync(async () =>
        {
            var project = CreateProject();
            try
            {
                using var capture = new WorkspaceCapture(project.Session);
                var view = capture.View;
                await view.SynchronizeAssetDocumentsAsync();
                capture.Capture(1400, 900);

                var dockControl = view.GetVisualDescendants().OfType<DockControl>().Single();
                var mainLayout = dockControl.Layout!;
                var pane = FindPane(view, paneId);
                var actualPaneId = pane.Id!;
                var mainWindow = Assert.IsAssignableFrom<Window>(TopLevel.GetTopLevel(view));
                var initialFloatingHostCount = view.FloatingWorkspaceWindows.Count;
                view.ConfigureWorkspacePersistence(Path.Combine(project.Root, "workspace.json"), restore: false);

                for (var cycle = 0; cycle < 3; cycle++)
                {
                    if (cycle == 0)
                    {
                        // Use the document lifecycle or Dock factory for the initial float.
                        if (paneId == "AssetDocument")
                            await view.FloatDocumentAsync("Panel2D", project.PanelPath);
                        else
                            view.MoveWorkspacePane(actualPaneId, "float");
                    }
                    else
                    {
                        var dockedTargetKind = paneId != "AssetDocument" ? "ToolTabStripItem" : targetKind;
                        var dockedTarget = FindGestureTarget(view, dockedTargetKind, actualPaneId);
                        var dockedMenu = OpenContextMenu(mainWindow, dockedTarget);
                        var dockedSnapshot = InspectMenu(dockedMenu);
                        Assert.Equal(ExpectedHeaders, dockedSnapshot.Headers);
                        Assert.False(dockedSnapshot.MaximiseChecked);
                        Assert.True(dockedSnapshot.FloatEnabled);
                        dockedMenu.Items.OfType<MenuItem>().Single(item => Equals(item.Header, "Float"))
                            .RaiseEvent(new global::Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent));
                        Dispatcher.UIThread.RunJobs();
                    }
                    capture.CaptureFloatingWindows();
                    Assert.Equal(initialFloatingHostCount + 1, view.FloatingWorkspaceWindows.Count);
                    if (cycle == 0 && persistedFirstFloat)
                    {
                        // Persist and restore the fresh floating layout before its first gesture.
                        view.SaveWorkspace();
                        view.ResetLayout();
                        view.RestoreWorkspace();
                        await view.WaitForSceneAsync();
                        await view.SynchronizeAssetDocumentsAsync();
                        capture.CaptureFloatingWindows();
                        Assert.Equal(initialFloatingHostCount + 1, view.FloatingWorkspaceWindows.Count);
                        mainLayout = dockControl.Layout!;
                        actualPaneId = FindPane(view, paneId).Id;
                    }
                    if(cycle == 0)
                    {
                        var commands = view.GetDocumentEditor("Panel2D").CommandSession;
                        var rectangle = commands.AddRectangle();
                        commands.UpdateElement(rectangle, name: "Retained across tab cycles");
                    }
                    var host = Assert.Single(view.FloatingWorkspaceWindows);
                    var floatingPane = FindPane(view, actualPaneId);
                    var originalOwner = floatingPane.Owner;
                    var target = FindGestureTarget(host, targetKind, actualPaneId);

                    // In the fresh case, prove the untouched pointer gesture works before a menu primes the tab.
                    if (cycle == 0 && !persistedFirstFloat)
                    {
                        DoubleClick(host, target);
                        Assert.Equal(actualPaneId, view.InspectTabPresentation().MaximisedPaneId);
                        Assert.True(view.InspectTabPresentation().WasFloating);
                        var firstOverlay = Assert.Single(host.GetVisualDescendants().OfType<Control>().Where(c => c.Name == "MaximisedTabHeader"));
                        capture.CaptureFloatingWindows();
                        DoubleClick(host, firstOverlay);
                        capture.CaptureFloatingWindows();
                        target = FindGestureTarget(host, targetKind, actualPaneId);
                        Assert.Null(view.InspectTabPresentation().MaximisedPaneId);
                        Assert.Same(host, Assert.IsAssignableFrom<Window>(TopLevel.GetTopLevel(Assert.IsAssignableFrom<Control>(floatingPane.Context))));
                    }

                    // Exercise the actual native tab context-menu path before any maximise call.
                    var normalMenu = OpenContextMenu(host, target);
                    var normalSnapshot = InspectMenu(normalMenu);
                    Assert.Equal(ExpectedHeaders, normalSnapshot.Headers);
                    Assert.False(normalSnapshot.MaximiseChecked);
                    Assert.False(normalSnapshot.FloatEnabled);
                    if(paneId != "AssetDocument")
                    {
                        normalMenu.Close();
                        var chrome = target.GetVisualAncestors().OfType<ToolChromeControl>().First();
                        var menuButton = chrome.GetVisualDescendants().OfType<Button>().Single(b=>b.Name=="PART_MenuButton");
                        var iconContextMenu = OpenContextMenu(host,menuButton);
                        Assert.Equal(ExpectedHeaders,InspectMenu(iconContextMenu).Headers);
                        iconContextMenu.Close();
                        var menuPoint = menuButton.TranslatePoint(new Point(menuButton.Bounds.Width/2,menuButton.Bounds.Height/2),host)!.Value;
                        host.MouseDown(menuPoint,MouseButton.Left);host.MouseUp(menuPoint,MouseButton.Left);
                        Dispatcher.UIThread.RunJobs();
                        normalMenu = Assert.IsType<ContextMenu>(chrome.ContextMenu);
                        Assert.True(normalMenu.IsOpen);
                        Assert.Equal(ExpectedHeaders,InspectMenu(normalMenu).Headers);
                    }


                    if(cycle == 2 && paneId != "AssetDocument")
                    {
                        normalMenu.Close();
                        var chrome = target.GetVisualAncestors().OfType<ToolChromeControl>().First();
                        var maximiseButton = chrome.GetVisualDescendants().OfType<Button>().Single(b=>b.Name=="PART_MaximizeRestoreButton");
                        var point = maximiseButton.TranslatePoint(new Point(maximiseButton.Bounds.Width/2,maximiseButton.Bounds.Height/2),host)!.Value;
                        host.MouseDown(point,MouseButton.Left);host.MouseUp(point,MouseButton.Left);
                        Dispatcher.UIThread.RunJobs();
                    }
                    else if (cycle % 2 == 0)
                    {
                        normalMenu.Items.OfType<MenuItem>().Single(item => item.Name == "OasisMaximiseTab")
                            .RaiseEvent(new global::Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent));
                        Dispatcher.UIThread.RunJobs();
                    }
                    else
                    {
                        normalMenu.Close();
                        Dispatcher.UIThread.RunJobs();
                        DoubleClick(host, target);
                    }
                    Assert.Equal(actualPaneId, view.InspectTabPresentation().MaximisedPaneId);
                    Assert.True(view.InspectTabPresentation().WasFloating);
                    Assert.Equal(initialFloatingHostCount + 1, view.FloatingWorkspaceWindows.Count);
                    Assert.Same(host, Assert.IsAssignableFrom<Window>(TopLevel.GetTopLevel(Assert.IsAssignableFrom<Control>(floatingPane.Context))));

                    capture.CaptureFloatingWindows();
                    var overlay = Assert.Single(host.GetVisualDescendants().OfType<Control>().Where(c => c.Name == "MaximisedTabHeader"));
                    RightClick(host, overlay);
                    var maximisedMenu = Assert.IsType<ContextMenu>(overlay.ContextMenu);
                    Assert.True(maximisedMenu.IsOpen);
                    var maximisedSnapshot = InspectMenu(maximisedMenu);
                    Assert.Equal(normalSnapshot.Headers, maximisedSnapshot.Headers);
                    Assert.Equal(ExpectedHeaders, maximisedSnapshot.Headers);
                    Assert.True(maximisedSnapshot.MaximiseChecked);
                    Assert.False(maximisedSnapshot.CloseEnabled);
                    Assert.False(maximisedSnapshot.FloatEnabled);
                    Assert.False(maximisedSnapshot.AddTabEnabled);
                    if (cycle % 2 == 0)
                    {
                        maximisedMenu.Items.OfType<MenuItem>().Single(item => item.Name == "OasisMaximiseTab")
                            .RaiseEvent(new global::Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent));
                        Dispatcher.UIThread.RunJobs();
                    }
                    else
                    {
                        maximisedMenu.Close();
                        Dispatcher.UIThread.RunJobs();
                        DoubleClick(host, overlay);
                    }
                    Assert.Null(view.InspectTabPresentation().MaximisedPaneId);
                    Assert.Equal(initialFloatingHostCount + 1, view.FloatingWorkspaceWindows.Count);
                    Assert.Same(host, Assert.IsAssignableFrom<Window>(TopLevel.GetTopLevel(Assert.IsAssignableFrom<Control>(floatingPane.Context))));
                    Assert.Same(originalOwner, floatingPane.Owner);

                    // Redock with the real factory path, targeting another existing pane.
                    if (paneId == "AssetDocument")
                        await view.DockDocumentAsync("Panel2D", project.PanelPath);
                    else
                        view.MoveWorkspacePane(actualPaneId, "tab", paneId == "Project" ? "Output" : "Project");
                    capture.Capture(1400,900);
                    Assert.Equal(initialFloatingHostCount, view.FloatingWorkspaceWindows.Count);
                    Assert.Same(mainLayout, dockControl.Layout);
                    if(paneId == "AssetDocument") Assert.IsAssignableFrom<IDocumentDock>(floatingPane.Owner);
                    Assert.Same(mainWindow, TopLevel.GetTopLevel(Assert.IsAssignableFrom<Control>(floatingPane.Context)));
                    var retained = view.GetDocumentEditor("Panel2D").CommandSession;
                    Assert.True(retained.IsDirty);
                    Assert.True(retained.CanUndo);
                    Assert.Equal("Retained across tab cycles", Assert.Single(retained.Elements).Name);
                }
            }
            finally
            {
                Directory.Delete(project.Root, recursive: true);
            }
        });
    }

    private static MenuSnapshot InspectMenu(ContextMenu menu)
    {
        var items = menu.Items.Cast<object>().ToArray();
        var visibleSeparators = items.Select((item, index) => (item, index))
            .Where(pair => pair.item is Separator separator && separator.IsVisible)
            .Select(pair => pair.index).ToArray();
        Assert.DoesNotContain(0, visibleSeparators);
        Assert.DoesNotContain(items.Length - 1, visibleSeparators);
        Assert.DoesNotContain(visibleSeparators.Zip(visibleSeparators.Skip(1)), pair => pair.Second == pair.First + 1);

        foreach (var item in items.OfType<MenuItem>().Where(item => item.IsVisible))
        {
            Assert.False(string.IsNullOrWhiteSpace(item.Header?.ToString()), "A visible menu item has a blank header.");
            AssertMenuChildren(item);
        }

        var menuItems = items.OfType<MenuItem>().ToArray();
        var maximise = Assert.Single(menuItems.Where(item => item.Name == "OasisMaximiseTab"));
        var close = Assert.Single(menuItems.Where(item => Equals(item.Header, "Close Tab")));
        var floating = Assert.Single(menuItems.Where(item => Equals(item.Header, "Float")));
        var addTab = Assert.Single(menuItems.Where(item => Equals(item.Header, "Add Tab")));
        Assert.Equal(WorkspaceView.StandardWorkspaceTabs, addTab.Items.OfType<MenuItem>().Select(item => item.Header?.ToString()));
        return new MenuSnapshot(Headers(items), maximise.IsChecked, close.IsEnabled, floating.IsEnabled, addTab.IsEnabled);

        static string[] Headers(IEnumerable<object> source) => source.Select(item => item switch
        {
            MenuItem menuItem => menuItem.Header?.ToString() ?? "<blank>",
            Separator => "<separator>",
            _ => item.GetType().Name
        }).ToArray();

        static void AssertMenuChildren(MenuItem item)
        {
            var children = item.Items.Cast<object>().ToArray();
            for (var i = 0; i < children.Length; i++)
            {
                if (children[i] is MenuItem child && child.IsVisible)
                    Assert.False(string.IsNullOrWhiteSpace(child.Header?.ToString()), "A visible submenu item has a blank header.");
                if (children[i] is Separator separator && separator.IsVisible)
                {
                    Assert.NotEqual(0, i);
                    Assert.NotEqual(children.Length - 1, i);
                    Assert.False(children[i - 1] is Separator previous && previous.IsVisible);
                }
            }
        }
    }

    private static ContextMenu OpenContextMenu(Window window, Control target)
    {
        RightClick(window, target);
        var menu = target.ContextMenu ?? target.GetVisualAncestors().OfType<Control>()
            .Select(control => control.ContextMenu).FirstOrDefault(contextMenu => contextMenu is not null);
        Assert.True(menu is not null, $"Menu missing: target={target.GetType().Name}, bounds={target.Bounds}, host={window.ClientSize}, nativeMenu={(target as DocumentTabStripItem)?.DocumentContextMenu?.IsOpen}, targetParent={target.GetVisualParent()?.GetType().Name}, hit={window.InputHitTest(target.TranslatePoint(new Point(target.Bounds.Width/2,target.Bounds.Height/2),window)!.Value)?.GetType().Name}");
        Assert.True(menu!.IsOpen);
        return menu;
    }

    private static Control FindGestureTarget(Control root, string targetKind, string paneId)
        => targetKind == "ToolChromeGrip"
            ? root.GetVisualDescendants().OfType<Control>().Single(control => control.Name == "PART_Grip"
                && Ancestors(control).OfType<Control>().Any(parent => parent.GetType().Name == "ToolChromeControl"
                    && BelongsToPane(parent, paneId)))
            : root.GetVisualDescendants().OfType<Control>().Single(control =>
                control.GetType().Name == targetKind && BelongsToPane(control, paneId));

    private static IEnumerable<Visual> Ancestors(Visual visual)
    {
        for (var parent = visual.GetVisualParent(); parent is not null; parent = parent.GetVisualParent())
            yield return parent;
    }

    private static bool BelongsToPane(Control control, string paneId)
    {
        var pane = control.DataContext as IDockable;
        if (pane is IDock dock) pane = dock.ActiveDockable;
        return pane?.Id == paneId;
    }

    private static IDockable FindPane(WorkspaceView view, string paneId)
        => WorkspaceLayoutState.LeafMap(view.GetVisualDescendants().OfType<DockControl>().Single().Layout!).Values
            .Single(item => item.Id == paneId || (paneId == "AssetDocument" && item is Document document && document.Context is WorkspaceView));

    private static void DoubleClick(Window window, Control target)
    {
        var point = target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), window);
        Assert.True(point.HasValue);
        for (var index = 0; index < 2; index++)
        {
            window.MouseDown(point!.Value, MouseButton.Left);
            window.MouseUp(point.Value, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static void RightClick(Window window, Control target)
    {
        var point = target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), window);
        Assert.True(point.HasValue);
        window.MouseDown(point!.Value, MouseButton.Right);
        window.MouseUp(point.Value, MouseButton.Right);
        Dispatcher.UIThread.RunJobs();
    }

    private static TestProject CreateProject()
    {
        var root = Path.Combine(Path.GetTempPath(), "OasisDockTabCycle-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var session = new AuthoringSession(root);
        session.CreateProject("Dock cycle");
        var panelPath = session.CreatePanel("Panel");
        session.Save();
        return new TestProject(root, session, panelPath);
    }

    private sealed record MenuSnapshot(string[] Headers, bool MaximiseChecked, bool CloseEnabled, bool FloatEnabled, bool AddTabEnabled);
    private sealed record TestProject(string Root, AuthoringSession Session, string PanelPath);
}
