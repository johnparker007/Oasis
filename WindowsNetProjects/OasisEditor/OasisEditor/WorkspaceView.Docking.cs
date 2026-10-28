using Avalonia.Threading;
using Dock.Model.Core;

namespace OasisEditor.Avalonia;

public sealed partial class WorkspaceView
{
    /// <summary>Use the same live Dock factory operations as pane title-bar commands.</summary>
    public WorkspaceState MoveWorkspacePane(string paneId, string placement, string targetId = "Authoring")
    {
        RestoreMaximisedTab();
        var panes = WorkspaceLayoutState.LeafMap(_dock.Layout ?? throw new InvalidOperationException("The workspace has no layout."));
        if (!panes.TryGetValue(paneId, out var pane)) throw new ArgumentException("Unknown workspace pane.", nameof(paneId));
        if (placement == "float") _factory.FloatDockable(pane);
        else if (placement == "tab")
        {
            if (!panes.TryGetValue(targetId, out var target) || ReferenceEquals(pane, target))
                throw new ArgumentException("Choose a different target pane.", nameof(targetId));
            if (pane.Owner is not IDock sourceDock || target.Owner is not IDock targetDock)
                throw new InvalidOperationException("The pane is not owned by a dock.");
            _factory.MoveDockable(sourceDock, targetDock, pane, target);
            _factory.SetActiveDockable(pane);
        }
        else throw new ArgumentException("Use float or tab.", nameof(placement));
        UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        return InspectWorkspaceState();
    }
}
