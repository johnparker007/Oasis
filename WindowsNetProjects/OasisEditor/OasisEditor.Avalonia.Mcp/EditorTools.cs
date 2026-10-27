using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace OasisEditor.Avalonia.Mcp;

[McpServerToolType]
public sealed partial class EditorTools(AuthoringSession session, FaceAuthoringSession faces, WorkspaceCapture capture, EditorDispatcher ui)
{
    private object Snapshot() => new
    {
        session.ProjectName, session.ProjectFilePath, session.PanelTitle, session.PanelPath,
        session.IsDirty, session.CanUndo, session.CanRedo, session.SelectedId, session.SelectedIds,
        panels = session.ProjectFilePath is null ? [] : session.ListPanels(), elements = session.Elements,
        assets = session.ProjectFilePath is null ? [] : session.ListAssets(), sourceShapes = session.SourceShapes,
        recoveredInputDefinitions = session.ImportedInputDefinitions,
        face = new { faces.FacePath, faces.FaceTitle, faces.IsDirty, faces.CanUndo, faces.CanRedo, faces.SelectedId, elements = faces.Elements, faces.ArtworkPath, faces.LogicalWidth, faces.LogicalHeight },
        rendering = new { supported = "Panel2D geometry, artwork images and text; Face artwork and editable bounds", deferred = "3D, emulation previews, specialist lamp/reel/display rendering" }
    };
    private object Act(Action action) { action(); capture.View.ShowPanel(); return Snapshot(); }
    private object ActFace(Action action) { action(); capture.View.ShowFace(); return Snapshot(); }

    [McpServerTool(Name = "create_project"), Description("Create and open a complete native Oasis project, including the real default Machine. Refuses existing folders and unsaved edits.")]
    public Task<object> CreateProject(string name, CancellationToken cancellationToken) => ui.Invoke(() => Act(() => capture.View.CreateProject(name)), cancellationToken);

    [McpServerTool(Name = "open_project"), Description("Open a schema-9 Oasis project within the explicit workspace. Headless isolated session; rejects dirty replacement.")]
    public Task<object> OpenProject(string path, CancellationToken cancellationToken) => ui.Invoke(() => Act(() => capture.View.OpenProject(path)), cancellationToken);

    [McpServerTool(Name = "inspect_workspace", ReadOnly = true), Description("Inspect project, Panel2D assets, active document, selection, dirty state, history and all element geometry.")]
    public Task<object> Inspect(CancellationToken cancellationToken) => ui.Invoke(Snapshot, cancellationToken);

    [McpServerTool(Name = "open_panel2d"), Description("Open Assets/Panel2D/<name>/asset.panel2d using the shared Oasis storage validator. Refuses unsaved replacement.")]
    public Task<object> OpenPanel(string path, CancellationToken cancellationToken) => ui.Invoke(() => Act(() => session.OpenPanel(path)), cancellationToken);

    [McpServerTool(Name = "create_panel2d"), Description("Create and save a fresh schema-3 Panel2D package. Refuses existing packages.")]
    public Task<object> CreatePanel(string name, CancellationToken cancellationToken) => ui.Invoke(() => Act(() => session.CreatePanel(name)), cancellationToken);

    [McpServerTool(Name = "add_rectangle"), Description("Add a real Panel2D rectangle through document history and select it. Does not save.")]
    public Task<object> AddRectangle(CancellationToken cancellationToken) => ui.Invoke(() => Act(() => session.AddRectangle()), cancellationToken);

    [McpServerTool(Name = "select_element"), Description("Select one element by stable ID, or clear selection with null. Selection does not dirty the document.")]
    public Task<object> Select(CancellationToken cancellationToken, string? id = null) => ui.Invoke(() => Act(() => session.Select(id)), cancellationToken);

    [McpServerTool(Name = "update_element"), Description("Apply one atomic, undoable name/geometry/visibility/lock edit. Locked transforms reject geometry edits. Does not save.")]
    public Task<object> Update(string id, CancellationToken cancellationToken, string? name = null, double? x = null, double? y = null,
        double? width = null, double? height = null, bool? visible = null, bool? locked = null)
        => ui.Invoke(() => Act(() => session.UpdateElement(id, name, x, y, width, height, visible, locked)), cancellationToken);

    [McpServerTool(Name = "history"), Description("Undo one document command; set redo=true to redo. Does not save.")]
    public Task<object> History(CancellationToken cancellationToken, bool redo = false)
        => ui.Invoke(() => Act(() => { if (redo) session.Redo(); else session.Undo(); }), cancellationToken);

    [McpServerTool(Name = "save_panel2d"), Description("Atomically save using shared full Panel2D DTOs. Refuses files changed externally or paths replaced by links.")]
    public Task<object> Save(CancellationToken cancellationToken) => ui.Invoke(() => Act(session.Save), cancellationToken);

    [McpServerTool(Name = "capture_workspace", ReadOnly = true), Description("Render the actual Avalonia docking workspace and 2D viewport to a PNG with headless Skia. No desktop capture, focus or native window. Themes System/Light/Dark.")]
    public Task<CallToolResult> Capture(CancellationToken cancellationToken, int width = 1400, int height = 900, string theme = "Dark")
        => ui.Invoke(() => new CallToolResult { Content = [ImageContentBlock.FromBytes(capture.Capture(width, height, theme), "image/png")] }, cancellationToken);
}
