using System.ComponentModel;
using ModelContextProtocol.Server;

namespace OasisEditor.Avalonia.Mcp;

public sealed partial class EditorTools
{
    [McpServerTool(Name = "inspect_layout", ReadOnly = true), Description("Inspect actual dock topology, floating panes, theme, window bounds and clean document reopen state.")]
    public Task<WorkspaceState> InspectLayout(CancellationToken cancellationToken) => ui.Invoke(capture.View.InspectWorkspaceState, cancellationToken);

    [McpServerTool(Name = "save_workspace"), Description("Save actual workspace layout/theme/window/document paths to an explicit settings file inside the workspace. Does not save authored documents.")]
    public Task<WorkspaceState> SaveWorkspace(string path, CancellationToken cancellationToken) => ui.Invoke(() =>
    {
        var file = WorkspacePathPolicy.ResolveConfined(session.WorkspacePath, Path.GetRelativePath(session.WorkspacePath, Path.GetFullPath(path, session.WorkspacePath)));
        capture.View.ConfigureWorkspacePersistence(file, restore: false); capture.View.SaveWorkspace();
        return capture.View.InspectWorkspaceState();
    }, cancellationToken);

    [McpServerTool(Name = "restore_workspace"), Description("Restore a saved workspace file inside the explicit workspace. Validates topology/content and refuses unsaved documents. No native desktop windows in this host.")]
    public Task<WorkspaceState> RestoreWorkspace(string path, CancellationToken cancellationToken) => ui.InvokeAsync(async () =>
    {
        var file = WorkspacePathPolicy.ResolveConfined(session.WorkspacePath, Path.GetRelativePath(session.WorkspacePath, Path.GetFullPath(path, session.WorkspacePath)), requireExists: true);
        capture.View.ConfigureWorkspacePersistence(file);
        await capture.View.WaitForSceneAsync();return capture.View.InspectWorkspaceState();
    }, cancellationToken);

    [McpServerTool(Name = "reset_layout"), Description("Reset the docking layout while retaining document contents and pending edits. Does not overwrite saved settings.")]
    public Task<WorkspaceState> ResetLayout(CancellationToken cancellationToken) => ui.Invoke(() =>
    { capture.View.ResetLayout(); return capture.View.InspectWorkspaceState(); }, cancellationToken);
}
