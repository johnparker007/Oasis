using Avalonia.Threading;
using Dock.Model.Core;
using Dock.Model.Controls;

namespace OasisEditor.Avalonia;

public sealed partial class WorkspaceView
{
    public static IReadOnlyList<string> StandardWorkspaceTabs { get; }=Array.AsReadOnly(new[]{"Hierarchy","Inspector","Project","Output"});

    private int _tabFocusRequestId;

    private void FocusWorkspaceTab(IDockable pane)
    {
        if(pane.Owner is not IDock owner || owner.VisibleDockables?.Contains(pane)!=true)return;
        ++_tabFocusRequestId;
        _factory.SetActiveDockable(pane);
        _factory.SetFocusedDockable(owner,pane);
        FocusNativeDockable(pane);
    }
    private void FocusRestoredTab(IDockable pane)
    {
        FocusWorkspaceTab(pane);
        // Deferred Dock presenters may select a different group as they reattach.
        // Reapply focus after that pass unless a newer tab action supersedes it.
        var requestId=_tabFocusRequestId;
        Dispatcher.UIThread.Post(()=>
        {
            if(requestId!=_tabFocusRequestId || _disposed || _maximisedTab is not null || _dock.Layout is null)return;
            if(WorkspaceLayoutState.LeafMap(_dock.Layout).TryGetValue(pane.Id,out var current) && ReferenceEquals(current,pane))FocusWorkspaceTab(pane);
        },DispatcherPriority.Loaded);
    }

    /// <summary>Reopen or move a singleton standard pane into the selected tab group.</summary>
    public void AddWorkspaceTab(string paneId,string targetPaneId)
    {
        if(_documentOwner is {} host){host.AddWorkspaceTab(paneId,targetPaneId);return;}
        if(!StandardWorkspaceTabs.Contains(paneId,StringComparer.Ordinal))throw new ArgumentException("Choose a standard workspace tab.",nameof(paneId));
        if(_maximisedTab is not null)throw new InvalidOperationException("Restore the maximised tab before adding a tab.");
        var pane=FindWorkspaceTab(paneId);var target=FindWorkspaceTab(targetPaneId);
        if(target.Owner is not IDock targetDock || targetDock.VisibleDockables?.Contains(target)!=true)throw new InvalidOperationException("Choose a visible target tab.");
        if(ReferenceEquals(pane,target)){FocusWorkspaceTab(pane);return;}
        _factory.RestoreDockable(pane);
        if(pane.Owner is IDock sourceDock){if(!ReferenceEquals(sourceDock,targetDock))_factory.MoveDockable(sourceDock,targetDock,pane,target);}
        else{_factory.AddDockable(targetDock,pane);_factory.InitDockable(pane,targetDock);}
        FocusWorkspaceTab(pane);CloseEmptyNativeHosts();StateChanged?.Invoke();
    }
    private void RetainClosedStandardPane(IDockable pane)
    {
        if(!StandardWorkspaceTabs.Contains(pane.Id,StringComparer.Ordinal) || _dock.Layout is not IRootDock root)return;
        if(pane.Owner is not IRootDock hiddenRoot || ReferenceEquals(hiddenRoot,root) || hiddenRoot.HiddenDockables?.Contains(pane)!=true)return;
        hiddenRoot.HiddenDockables.Remove(pane);
        root.HiddenDockables??=_factory.CreateList<IDockable>();root.HiddenDockables.Add(pane);
        pane.Owner=root;pane.OriginalOwner=null;
        CloseEmptyNativeHosts();
    }
    private IEnumerable<IDockable> MainVisibleTabs(IDockable node)
    {
        if(node is ITool or IDocument){yield return node;yield break;}
        if(node is IDock dock)foreach(var child in dock.VisibleDockables??[])
            foreach(var pane in MainVisibleTabs(child))yield return pane;
    }
    private void ReopenStandardPane(IDockable pane)
    {
        // Saved hidden panes have no live OriginalOwner; give them a visible group.
        if(pane.Owner is IRootDock root && root.HiddenDockables?.Contains(pane)==true && pane.OriginalOwner is null)
        {
            if(MainVisibleTabs(_dock.Layout!).FirstOrDefault() is {} target){AddWorkspaceTab(pane.Id,target.Id);return;}
            _factory.RestoreDockable(pane);
            var dock=_mainAssetDock??throw new InvalidOperationException("There is no available tab group.");
            _factory.AddDockable(dock,pane);_factory.InitDockable(pane,dock);
        }
        else _factory.RestoreDockable(pane);
        FocusWorkspaceTab(pane);
    }

}
