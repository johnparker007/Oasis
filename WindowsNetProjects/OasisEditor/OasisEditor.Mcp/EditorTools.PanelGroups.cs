using System.ComponentModel;
using ModelContextProtocol.Server;

namespace OasisEditor.Avalonia.Mcp;

public sealed partial class EditorTools
{
    [McpServerTool(Name="create_panel_group"),Description("Create a real Panel2D group. Group the current selection atomically by default; preserves world transforms. Hierarchy order controls drawing. Empty groups can specify a parentId.")]
    public Task<object> CreatePanelGroup(CancellationToken cancellationToken,string name="Group",string? parentId=null,bool groupSelection=true,string icon="Hierarchy")
        =>ui.Invoke(()=>Act(()=>session.CreateGroup(name,parentId,groupSelection,icon)),cancellationToken);
    [McpServerTool(Name="update_panel_group"),Description("Undoable name, local position/rotation/scale, built-in icon, visibility or lock edit. Transform changes affect descendants. Zero scales and locked geometry are rejected.")]
    public Task<object> UpdatePanelGroup(string id,CancellationToken cancellationToken,string? name=null,double? x=null,double? y=null,double? rotation=null,double? scaleX=null,double? scaleY=null,string? icon=null,bool? visible=null,bool? locked=null)
        =>ui.Invoke(()=>Act(()=>session.UpdateGroup(id,name,x,y,rotation,scaleX,scaleY,icon,visible,locked)),cancellationToken);
    [McpServerTool(Name="reparent_panel_selection"),Description("Move the current selection into a group, or the panel root when parentId is null. Preserves world transforms including nested nonuniform scaling; drawing follows the new hierarchy order.")]
    public Task<object> ReparentPanelSelection(CancellationToken cancellationToken,string? parentId=null)
        =>ui.Invoke(()=>Act(()=>session.ReparentSelection(parentId)),cancellationToken);
    [McpServerTool(Name="reorder_panel_selection"),Description("Place selected objects before/after a target sibling, adopting the target's parent and preserving world transforms. Changes hierarchy and draw order as one undo command.")]
    public Task<object> ReorderPanelSelection(string targetId,CancellationToken cancellationToken,bool after=false)
        =>ui.Invoke(()=>Act(()=>session.ReorderSelection(targetId,after)),cancellationToken);
    [McpServerTool(Name="ungroup_panel_selection"),Description("Dissolve selected groups, retaining children, world transforms and inherited state in their hierarchy positions. Undoable.")]
    public Task<object> UngroupPanelSelection(CancellationToken cancellationToken)
        =>ui.Invoke(()=>Act(session.UngroupSelection),cancellationToken);
    [McpServerTool(Name="set_panel_group_expanded"),Description("Expand/collapse a Panel2D hierarchy group. Workspace presentation only; does not dirty the asset or change undo history.")]
    public Task<object> SetPanelGroupExpanded(string id,bool expanded,CancellationToken cancellationToken)
        =>ui.Invoke<object>(()=>{capture.View.CommandView.SetPanelGroupExpanded(id,expanded);return Snapshot();},cancellationToken);
    [McpServerTool(Name="inspect_panel_hierarchy",ReadOnly=true),Description("Inspect group transforms/icons, complete hierarchy order, actual visible hierarchy rows and transformed viewport bounds.")]
    public Task<object> InspectPanelHierarchy(CancellationToken cancellationToken)
        =>ui.Invoke<object>(()=>new {groups=session.Groups,hierarchy=session.HierarchyElements,visibleRows=capture.View.CommandView.InspectPanelHierarchyRows(),world=session.ViewportElements,expanded=capture.View.CommandView.ExpandedPanelGroups},cancellationToken);
}
