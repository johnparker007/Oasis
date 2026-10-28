using System.ComponentModel;
using ModelContextProtocol.Server;

namespace OasisEditor.Avalonia.Mcp;

public sealed partial class EditorTools
{
    private ReelAuthoringSession Reels => capture.View.GetDocumentEditor("Reel").Reels;
    private object ReelSnapshot() => new { path=Reels.FilePath, document=Reels.FilePath is null?null:Reels.Document,
        Reels.IsDirty,Reels.CanUndo,Reels.CanRedo,scene=capture.View.CommandView.InspectScene3D() };
    private void RequireReel()
    {if(capture.View.CommandView.SceneKind!="Reel"||Reels.FilePath is null)throw new InvalidOperationException("Open a Reel asset first.");}
    [McpServerTool(Name="create_reel"),Description("Create and open a native physical Reel package. The schema stores stable ID, display name, diameter and width in mm; Face symbol-band artwork is separate. Refuses existing destinations and unsaved replacement.")]
    public Task<object> CreateReel(string name,CancellationToken cancellationToken)
        =>ui.InvokeAsync<object>(async()=>{var path=Reels.Create(name);await capture.View.OpenNativeAssetAsync("Reel",path);return ReelSnapshot();},cancellationToken);
    [McpServerTool(Name="open_reel"),Description("Open an existing project Reel manifest in the native physical Reel inspector and GPU preview. Protects pending edits.")]
    public Task<object> OpenReel(string path,CancellationToken cancellationToken)
        =>ui.InvokeAsync<object>(async()=>{await capture.View.OpenNativeAssetAsync("Reel",path);return ReelSnapshot();},cancellationToken);
    [McpServerTool(Name="inspect_reel",ReadOnly=true),Description("Inspect the open native Reel document, history, dirty state and actual physical preview scene.")]
    public Task<object> InspectReel(CancellationToken cancellationToken)
        =>ui.InvokeAsync<object>(async()=>{await capture.View.CommandView.WaitForSceneAsync();return ReelSnapshot();},cancellationToken);
    [McpServerTool(Name="update_reel"),Description("Apply a validated native Reel edit as one undoable command. Dimensions must be positive finite millimetres; ID is retained. Omitted fields remain unchanged.")]
    public Task<object> UpdateReel(CancellationToken cancellationToken,string? displayName=null,double? diameterMm=null,double? widthMm=null)
        =>ui.InvokeAsync<object>(async()=>{RequireReel();Reels.Update(displayName,diameterMm,widthMm);await capture.View.CommandView.WaitForSceneAsync();return ReelSnapshot();},cancellationToken);
    [McpServerTool(Name="save_reel"),Description("Atomically save the active native Reel. Refuses external file/package changes and links.")]
    public Task<object> SaveReel(CancellationToken cancellationToken)
        =>ui.Invoke<object>(()=>{RequireReel();Reels.Save();return ReelSnapshot();},cancellationToken);
}
