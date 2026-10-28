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
public sealed class MaximisedPaneFocusRegressionTests
{
    [Theory]
    [InlineData("Hierarchy", "RightmostDocument", false)]
    [InlineData("Inspector", "RightmostDocument", false)]
    [InlineData("Hierarchy", "RightmostDocument", true)]
    [InlineData("Inspector", "RightmostDocument", true)]
    [InlineData("Hierarchy", "Output", false)]
    [InlineData("Inspector", "Output", false)]
    [InlineData("Hierarchy", "Output", true)]
    [InlineData("Inspector", "Output", true)]
    public async Task RestoringMaximisedPaneKeepsFocusWhenAnotherGroupHasASelectedNonfirstTab(string paneId, string competingGroup, bool reordered)
    {
        await HeadlessTestHost.RunAsync(async () =>
        {
            var project = CreateProject();
            try
            {
                using var capture = new WorkspaceCapture(project.Session);
                var view = capture.View;
                await view.OpenNativeAssetAsync("Panel2D", project.PanelPath);
                await view.OpenNativeAssetAsync("Machine", project.MachinePath);
                await view.SynchronizeAssetDocumentsAsync();
                Assert.NotEmpty(capture.Capture());

                var layout = Assert.IsAssignableFrom<IRootDock>(view.GetVisualDescendants().OfType<DockControl>().Single().Layout);
                var leaves = WorkspaceLayoutState.LeafMap(layout);
                var pane = leaves[paneId];
                IDock competingOwner;
                IDockable selectedTab;

                if (competingGroup == "RightmostDocument")
                {
                    var documents = leaves.Values.OfType<Document>().Where(document => document.Context is WorkspaceView).ToArray();
                    Assert.Equal(2, documents.Length);
                    var panelDocument = documents.Single(document => document.Title.Contains("Panel", StringComparison.Ordinal));
                    var machineDocument = documents.Single(document => document.Title.Contains("Machine", StringComparison.Ordinal));
                    competingOwner = Assert.IsAssignableFrom<IDock>(machineDocument.Owner);
                    Assert.Same(competingOwner, panelDocument.Owner);
                    if (reordered) SwapFirstTwoTabs(competingOwner);
                    selectedTab = competingOwner.VisibleDockables![^1];
                    // Select the rightmost actual tab; swapping the collection exercises a reordered group too.
                    (view.GetVisualDescendants().OfType<DockControl>().Single().Factory ?? layout.Factory)!.SetActiveDockable(selectedTab);
                }
                else
                {
                    view.MoveWorkspacePane("Output", "tab", "Project");
                    leaves = WorkspaceLayoutState.LeafMap(layout);
                    var outputTab = leaves["Output"];
                    competingOwner = Assert.IsAssignableFrom<IDock>(outputTab.Owner);
                    Assert.Same(competingOwner, leaves["Project"].Owner);
                    if (reordered) SwapFirstTwoTabs(competingOwner);
                    selectedTab = competingOwner.VisibleDockables![^1];
                    var panelDocument = leaves.Values.OfType<Document>().Single(document => document.Title.Contains("Panel", StringComparison.Ordinal));
                    (view.GetVisualDescendants().OfType<DockControl>().Single().Factory ?? layout.Factory)!.SetActiveDockable(panelDocument);
                    (view.GetVisualDescendants().OfType<DockControl>().Single().Factory ?? layout.Factory)!.SetActiveDockable(selectedTab);
                    Assert.Same(selectedTab, competingOwner.ActiveDockable);
                }

                Assert.NotSame(competingOwner, pane.Owner);
                Assert.Equal(1, competingOwner.VisibleDockables!.IndexOf(selectedTab));
                RenderAndDrain(view, capture);
                Assert.Same(selectedTab, competingOwner.ActiveDockable);

                view.ToggleTabMaximised(pane.Id!);
                RenderAndDrain(view, capture);
                view.RestoreMaximisedTab();
                // Restore can schedule dock and visual-tree updates; process several deterministic headless frames.
                for (var frame = 0; frame < 4; frame++) RenderAndDrain(view, capture);

                var owner = Assert.IsAssignableFrom<IDock>(pane.Owner);
                Assert.Null(view.InspectTabPresentation().MaximisedPaneId);
                Assert.Same(pane, layout.FocusedDockable);
                Assert.True(owner.IsActive);
                Assert.Same(selectedTab, competingOwner.ActiveDockable);
                Assert.Same(pane, WorkspaceLayoutState.LeafMap(layout)[pane.Id!]);
            }
            finally { Directory.Delete(project.Root, recursive: true); }
        });
    }

    private static void RenderAndDrain(WorkspaceView view, WorkspaceCapture capture)
    {
        view.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        Assert.NotEmpty(capture.Capture());
        Dispatcher.UIThread.RunJobs();
    }

    private static void SwapFirstTwoTabs(IDock owner)
    {
        var tabs = owner.VisibleDockables!;
        Assert.Equal(2, tabs.Count);
        (tabs[0], tabs[1]) = (tabs[1], tabs[0]);
    }

    private static (string Root, AuthoringSession Session, string PanelPath, string MachinePath) CreateProject()
    {
        var root = Path.Combine(Path.GetTempPath(), "OasisMaximisedFocus-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var session = new AuthoringSession(root);
        session.CreateProject("Maximised focus");
        var panelPath = session.CreatePanel("Panel");
        session.Save();
        var machines = new MachineAuthoringSession(session);
        var machinePath = machines.CreateMachine("Machine");
        return (root, session, panelPath, machinePath);
    }
}
