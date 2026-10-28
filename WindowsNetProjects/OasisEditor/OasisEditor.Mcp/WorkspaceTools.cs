using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace OasisEditor.Avalonia.Mcp;

public sealed partial class EditorTools
{
    [McpServerTool(Name = "move_workspace_pane"), Description("Move an actual live Dock pane (Project, Hierarchy, Inspector, Output or Authoring): float, or tab beside targetId. Exercises production pane presenters; no desktop input or authored changes.")]
    public Task<WorkspaceState> MovePane(string paneId, string placement, CancellationToken cancellationToken, string targetId = "Authoring")
        => ui.Invoke(() => capture.View.MoveWorkspacePane(paneId, placement, targetId), cancellationToken);

    [McpServerTool(Name = "add_workspace_tab"), Description("Reopen or move a standard workspace tab into the tab group containing targetPaneId.")]
    public Task<WorkspaceState> AddWorkspaceTab(string paneId, string targetPaneId, CancellationToken cancellationToken)
        => ui.Invoke(() =>
        {
            capture.View.AddWorkspaceTab(paneId, targetPaneId);
            return capture.View.InspectWorkspaceState();
        }, cancellationToken);

    [McpServerTool(Name = "close_workspace_tab"), Description("Close a standard workspace tab using the production tab context-menu action.")]
    public Task<WorkspaceState> CloseWorkspaceTab(string paneId, CancellationToken cancellationToken)
        => ui.Invoke(() =>
        {
            var menu = capture.View.CreateTabContextMenu(paneId);
            var close = menu.Items.OfType<MenuItem>().Single(item => Equals(item.Header, "Close Tab"));
            if (!close.IsEnabled) throw new InvalidOperationException($"Workspace tab '{paneId}' cannot be closed in its current state.");
            close.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            return capture.View.InspectWorkspaceState();
        }, cancellationToken);

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

    [McpServerTool(Name = "toggle_tab_maximised"), Description("Maximise a live document or pane in its current dock host, or restore it to its exact docked/floating placement. Keeps document contents and history intact.")]
    public Task<WorkspaceTabPresentation> ToggleTabMaximised(string paneId, CancellationToken cancellationToken)
        => ui.InvokeAsync(async () =>
        {
            if (capture.View.InspectTabPresentation().MaximisedPaneId != paneId)
                await capture.View.SynchronizeAssetDocumentsAsync();
            return capture.View.ToggleTabMaximised(paneId);
        }, cancellationToken);

    [McpServerTool(Name = "inspect_tab_presentation", ReadOnly = true), Description("Inspect whether a document or pane is currently maximised in its Dock host.")]
    public Task<WorkspaceTabPresentation> InspectTabPresentation(CancellationToken cancellationToken)
        => ui.Invoke(capture.View.InspectTabPresentation, cancellationToken);

    [McpServerTool(Name = "inspect_tab_menu", ReadOnly = true), Description("Inspect the live Dock tab context menu, including Maximise state and whether Close Tab is available.")]
    public Task<object> InspectTabMenu(string paneId, CancellationToken cancellationToken)
        => ui.Invoke<object>(() =>
        {
            static object Describe(Control item) => new
            {
                type = item.GetType().Name,
                header = (item as MenuItem)?.Header?.ToString(),
                enabled = item.IsEnabled,
                visible = item.IsVisible,
                checkedState = (item as MenuItem)?.IsChecked,
                children = item is MenuItem menuItem
                    ? menuItem.Items.OfType<Control>().Select(Describe).ToArray()
                    : Array.Empty<object>()
            };
            var menu = capture.OpenTabMenu(paneId);
            try
            {
                return new { paneId,
                    items = menu.Items.OfType<MenuItem>().Select(Describe).ToArray(),
                    entries = menu.Items.OfType<Control>().Select(Describe).ToArray() };
            }
            finally { menu.Close(); }
        }, cancellationToken);

    [McpServerTool(Name = "capture_tab_menu", ReadOnly = true), Description("Capture the production tab action popup for a document or pane in the requested theme. Headless workspace only; no desktop input.")]
    public Task<CallToolResult> CaptureTabMenu(string paneId, CancellationToken cancellationToken, string theme = "Dark", int width = 1400, int height = 900)
        => ui.Invoke(() =>
        {
            capture.Capture(width, height, theme);
            var menu = capture.OpenTabMenu(paneId);
            try
            {
                var images = new List<byte[]> { capture.Capture(width, height, theme) };
                images.AddRange(capture.CaptureFloatingWindows());
                return new CallToolResult { Content = [.. images.Select(image => ImageContentBlock.FromBytes(image, "image/png"))] };
            }
            finally { menu.Close(); }
        }, cancellationToken);

    [McpServerTool(Name = "restore_maximised_tab"), Description("Restore the maximised document or pane to its exact original docked or floating placement.")]
    public Task<WorkspaceTabPresentation> RestoreMaximisedTab(CancellationToken cancellationToken)
        => ui.Invoke(() =>
        {
            capture.View.RestoreMaximisedTab();
            return capture.View.InspectTabPresentation();
        }, cancellationToken);

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
