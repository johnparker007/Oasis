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

    [McpServerTool(Name = "undo"), Description("Undo the last command in the specified session document. Does not undo copied files or saves.")]
    public Task<PanelSnapshot> Undo(Guid documentId, CancellationToken cancellationToken) => ui.Invoke(() => session.Undo(documentId), cancellationToken);

    [McpServerTool(Name = "redo"), Description("Redo the next command in the specified session document.")]
    public Task<PanelSnapshot> Redo(Guid documentId, CancellationToken cancellationToken) => ui.Invoke(() => session.Redo(documentId), cancellationToken);

    [McpServerTool(Name = "capture_panel2d", ReadOnly = true), Description("Return a PNG of the actual WPF Panel2D editor view rendered offscreen. No desktop capture or focus changes; excludes window chrome, menus and dialogs. Width 320..1920, height 240..1080; theme System/Light/Dark.")]
    public Task<CallToolResult> CapturePanel(Guid documentId, CancellationToken cancellationToken, int width = 1000, int height = 700, string theme = "System") => ui.Invoke(() =>
    {
        if (!Enum.TryParse<ThemePreference>(theme, true, out var preference) || !Enum.IsDefined(preference)) throw new ArgumentException("Use System, Light or Dark.");
        var png = session.CapturePanel(documentId, width, height, preference);
        return new CallToolResult { Content = [ImageContentBlock.FromBytes(png, "image/png")] };
    }, cancellationToken);
}