using System.ComponentModel;
using ModelContextProtocol.Server;

namespace OasisEditor.Avalonia.Mcp;

public sealed partial class EditorTools
{
    [McpServerTool(Name="float_document"),Description("Move an open asset tab into its own live editor window, preserving unsaved contents, selection and undo history. In this host the window is headless; no desktop input or focus is used.")]
    public Task<object> FloatDocument(string kind,string path,CancellationToken cancellationToken,double x=80,double y=80,double width=1200,double height=800)
        =>ui.InvokeAsync<object>(async()=>{await capture.View.FloatDocumentAsync(kind,path,new(x,y,width,height));return capture.View.InspectFloatingDocument(kind,path);},cancellationToken);

    [McpServerTool(Name="dock_document"),Description("Return one owned floating asset to the main workspace without saving or discarding its pending edits/history.")]
    public Task<object> DockDocument(string kind,string path,CancellationToken cancellationToken)
        =>ui.InvokeAsync<object>(async()=>{await capture.View.DockDocumentAsync(kind,path);return Snapshot();},cancellationToken);

    [McpServerTool(Name="inspect_floating_documents",ReadOnly=true),Description("Inspect owned asset window paths, Library scopes, bounds and viewport states. No desktop inspection.")]
    public Task<IReadOnlyList<FloatingAssetState>> InspectFloatingDocuments(CancellationToken cancellationToken)
        =>ui.Invoke(capture.View.InspectFloatingDocuments,cancellationToken);

    [McpServerTool(Name="inspect_floating_document",ReadOnly=true),Description("Inspect one floating asset's actual document, selection, dirty state and GPU scene diagnostics.")]
    public Task<object> InspectFloatingDocument(string kind,string path,CancellationToken cancellationToken)
        =>ui.Invoke(()=>capture.View.InspectFloatingDocument(kind,path),cancellationToken);

    [McpServerTool(Name="floating_document_history"),Description("Undo/redo one command in the specified floating document's own history; does not save.")]
    public Task<object> FloatingDocumentHistory(string kind,string path,CancellationToken cancellationToken,bool redo=false)
        =>ui.Invoke(()=>{capture.View.FloatingDocumentHistory(kind,path,redo);return capture.View.InspectFloatingDocument(kind,path);},cancellationToken);

    [McpServerTool(Name="save_floating_document"),Description("Save the specified floating asset through its native save/conflict guards.")]
    public Task<object> SaveFloatingDocument(string kind,string path,CancellationToken cancellationToken)
        =>ui.Invoke(()=>{capture.View.SaveFloatingDocument(kind,path);return capture.View.InspectFloatingDocument(kind,path);},cancellationToken);

    [McpServerTool(Name="update_floating_element"),Description("Apply one native undoable geometry/name/visibility/lock edit to an element in a floating Panel2D or Face. Other windows are unaffected.")]
    public Task<object> UpdateFloatingElement(string kind,string path,string id,CancellationToken cancellationToken,string? name=null,double? x=null,double? y=null,
        double? width=null,double? height=null,bool? visible=null,bool? locked=null)
        =>ui.Invoke(()=>{capture.View.UpdateFloatingElement(kind,path,id,name,x,y,width,height,visible,locked);return capture.View.InspectFloatingDocument(kind,path);},cancellationToken);

    [McpServerTool(Name="update_floating_reel"),Description("Edit native physical Reel dimensions in its owned floating editor.")]
    public Task<object> UpdateFloatingReel(string path,CancellationToken cancellationToken,string? displayName=null,double? diameterMm=null,double? widthMm=null)
        =>ui.Invoke(()=>{capture.View.GetFloatingDocumentView("Reel",path).Reels.Update(displayName,diameterMm,widthMm);return capture.View.InspectFloatingDocument("Reel",path);},cancellationToken);

    [McpServerTool(Name="set_floating_cabinet_model"),Description("Apply a native scale/up-axis edit in the specified floating Cabinet editor.")]
    public Task<object> SetFloatingCabinetModel(string path,CancellationToken cancellationToken,double? scale=null,string? upAxis=null)
        =>ui.Invoke(()=>{capture.View.GetFloatingDocumentView("Cabinet3D",path).Cabinets.SetModel(scale,upAxis);return capture.View.InspectFloatingDocument("Cabinet3D",path);},cancellationToken);

    [McpServerTool(Name="add_floating_prefab_node"),Description("Add a child node through a floating Prefab's own validated history.")]
    public Task<object> AddFloatingPrefabNode(string path,string parentId,string name,CancellationToken cancellationToken)
        =>ui.Invoke(()=>{capture.View.GetFloatingDocumentView("Prefab",path).Prefabs.AddNode(parentId,name);return capture.View.InspectFloatingDocument("Prefab",path);},cancellationToken);

    [McpServerTool(Name="set_floating_machine_name"),Description("Apply a native display-name edit in the specified floating Machine editor.")]
    public Task<object> SetFloatingMachineName(string path,string name,CancellationToken cancellationToken)
        =>ui.Invoke(()=>{capture.View.GetFloatingDocumentView("Machine",path).Machines.SetDisplayName(name);return capture.View.InspectFloatingDocument("Machine",path);},cancellationToken);
}
