using Avalonia;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.VisualTree;
using Dock.Avalonia.Controls;
using Dock.Model.Core;
using Dock.Model.Controls;

namespace OasisEditor.Avalonia;

public sealed record WorkspaceTabPresentation(string? MaximisedPaneId, bool WasFloating);

public sealed partial class WorkspaceView
{
    private sealed record MaximisedTab(IDockable Pane, Control Surface, Grid? MainGrid,
        Window? FloatingHost, object? OriginalContent, IDataTemplate? OriginalTemplate, WindowState WindowState, PixelPoint Position, Size Size, NativeAssetEditor? FocusedAsset);
    private MaximisedTab? _maximisedTab;
    private readonly HashSet<Window> _tabGestureHosts=[];
    private static readonly ConditionalWeakTable<Window,WorkspaceView> TabHosts=new();
    // DoubleTapped bubbles only. A class handler runs before Dock's instance
    // handler (which would otherwise float the tab on the same double click).
    private static readonly IDisposable TabDoubleTapHandler=InputElement.DoubleTappedEvent.AddClassHandler<Control>((control,e)=>
    {
        if(control is not (DocumentTabStripItem or ToolTabStripItem or ToolChromeControl))return;
        if(TopLevel.GetTopLevel(control) is Window window && TabHosts.TryGetValue(window,out var workspace))workspace.TabDoubleTapped(control,e);
    });

    private static readonly IDisposable TabContextHandler=Control.ContextRequestedEvent.AddClassHandler<Control>((control,e)=>
    {
        if(TabAt(e.Source) is not {} tab || TopLevel.GetTopLevel(tab.Header) is not Window window)return;
        if(TabHosts.TryGetValue(window,out var workspace))workspace.TabContextRequested(control,e);
    });

    private void InitializeTabPresentation()
    {
        AttachedToVisualTree+=(_,_)=>{if(TopLevel.GetTopLevel(this) is Window host)AttachTabGestures(host);};
        _factory.WindowOpened+=(_,e)=>{if(e.Window.Host is Window host)AttachTabGestures(host);};
        _factory.WindowClosed+=(_,e)=>{if(e.Window.Host is Window host)DetachTabGestures(host);};
        _factory.DockableClosing+=(_,e)=>{if(ReferenceEquals(_maximisedTab?.Pane,e.Dockable))e.Cancel=true;};
    }
    private void AttachTabGestures(Window host)
    {
        if(!_tabGestureHosts.Add(host))return;
        TabHosts.Add(host,this);
        host.AddHandler(PointerReleasedEvent,TabPointerReleased,RoutingStrategies.Tunnel);
        host.Closing+=MaximisedHostClosing;
        host.Closed+=(_,_)=>DetachTabGestures(host);
    }
    private void DetachTabGestures(Window host)
    {
        if(!_tabGestureHosts.Remove(host))return;
        TabHosts.Remove(host);
        host.Closing-=MaximisedHostClosing;
        host.RemoveHandler(PointerReleasedEvent,TabPointerReleased);
    }
    private void MaximisedHostClosing(object? sender,global::Avalonia.Controls.WindowClosingEventArgs e)
    {
        if(_maximisedTab?.FloatingHost is not {} host || !ReferenceEquals(sender,host))return;
        // Cancel before HostWindow asks Dock to close its content. Factory
        // WindowClosing runs after that destructive OnClose callback.
        e.Cancel=true;
    }
    private static (IDockable Pane,Control Header)? TabAt(object? source)
    {
        var inGrip=false;
        for(var visual=source as Visual;visual is not null;visual=visual.GetVisualParent())
        {
            if(visual is Button)return null;
            if(visual is not Control control)continue;
            if(control.Name=="PART_Grip")inGrip=true;
            if(control is ToolChromeControl && !inGrip)return null;
            if(control is not (DocumentTabStripItem or ToolTabStripItem or ToolChromeControl))continue;
            var pane=control.DataContext as IDockable;
            if(pane is IDock dock)pane=dock.ActiveDockable;
            if(pane is not null)return(pane,control);
        }
        return null;
    }
    private void TabDoubleTapped(object? sender,TappedEventArgs e)
    {
        if(TabAt(e.Source) is not {} tab)return;
        e.Handled=true;Try(()=>ToggleTabMaximised(tab.Pane.Id));
    }
    private void TabPointerReleased(object? sender,PointerReleasedEventArgs e)
    {
        if(e.InitialPressMouseButton!=MouseButton.Right || TabAt(e.Source) is not {} tab)return;
        e.Handled=true;OpenTabMenu(tab.Pane,tab.Header);
    }
    private void TabContextRequested(object? sender,ContextRequestedEventArgs e)
    {
        if(TabAt(e.Source) is not {} tab)return;
        e.Handled=true;OpenTabMenu(tab.Pane,tab.Header);
    }
    private void OpenTabMenu(IDockable pane,Control header)
    {
        var native=header switch
        {
            DocumentTabStripItem document=>document.DocumentContextMenu,
            ToolTabStripItem tool=>tool.TabContextMenu,
            _=>null
        };
        var menu=CreateTabContextMenu(pane.Id);
        if(native is not null)
        {
            native.DataContext=pane;
            // Dock shares these template resources; copy the actions into a fresh
            // popup rather than reparenting or mutating its stateful menu controls.
            foreach(var item in native.Items)
            {
                if(item is MenuItem action && action.Header?.ToString() is "Close" or "Close Tab" or "Float")continue;
                if(item is Control control)menu.Items.Add(CloneTabMenuItem(control));
            }
        }
        header.ContextMenu=menu;
        menu.Open(header);
    }
    private static Control CloneTabMenuItem(Control item)
    {
        if(item is not MenuItem original)return new Separator{IsVisible=item.IsVisible};
        var copy=new MenuItem{Header=original.Header,Command=original.Command,CommandParameter=original.CommandParameter,
            IsEnabled=original.IsEnabled,IsVisible=original.IsVisible,ToggleType=original.ToggleType,IsChecked=original.IsChecked};
        foreach(var child in original.Items.OfType<Control>())copy.Items.Add(CloneTabMenuItem(child));
        return copy;
    }
    public WorkspaceTabPresentation InspectTabPresentation()=>_documentOwner?.InspectTabPresentation()
        ??new(_maximisedTab?.Pane.Id,_maximisedTab?.FloatingHost is not null);

    public ContextMenu CreateTabContextMenu(string paneId)
    {
        if(_documentOwner is {} owner)return owner.CreateTabContextMenu(paneId);
        var pane=FindWorkspaceTab(paneId);
        var maximised=ReferenceEquals(_maximisedTab?.Pane,pane);
        var menu=new ContextMenu();
        var maximise=new MenuItem{Name="OasisMaximiseTab",Header="Maximise",ToggleType=MenuItemToggleType.CheckBox,IsChecked=maximised};
        maximise.Click+=(_,_)=>{menu.Close();Try(()=>ToggleTabMaximised(paneId));};
        var close=new MenuItem{Header="Close Tab",IsEnabled=!maximised && pane.CanClose};
        close.Click+=(_,_)=>Try(()=>_factory.CloseDockable(pane));
        var floating=new MenuItem{Header="Float",IsEnabled=!maximised && pane.CanFloat};
        floating.Click+=(_,_)=>Try(()=>{RestoreMaximisedTab();_factory.FloatDockable(pane);});
        menu.Items.Add(maximise);menu.Items.Add(new Separator());menu.Items.Add(close);menu.Items.Add(floating);
        return menu;
    }
    private IDockable FindWorkspaceTab(string paneId)
        =>WorkspaceLayoutState.LeafMap(_dock.Layout??throw new InvalidOperationException("Workspace has no layout."))
            .GetValueOrDefault(paneId)??throw new ArgumentException("Unknown workspace tab.",nameof(paneId));

    public WorkspaceTabPresentation ToggleTabMaximised(string paneId)
    {
        if(_documentOwner is {} owner)return owner.ToggleTabMaximised(paneId);
        if(_nativeLayoutOperation || !_nativeRestoreTask.IsCompleted)throw new InvalidOperationException("Wait for workspace restoration to finish.");
        var pane=FindWorkspaceTab(paneId);
        if(ReferenceEquals(_maximisedTab?.Pane,pane)){RestoreMaximisedTab();return InspectTabPresentation();}
        RestoreMaximisedTab();
        var previousFocus=_focusedAsset;
        FocusNativeDockable(pane);
        var floating=(_dock.Layout as IRootDock)?.Windows?.FirstOrDefault(w=>w.Layout is not null && WorkspaceLayoutState.LeafMap(w.Layout).ContainsKey(paneId))?.Host as Window;
        var surface=new Grid{RowDefinitions=new RowDefinitions("Auto,*")};
        surface.Bind(BackgroundProperty,this.GetResourceObservable("WorkspaceBrush"));
        var title=new TextBlock{Margin=new Thickness(9,5),VerticalAlignment=VerticalAlignment.Center};
        title.Bind(TextBlock.TextProperty,new Binding(nameof(IDockable.Title)){Source=pane});
        var header=new Border{Child=title,Name="MaximisedTabHeader"};
        header.Bind(BackgroundProperty,this.GetResourceObservable("ToolbarBrush"));
        header.DoubleTapped+=(_,e)=>{e.Handled=true;RestoreMaximisedTab();};
        header.ContextRequested+=(_,e)=>{e.Handled=true;header.ContextMenu=CreateTabContextMenu(paneId);header.ContextMenu.Open(header);};
        ToolTip.SetTip(header,"Double-click to restore tab");surface.Children.Add(header);
        var presenter=new DockPanePresenter();
        presenter.Bind(DockPanePresenter.PaneContextProperty,new Binding(nameof(IDockable.Context)){Source=pane});
        Grid.SetRow(presenter,1);surface.Children.Add(presenter);
        if(floating is not null)
        {
            AttachTabGestures(floating);
            _maximisedTab=new(pane,surface,null,floating,floating.Content,floating.ContentTemplate,floating.WindowState,floating.Position,floating.ClientSize,previousFocus);
            floating.Content=null;floating.Presenter?.UpdateChild();
            floating.ContentTemplate=null;floating.Content=surface;floating.WindowState=WindowState.Maximized;
        }
        else
        {
            var grid=(Grid)_dock.Parent!;
            _maximisedTab=new(pane,surface,grid,null,null,null,default,default,default,previousFocus);
            grid.Children.Remove(_dock);Grid.SetRow(surface,2);grid.Children.Add(surface);
        }
        StateChanged?.Invoke();return InspectTabPresentation();
    }
    public void RestoreMaximisedTab()
    {
        if(_documentOwner is {} owner){owner.RestoreMaximisedTab();return;}
        if(_maximisedTab is not {} saved)return;
        _maximisedTab=null;
        if(saved.FloatingHost is {} host)
        {
            host.Content=null;host.Presenter?.UpdateChild();host.ContentTemplate=saved.OriginalTemplate;host.Content=saved.OriginalContent;
            host.WindowState=saved.WindowState;
            if(saved.WindowState==WindowState.Normal){host.Position=saved.Position;host.Width=saved.Size.Width;host.Height=saved.Size.Height;}
        }
        else if(saved.MainGrid is {} grid)
        {grid.Children.Remove(saved.Surface);grid.Children.Add(_dock);}
        if(saved.FocusedAsset is {} focused && _nativeDocuments.ContainsKey(focused.Asset.Id))FocusNativeEditor(focused);
        StateChanged?.Invoke();
    }
    private WorkspaceDockNode CapturePresentedLayout()
    {
        var layout=WorkspaceLayoutState.Capture(_dock.Layout!);
        if(_maximisedTab is not {FloatingHost:{} host} saved || _dock.Layout is not IRootDock root || layout.Windows is null)return layout;
        var windows=layout.Windows.ToArray();
        for(var i=0;i<windows.Length;i++)
            if(ReferenceEquals(root.Windows?[i].Host,host))windows[i]=windows[i] with{Bounds=new(saved.Position.X,saved.Position.Y,saved.Size.Width,saved.Size.Height,saved.WindowState.ToString())};
        return layout with{Windows=windows};
    }
    private void DisposeTabPresentation()
    {RestoreMaximisedTab();foreach(var host in _tabGestureHosts.ToArray())DetachTabGestures(host);}
}
