using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
namespace OasisEditor.Avalonia.Mcp;
public sealed partial class EditorTools
{
    [McpServerTool(Name="import_face_lamp_mask"), Description("Import a workspace image as an immutable authored Face lamp mask; Build normalizes a generated copy without overwriting the source.")]
    public Task<object> ImportFaceLampMask(string path, CancellationToken cancellationToken)
        => ui.Invoke(() => ActFace(() => faces.ImportLampMask(ResolveWorkspaceFile(path))), cancellationToken);
    [McpServerTool(Name="build_face"), Description("Rebuild active Face artwork, lamp mask and native auto-authored trays. Optional standalone runtime export uses real native texture generation; missing Machine composition dependencies fail explicitly. Rolls back on failure. Does not save the authored manifest.")]
    public Task<object> BuildFace(CancellationToken cancellationToken, bool runtimeAssets=false)
        => ui.Invoke(() => { capture.View.ShowFace(); capture.View.BuildActiveFace(runtimeAssets,cancellationToken); return Snapshot(); }, cancellationToken);
    [McpServerTool(Name="rebuild_face_components_from_source"), Description("Replace local Face component edits from its saved source Panel2D as one undoable edit. Requires explicit confirmation. Artwork and illumination are preserved.")]
    public Task<object> RebuildFaceComponents(bool confirm, CancellationToken cancellationToken)
        => ui.Invoke(() => ActFace(() => { if(!confirm) throw new ArgumentException("Confirm replacement of local component edits."); faces.RebuildComponentsFromSource(); }), cancellationToken);
    [McpServerTool(Name="use_panel2d_face_artwork_source"), Description("Restore the Face's saved Panel2D source as its artwork recipe; preserves processing and override settings. Build applies the change.")]
    public Task<object> UsePanelFaceArtwork(CancellationToken cancellationToken) => ui.Invoke(() => ActFace(() => faces.UsePanelArtworkSource()),cancellationToken);
    [McpServerTool(Name="use_panel2d_face_lamp_mask"), Description("Configure the saved Panel2D source as the Face's derived lamp-mask recipe. Build regenerates mask and trays.")]
    public Task<object> UsePanelFaceLampMask(CancellationToken cancellationToken) => ui.Invoke(() => ActFace(faces.UseDerivedLampMask),cancellationToken);
    [McpServerTool(Name="create_face_artwork_override_from_base"), Description("Copy current generated Base artwork into an immutable authored override image. Requires a current Base build.")]
    public Task<object> CreateFaceOverrideFromBase(CancellationToken cancellationToken) => ui.Invoke(() => ActFace(() => faces.CreateArtworkOverrideFromBase()),cancellationToken);
    [McpServerTool(Name="set_face_artwork_override_enabled"), Description("Enable or disable the existing native artwork override through document history.")]
    public Task<object> EnableFaceOverride(bool enabled, CancellationToken cancellationToken)
        => ui.Invoke(() => ActFace(() => faces.SetArtworkOverrideEnabled(enabled)), cancellationToken);
    [McpServerTool(Name="remove_face_artwork_override"), Description("Remove the override recipe through document history; the immutable source image is retained.")]
    public Task<object> RemoveFaceOverride(CancellationToken cancellationToken)
        => ui.Invoke(() => ActFace(() => faces.RemoveArtworkOverride()), cancellationToken);
    [McpServerTool(Name="inspect_face_build", ReadOnly=true), Description("Inspect native Face generation states, lamp-mask source, trays, emitters, and runtime asset paths.")]
    public Task<object> InspectFaceBuild(CancellationToken cancellationToken)
        => ui.Invoke<object>(() => { var f=faces.Document; return new { f.BuildState, f.MaskLayer, f.Trays, f.LampEmitters, f.RuntimeRenderAssets }; }, cancellationToken);
    [McpServerTool(Name="capture_face_artwork_editor", ReadOnly=true), Description("Capture the actual native Avalonia Face registration/calibration dialog in the isolated headless host. No desktop capture or authored changes.")]
    public Task<CallToolResult> CaptureFaceArtworkEditor(CancellationToken cancellationToken,bool calibrationStage=false,string theme="Dark")
        => ui.InvokeAsync(async () => new CallToolResult { Content=[ImageContentBlock.FromBytes(await capture.CaptureFaceArtworkEditor(session,faces,calibrationStage,theme),"image/png")] },cancellationToken);
}
