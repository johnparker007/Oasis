using System.ComponentModel;
using ModelContextProtocol.Server;

namespace OasisEditor.Avalonia.Mcp;

public sealed partial class EditorTools
{
    [McpServerTool(Name = "close_project"), Description("Close the open project after all retained authored documents are saved or undone. Stops this session's native preview; never discards edits.")]
    public Task<object> CloseProject(CancellationToken cancellationToken)
        => ui.InvokeAsync<object>(async () =>
        {
            await capture.View.CloseProjectAsync();
            capture.View.ShowPanel();
            return Snapshot();
        }, cancellationToken);

    [McpServerTool(Name = "validate_face"), Description("Run portable Face source-panel and tray/emitter validation. Returns diagnostics without changing authored data.")]
    public Task<object> ValidateFace(CancellationToken cancellationToken)
        => ui.Invoke<object>(() => new { workspace = Snapshot(), diagnostics = capture.View.ValidateActiveFace() }, cancellationToken);

    [McpServerTool(Name = "open_face_source_panel"), Description("Open the saved Panel2D manifest recorded by the active Face. Fails safely if the Face has no source path or the saved source is unavailable.")]
    public Task<object> OpenFaceSourcePanel(CancellationToken cancellationToken)
        => ui.Invoke(() =>
        {
            capture.View.OpenFaceSourcePanel();
            return Snapshot();
        }, cancellationToken);
}
