using System.ComponentModel;
using ModelContextProtocol.Server;

namespace OasisEditor.Avalonia.Mcp;

public sealed partial class EditorTools
{
    [McpServerTool(Name = "move_workspace_pane"), Description("Move an actual live Dock pane (Project, Hierarchy, Inspector, Output or Authoring): float, or tab beside targetId. Exercises production pane presenters; no desktop input or authored changes.")]
    public Task<WorkspaceState> MovePane(string paneId, string placement, CancellationToken cancellationToken, string targetId = "Authoring")
        => ui.Invoke(() => capture.View.MoveWorkspacePane(paneId, placement, targetId), cancellationToken);

    [McpServerTool(Name = "split_document"), Description("Split two open native asset documents into adjacent Dock document groups. Keeps each asset's own editor, pending edits and command history.")]
    public Task<WorkspaceState> SplitDocument(string kind, string path, string targetKind, string targetPath,
        CancellationToken cancellationToken, string side = "right")
        => ui.InvokeAsync(async () =>
        {
            await capture.View.SplitDocumentAsync(kind, path, targetKind, targetPath, side);
            return capture.View.InspectWorkspaceState();
        }, cancellationToken);

    [McpServerTool(Name = "inspect_layout", ReadOnly = true), Description("Inspect actual dock topology, floating panes, theme, window bounds and clean document reopen state.")]
    public Task<WorkspaceState> InspectLayout(CancellationToken cancellationToken) => ui.Invoke(capture.View.InspectWorkspaceState, cancellationToken);

    [McpServerTool(Name = "save_workspace"), Description("Save actual workspace layout/theme/window/document paths to an explicit settings file inside the workspace. Does not save authored documents.")]
    public Task<WorkspaceState> SaveWorkspace(string path, CancellationToken cancellationToken) => ui.Invoke(() =>
    {
        var file = WorkspacePathPolicy.ResolveConfined(rootSession.WorkspacePath, Path.GetRelativePath(rootSession.WorkspacePath, Path.GetFullPath(path, rootSession.WorkspacePath)));
        capture.View.ConfigureWorkspacePersistence(file, restore: false); capture.View.SaveWorkspace();
        return capture.View.InspectWorkspaceState();
    }, cancellationToken);

    [McpServerTool(Name = "restore_workspace"), Description("Restore a saved workspace file inside the explicit workspace. Validates topology/content and refuses unsaved documents. No native desktop windows in this host.")]
    public Task<WorkspaceState> RestoreWorkspace(string path, CancellationToken cancellationToken) => ui.InvokeAsync(async () =>
    {
        var file = WorkspacePathPolicy.ResolveConfined(rootSession.WorkspacePath, Path.GetRelativePath(rootSession.WorkspacePath, Path.GetFullPath(path, rootSession.WorkspacePath)), requireExists: true);
        capture.View.ConfigureWorkspacePersistence(file);
        await capture.View.WaitForSceneAsync();return capture.View.InspectWorkspaceState();
    }, cancellationToken);

    [McpServerTool(Name = "reset_layout"), Description("Reset the docking layout while retaining document contents and pending edits. Does not overwrite saved settings.")]
    public Task<WorkspaceState> ResetLayout(CancellationToken cancellationToken) => ui.Invoke(() =>
    { capture.View.ResetLayout(); return capture.View.InspectWorkspaceState(); }, cancellationToken);
}
