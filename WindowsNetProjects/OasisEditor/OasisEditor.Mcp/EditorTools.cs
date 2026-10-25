using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using OasisEditor.Automation;

namespace OasisEditor.Mcp;

[McpServerToolType]
public sealed class EditorTools(McpEditorSession session, EditorDispatcher ui)
{
    [McpServerTool(Name = "create_project"), Description("Create a new project under the explicit workspace. Refuses existing directories; one project per session.")]
    public Task<ProjectSnapshot> CreateProject(string name, CancellationToken cancellationToken) => ui.Invoke(() => session.CreateProject(name), cancellationToken);

    [McpServerTool(Name = "open_project"), Description("Open an existing current-schema project inside the workspace; does not launch a window.")]
    public Task<ProjectSnapshot> OpenProject(string path, CancellationToken cancellationToken) => ui.Invoke(() => session.OpenProject(path), cancellationToken);

    [McpServerTool(Name = "inspect_project", ReadOnly = true), Description("Read the active project and open panel document IDs.")]
    public Task<ProjectSnapshot> InspectProject(CancellationToken cancellationToken) => ui.Invoke(session.InspectProject, cancellationToken);

    [McpServerTool(Name = "create_panel2d"), Description("Create and save a new Panel2D package in the open project; refuses existing packages.")]
    public Task<PanelSnapshot> CreatePanel(string name, CancellationToken cancellationToken) => ui.Invoke(() => session.CreatePanel(name), cancellationToken);

    [McpServerTool(Name = "open_panel2d"), Description("Validate and reopen an existing Panel2D package in the open project; returns a session document ID.")]
    public Task<PanelSnapshot> OpenPanel(string path, CancellationToken cancellationToken) => ui.Invoke(() => session.OpenPanelDocument(path), cancellationToken);

    [McpServerTool(Name = "import_fml"), Description("Import a workspace .fml into the specified Panel2D using the editor importer and undo command. Returns warnings/errors. Does not auto-save. Import may write assets and existing importer diagnostics even on failure.")]
    public Task<ImportSnapshot> ImportFml(Guid documentId, string source, CancellationToken cancellationToken) => ui.Invoke(() => session.ImportFml(documentId, source), cancellationToken);

    [McpServerTool(Name = "save_panel2d"), Description("Save a session Panel2D; refuses to overwrite changes made externally since opening or saving.")]
    public Task<PanelSnapshot> SavePanel(Guid documentId, CancellationToken cancellationToken) => ui.Invoke(() => session.SavePanel(documentId), cancellationToken);

    [McpServerTool(Name = "inspect_panel2d", ReadOnly = true), Description("Read panel state, undo availability and a bounded page of element IDs, types and geometry. Limit 1..200.")]
    public Task<PanelSnapshot> InspectPanel(Guid documentId, CancellationToken cancellationToken, int offset = 0, int limit = 50) => ui.Invoke(() => session.InspectPanel(documentId, offset, limit), cancellationToken);

    [McpServerTool(Name = "inspect_panel_element", ReadOnly = true), Description("Read one Panel2D element by stable ID, including geometry, visibility, transform lock and selection state.")]
    public Task<PanelElementSnapshot> InspectElement(Guid documentId, string elementId, CancellationToken cancellationToken) => ui.Invoke(() => session.InspectElement(documentId, elementId), cancellationToken);

    [McpServerTool(Name = "select_panel_elements"), Description("Replace document-local selection with up to 200 element IDs. Empty array clears. Validates all IDs first; does not dirty the document or add undo history.")]
    public Task<PanelSnapshot> SelectElements(Guid documentId, string[] elementIds, CancellationToken cancellationToken) => ui.Invoke(() => session.SelectElements(documentId, elementIds), cancellationToken);

    [McpServerTool(Name = "update_panel_element"), Description("Atomically edit name, X/Y, width/height, visibility or transform lock through the editor's undo command. Omitted/null fields remain unchanged. No-ops add no history. Geometry must be finite with positive dimensions; unlock transforms in a separate call first. Does not save.")]
    public Task<ElementEditSnapshot> UpdateElement(Guid documentId, string elementId, PanelElementEdit edit, CancellationToken cancellationToken) => ui.Invoke(() => session.UpdateElement(documentId, elementId, edit), cancellationToken);

    [McpServerTool(Name = "set_panel_view"), Description("Set transient viewport zoom (0.01..64; 1=100%) and pan in view coordinates. Does not dirty or save the document. Use capture_panel2d framing=fit or element for automatic framing.")]
    public Task<PanelViewSnapshot> SetView(Guid documentId, double zoom, CancellationToken cancellationToken, double panX = 0, double panY = 0) => ui.Invoke(() => session.SetPanelView(documentId, zoom, panX, panY), cancellationToken);

    [McpServerTool(Name = "undo"), Description("Undo the last command in the specified session document. Does not undo copied files or saves.")]
    public Task<PanelSnapshot> Undo(Guid documentId, CancellationToken cancellationToken) => ui.Invoke(() => session.Undo(documentId), cancellationToken);

    [McpServerTool(Name = "redo"), Description("Redo the next command in the specified session document.")]
    public Task<PanelSnapshot> Redo(Guid documentId, CancellationToken cancellationToken) => ui.Invoke(() => session.Redo(documentId), cancellationToken);

    [McpServerTool(Name = "capture_panel2d", ReadOnly = true), Description("Return a PNG of the actual WPF Panel2D view offscreen, without focus changes. Width 320..1920, height 240..1080; theme System/Light/Dark. Framing=current uses the session viewport, fit fits the panel, element fits the supplied elementId. Captures restore zoom/pan. Excludes window chrome, menus and dialogs.")]
    public Task<CallToolResult> CapturePanel(Guid documentId, CancellationToken cancellationToken, int width = 1000, int height = 700, string theme = "System", string framing = "current", string? elementId = null) => ui.Invoke(() =>
    {
        if (!Enum.TryParse<ThemePreference>(theme, true, out var preference) || !Enum.IsDefined(preference)) throw new ArgumentException("Use System, Light or Dark.");
        var png = session.CapturePanel(documentId, width, height, preference, framing, elementId);
        return new CallToolResult { Content = [ImageContentBlock.FromBytes(png, "image/png")] };
    }, cancellationToken);
}
