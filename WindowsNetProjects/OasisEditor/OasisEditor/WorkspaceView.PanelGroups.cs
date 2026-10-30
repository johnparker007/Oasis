using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace OasisEditor.Avalonia;

public sealed partial class WorkspaceView
{
    private static readonly DataFormat<WorkspaceView> GroupDragFormat=DataFormat.CreateInProcessFormat<WorkspaceView>("Oasis.PanelHierarchy");
    private readonly HashSet<string> _expandedPanelGroups = new(StringComparer.Ordinal);
    public IReadOnlyList<string> ExpandedPanelGroups => _session.Groups.Where(g=>_expandedPanelGroups.Contains(g.Id)).Select(g=>g.Id).ToArray();
    public IReadOnlyList<ElementSnapshot> InspectPanelHierarchyRows() => _rows.ToArray();
    private void CreatePanelGroupFromUi(bool selection=true,string? parentId=null)
    {var id=_session.CreateGroup(parentId:parentId,groupSelection:selection);SetPanelGroupExpanded(id,true);}
    public void SetPanelGroupExpanded(string id,bool expanded)
    {
        if(!_session.Groups.Any(g=>g.Id==id))throw new KeyNotFoundException("Group not found.");
        if(expanded)_expandedPanelGroups.Add(id);else _expandedPanelGroups.Remove(id);Refresh();
    }
    private IReadOnlyList<ElementSnapshot> VisibleHierarchyRows()
    {
        if(_faceActive)return ActiveElements;
        var groups=_session.Groups.ToDictionary(g=>g.Id);
        return _session.HierarchyElements.Where(e=>
        {for(var parent=e.ParentId;parent is not null;parent=groups[parent].ParentId)if(!_expandedPanelGroups.Contains(parent))return false;return true;}).ToArray();
    }
    private Control PanelHierarchyRow(ElementSnapshot? e)
    {
        if(e is null)return new TextBlock();
        var group=e.Kind=="group";var kind=group&&Enum.TryParse<EditorIconKind>(e.Icon,out var icon)?icon:EditorIcons.ForComponent(e.Kind);
        var label=kind==EditorIconKind.None?$"◇  {e.Name}":e.Name;
        var row=new StackPanel {Tag=e.Id,Orientation=Orientation.Horizontal,Spacing=2,Background=Brushes.Transparent};
        row.Children.Add(HierarchyStateButton(row,e,visibility:true));
        row.Children.Add(HierarchyStateButton(row,e,visibility:false));
        row.Children.Add(new Border {Width=e.Depth*16});
        if(e.Locked||e.AncestorsLocked)
        {
            var hint=e.Locked?"Transform locked":"Transform locked by parent group";
            ToolTip.SetTip(row,hint);
            global::Avalonia.Automation.AutomationProperties.SetHelpText(row,hint);
        }
        if(group)
        {
            var toggle=new Button {Name="TogglePanelGroup",Width=16,Height=20,MinHeight=0,MinWidth=0,Padding=new Thickness(2),Focusable=false,
                Content=new PathIcon {Width=8,Height=8,Data=Geometry.Parse(_expandedPanelGroups.Contains(e.Id)?"M0 1 L8 1 L4 6 Z":"M1 0 L6 4 L1 8 Z")}};
            global::Avalonia.Automation.AutomationProperties.SetName(toggle,(_expandedPanelGroups.Contains(e.Id)?"Collapse ":"Expand ")+e.Name);
            toggle.Click+=(_,_)=>SetPanelGroupExpanded(e.Id,!_expandedPanelGroups.Contains(e.Id));row.Children.Add(toggle);
        }
        else row.Children.Add(new Border {Width=16});
        row.Children.Add(new ContentControl {Name="HierarchyLabel",Content=EditorIcons.Label(kind,label),Opacity=e.Visible&&e.AncestorsVisible?1:.45});
        if(group && e.InputLabel is {} inputLabel)
        {
            var badge=new EditorIcon {Kind=EditorIconKind.Button,Margin=new Thickness(5,0,0,0)};
            ToolTip.SetTip(badge,"Input: "+inputLabel);row.Children.Add(badge);
        }
        var menu=new ContextMenu();row.ContextMenu=menu;
        void PopulateMenu()
        {
            menu.Items.Clear();
            void Item(string text,Action action,bool enabled=true){var item=new MenuItem {Header=text,IsEnabled=enabled};item.Click+=(_,_)=>Try(action);menu.Items.Add(item);}
            Item("Rename",()=>BeginHierarchyRename(e.Id));
            ((MenuItem)menu.Items[0]!).InputGesture = new KeyGesture(Key.F2);
            if(_faceActive)return;
            Item("Create Group",()=>CreatePanelGroupFromUi(false,group?e.Id:e.ParentId),!_session.Groups.Any(g=>g.Id==e.Id&&g.Control is not null));
            Item("Group Selection",()=>CreatePanelGroupFromUi());Item("Ungroup",_session.UngroupSelection,group&&!_session.Groups.Any(g=>g.Id==e.Id&&g.Control is not null));
            var move=new MenuItem {Header="Move to Group"};
            var root=new MenuItem {Header="Panel root"};root.Click+=(_,_)=>Try(()=>_session.ReparentSelection(null));move.Items.Add(root);
            foreach(var g in _session.Groups)
            {var target=new MenuItem {Header=_session.GroupLabel(g.Id).DisplayName,IsEnabled=!_session.SelectionContainsDescendant(g.Id)};target.Click+=(_,_)=>Try(()=>_session.ReparentSelection(g.Id));move.Items.Add(target);}
            menu.Items.Add(move);menu.Items.Add(new Separator());
            Item("Move Earlier",()=>_session.MoveSelectionInOrder(false));Item("Move Later",()=>_session.MoveSelectionInOrder(true));
            Item("Duplicate",()=>_session.DuplicateSelected());Item("Delete",()=>_session.DeleteSelected());
        }
        PopulateMenu();
        menu.Opening+=(_,_)=>{if(!SelectedIds.Contains(e.Id))SelectActive([e.Id]);PopulateMenu();};
        if(_faceActive)return row;
        Point? pressed=null;PointerPressedEventArgs? pressArgs=null;
        row.PointerPressed+=(_,args)=>{if(_hierarchyRename is null&&args.GetCurrentPoint(row).Properties.IsLeftButtonPressed&&
            args.Source is Visual source&&!source.GetVisualAncestors().Prepend(source).Any(v=>v is Button or TextBox))
            {pressed=args.GetPosition(row);pressArgs=args;}};
        row.PointerReleased+=(_,_)=>pressed=null;
        row.PointerMoved+=async(_,args)=>
        {
            if(pressed is not {} start||!args.GetCurrentPoint(row).Properties.IsLeftButtonPressed||Math.Abs(args.GetPosition(row).X-start.X)+Math.Abs(args.GetPosition(row).Y-start.Y)<5)return;
            var dragArgs=pressArgs!;pressed=null;pressArgs=null;if(!SelectedIds.Contains(e.Id))_session.Select(e.Id);
            var item=new DataTransferItem();item.Set(GroupDragFormat,this);using var data=new DataTransfer();data.Add(item);
            try {await DragDrop.DoDragDropAsync(dragArgs,data,DragDropEffects.Move);}
            catch(Exception error) when(error is InvalidOperationException or NotSupportedException){SetStatus(error.Message);}
        };
        DragDrop.SetAllowDrop(row,true);
        row.AddHandler(DragDrop.DragOverEvent,(_,args)=>{args.DragEffects=ReferenceEquals(args.DataTransfer.TryGetValue(GroupDragFormat),this)?DragDropEffects.Move:DragDropEffects.None;args.Handled=true;});
        row.AddHandler(DragDrop.DropEvent,(_,args)=>
        {
            if(!ReferenceEquals(args.DataTransfer.TryGetValue(GroupDragFormat),this))return;
            args.Handled=true;var y=args.GetPosition(row).Y;
            Try(()=>{if(group&&y>row.Bounds.Height*.25&&y<row.Bounds.Height*.75)_session.ReparentSelection(e.Id);else _session.ReorderSelection(e.Id,y>=row.Bounds.Height*.5);});
        });
        return row;
    }
    private void AppendGroupInspector(string id)
    {
        var g=_session.Groups.Single(g=>g.Id==id);var parts=g.Transform.Decompose();var presentation=_session.GroupLabel(id);Heading(g.Control is null?"GROUP":"LAMP CONTROL");
        string Display(double value)=>value.ToString("G12",System.Globalization.CultureInfo.InvariantCulture);
        Field("Name",g.AutomaticName?"":g.Name);_fields["Name"].PlaceholderText=presentation.Type;
        ToolTip.SetTip(_fields["Name"],"Leave blank for a live automatic name. Type a name to override it.");
        _inspector.Children.Add(ActionButton("Reset to Automatic",()=>Try(()=>_session.UpdateGroup(id,automaticName:true))));Field("X",Display(g.Transform.X));Field("Y",Display(g.Transform.Y));Field("Rotation",Display(parts.Rotation));Field("Scale X",Display(parts.ScaleX));Field("Scale Y",Display(parts.ScaleY));
        var icons=new ComboBox {Name="PanelGroupIcon",ItemsSource=new[]{"Automatic"}.Concat(PanelHierarchy.Icons).ToArray(),SelectedItem=g.AutomaticIcon?"Automatic":g.Icon,
            ItemTemplate=new global::Avalonia.Controls.Templates.FuncDataTemplate<string>((value,_)=>EditorIcons.Label(value=="Automatic"?EditorIconKind.Hierarchy:Enum.Parse<EditorIconKind>(value!),value))};
        _inspector.Children.Add(new TextBlock {Text="Icon"});_inspector.Children.Add(icons);
        var visible=new CheckBox {Content="Visible",IsChecked=g.Visible};var locked=new CheckBox {Content="Lock transform",IsChecked=g.Locked};
        _inspector.Children.Add(visible);_inspector.Children.Add(locked);
        BindInspectorEdits(() => _session.UpdateGroup(id,string.IsNullOrWhiteSpace(_fields["Name"].Text)?null:_fields["Name"].Text,Number("X"),Number("Y"),Number("Rotation"),Number("Scale X"),Number("Scale Y"),icons.SelectedItem as string=="Automatic"?null:icons.SelectedItem as string,visible.IsChecked==true,locked.IsChecked==true,automaticName:string.IsNullOrWhiteSpace(_fields["Name"].Text),automaticIcon:Equals(icons.SelectedItem,"Automatic")));
        if(g.Control is null)_inspector.Children.Add(ActionButton("Ungroup",()=>Try(_session.UngroupSelection)));
        else AppendControlInspector(g);
    }
}
