using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using OasisEditor.Automation;

namespace OasisEditor.Mcp;

[McpServerToolType]
public sealed class EditorTools(McpEditorSession session, EditorDispatcher ui)
{
    [McpServerTool(Name = "create_prefab"), Description("Create and initially save a transform-only Prefab in the existing Object3D asset family.")]
    public Task<PrefabSnapshot> CreatePrefab(string name, CancellationToken cancellationToken, string? modelSource = null) => ui.Invoke(() => session.CreatePrefab(name, modelSource), cancellationToken);

    [McpServerTool(Name = "open_prefab", ReadOnly = true), Description("Open a current-schema Prefab at Assets/Object3D/<Name>/asset.object3d in the project.")]
    public Task<PrefabSnapshot> OpenPrefab(string path, CancellationToken cancellationToken) => ui.Invoke(() => session.OpenPrefabDocument(path), cancellationToken);

    [McpServerTool(Name = "inspect_prefab", ReadOnly = true), Description("Inspect the Prefab node/component tree, stable IDs, dirty/history state and current build projection diagnostics.")]
    public Task<PrefabSnapshot> InspectPrefab(Guid documentId, CancellationToken cancellationToken) => ui.Invoke(() => session.InspectPrefab(documentId), cancellationToken);

    [McpServerTool(Name = "update_prefab_node"), Description("Atomically edit a Prefab node's name, local transform or one supported component, or remove one component kind. Validation precedes one undoable command. Model paths are package-relative.")]
    public Task<PrefabSnapshot> UpdatePrefabNode(Guid documentId, string nodeId, CancellationToken cancellationToken, string? name = null, MachineObjectTransform? transform = null, Object3DComponentDefinition? component = null, string? removeComponentKind = null) => ui.Invoke(() => session.UpdatePrefabNode(documentId, nodeId, name, transform, component, removeComponentKind), cancellationToken);

    [McpServerTool(Name = "add_prefab_child"), Description("Add a transform-only child with new stable node/component IDs through the document command history.")]
    public Task<PrefabSnapshot> AddPrefabChild(Guid documentId, string parentNodeId, string name, CancellationToken cancellationToken) => ui.Invoke(() => session.AddPrefabChild(documentId, parentNodeId, name), cancellationToken);

    [McpServerTool(Name = "reparent_prefab_node"), Description("Move a Prefab node beneath a parent at an optional sibling index. Rejects cycles and moving the root.")]
    public Task<PrefabSnapshot> ReparentPrefabNode(Guid documentId, string nodeId, string parentNodeId, CancellationToken cancellationToken, int? index = null) => ui.Invoke(() => session.ReparentPrefabNode(documentId, nodeId, parentNodeId, index), cancellationToken);

    [McpServerTool(Name = "remove_prefab_node"), Description("Remove a non-root node subtree through one undoable command.")]
    public Task<PrefabSnapshot> RemovePrefabNode(Guid documentId, string nodeId, CancellationToken cancellationToken) => ui.Invoke(() => session.RemovePrefabNode(documentId, nodeId), cancellationToken);

    [McpServerTool(Name = "prefab_history"), Description("Undo one Prefab command, or redo when redo=true.")]
    public Task<PrefabSnapshot> PrefabHistory(Guid documentId, CancellationToken cancellationToken, bool redo = false) => ui.Invoke(() => session.PrefabHistory(documentId, redo), cancellationToken);

    [McpServerTool(Name = "save_prefab"), Description("Save validated Prefab nodes/components, refusing external authored package changes. Source models must exist inside their package.")]
    public Task<PrefabSnapshot> SavePrefab(Guid documentId, CancellationToken cancellationToken) => ui.Invoke(() => session.SavePrefab(documentId), cancellationToken);

    [McpServerTool(Name = "capture_prefab", ReadOnly = true), Description("Capture the actual offscreen Prefab hierarchy/viewport/Inspector without desktop input.")]
    public async Task<CallToolResult> CapturePrefab(Guid documentId, CancellationToken cancellationToken, int width = 1400, int height = 900, string theme = "System")
    {
        if (!Enum.TryParse<ThemePreference>(theme, true, out var preference) || !Enum.IsDefined(preference)) throw new ArgumentException("Use System, Light, or Dark theme.");
        var capture = await ui.Invoke(() => session.CapturePrefabAsync(documentId, width, height, preference), cancellationToken);
        return new CallToolResult { Content = [ImageContentBlock.FromBytes(await capture, "image/png")] };
    }
    [McpServerTool(Name = "list_recent_projects", ReadOnly = true), Description("Read the editor's recent-project list. Listing does not grant workspace access; use a server configured for the intended project or a disposable copy.")]
    public Task<IReadOnlyList<string>> RecentProjects(CancellationToken cancellationToken) => ui.Invoke(McpEditorSession.ListRecentProjects, cancellationToken);

    [McpServerTool(Name = "list_machine_assets", ReadOnly = true), Description("List saved Machine manifest paths in the open project, maximum 200.")]
    public Task<IReadOnlyList<string>> Machines(CancellationToken cancellationToken) => ui.Invoke(session.ListMachineAssets, cancellationToken);

    [McpServerTool(Name = "open_machine", ReadOnly = true), Description("Open a saved Machine for read-only inspection inside the project; returns configuration, objects, anchors, inputs, script and diagnostics. Does not display a window or save.")]
    public Task<MachineSnapshot> OpenMachine(string path, CancellationToken cancellationToken) => ui.Invoke(() => session.OpenMachine(path), cancellationToken);

    [McpServerTool(Name = "inspect_machine", ReadOnly = true), Description("Read an open Machine's configuration and Oasis script diagnostics; document IDs belong to this session.")]
    public Task<MachineSnapshot> InspectMachine(Guid documentId, CancellationToken cancellationToken) => ui.Invoke(() => session.InspectMachine(documentId), cancellationToken);

    [McpServerTool(Name = "update_machine_anchor"), Description("Edit an existing Machine anchor's position and/or XYZ Euler rotation through one undoable command. Does not save.")]
    public Task<MachineSnapshot> UpdateMachineAnchor(Guid documentId, string anchorId, CancellationToken cancellationToken, MachineVector3? position = null, MachineVector3? rotation = null) => ui.Invoke(() => session.UpdateMachineAnchor(documentId, anchorId, position, rotation), cancellationToken);

    [McpServerTool(Name = "save_machine"), Description("Save the open Machine through the existing save service, refusing externally changed manifest or behavior source.")]
    public Task<MachineSnapshot> SaveMachine(Guid documentId, CancellationToken cancellationToken) => ui.Invoke(() => session.SaveMachine(documentId), cancellationToken);

    [McpServerTool(Name = "declare_machine_prefab"), Description("Declare a project Prefab alias for script spawning without adding a placed object. One undoable command; does not save.")]
    public Task<MachineSnapshot> DeclareMachinePrefab(Guid documentId, string alias, string prefabPath, CancellationToken cancellationToken) => ui.Invoke(() => session.DeclareMachinePrefab(documentId, alias, prefabPath), cancellationToken);

    [McpServerTool(Name = "set_machine_geometry_surface"), Description("Set a measured Machine-space rectangular support surface for bounded geometry queries. Finite pose and positive width/length required.")]
    public Task<MachineSnapshot> SetMachineGeometrySurface(Guid documentId, MachineGeometrySurface surface, CancellationToken cancellationToken) => ui.Invoke(() => session.SetMachineGeometrySurface(documentId, surface), cancellationToken);

    [McpServerTool(Name = "set_machine_interaction"), Description("Set the Machine interaction profile through one undoable command. Use kind Buttons with no Pool parameters, or Pool with a declared cuePrefabAlias and geometrySurface playingSurfaceId. Does not save.")]
    public Task<MachineSnapshot> SetMachineInteraction(Guid documentId, string kind, CancellationToken cancellationToken, string? cuePrefabAlias = null, string? playingSurfaceId = null) => ui.Invoke(() => session.SetMachineInteraction(documentId, kind, cuePrefabAlias, playingSurfaceId), cancellationToken);

    [McpServerTool(Name = "set_machine_behavior"), Description("Replace an existing Oasis Machine's behavior source through an undoable command; diagnostics are returned. Save/build remains explicit.")]
    public Task<MachineSnapshot> SetMachineBehavior(Guid documentId, string source, CancellationToken cancellationToken) => ui.Invoke(() => session.SetMachineBehavior(documentId, source), cancellationToken);

    [McpServerTool(Name = "set_machine_anchor"), Description("Add or update a Machine-space anchor with stable ID through a validated command.")]
    public Task<MachineSnapshot> SetMachineAnchor(Guid documentId, MachineAnchor anchor, CancellationToken cancellationToken) => ui.Invoke(() => session.SetMachineAnchor(documentId, anchor), cancellationToken);

    [McpServerTool(Name = "remove_machine_object"), Description("Remove a placed object subtree through its document command history; reusable Prefab sources are retained.")]
    public Task<MachineSnapshot> RemoveMachineObject(Guid documentId, string objectId, CancellationToken cancellationToken) => ui.Invoke(() => session.RemoveMachineObject(documentId, objectId), cancellationToken);

    [McpServerTool(Name = "add_machine_object"), Description("Place a project Prefab using a stable Machine object ID, optional parent and local transform. One undoable command; does not save.")]
    public Task<MachineSnapshot> AddMachineObject(Guid documentId, string id, string prefabPath, CancellationToken cancellationToken,
        string? name = null, MachineObjectTransform? transform = null, string? parentId = null, bool active = true) => ui.Invoke(() => session.AddMachineObject(documentId, id, prefabPath, name, transform, parentId, active), cancellationToken);

    [McpServerTool(Name = "update_machine_object"), Description("Edit root-only placement overrides: local transform, parent, active state and display name. clearParent=true returns to Machine root. Prefab structure remains source-owned.")]
    public Task<MachineSnapshot> UpdateMachineObject(Guid documentId, string objectId, CancellationToken cancellationToken,
        MachineObjectTransform? transform = null, string? parentId = null, bool clearParent = false, bool? active = null, string? name = null) => ui.Invoke(() => session.UpdateMachineObject(documentId, objectId, transform, parentId, clearParent, active, name), cancellationToken);

    [McpServerTool(Name = "undo_machine"), Description("Undo one command in this Machine document.")]
    public Task<MachineSnapshot> UndoMachine(Guid documentId, CancellationToken cancellationToken) => ui.Invoke(() => session.UndoMachine(documentId), cancellationToken);

    [McpServerTool(Name = "redo_machine"), Description("Redo one command in this Machine document.")]
    public Task<MachineSnapshot> RedoMachine(Guid documentId, CancellationToken cancellationToken) => ui.Invoke(() => session.RedoMachine(documentId), cancellationToken);

    [McpServerTool(Name = "build_machine"), Description("Build the persisted Machine for Oasis Player through the existing validated build pipeline. Writes/replaces Generated build output; use a disposable project. Currently requires project-local assets; returns success/buildRoot/errorMessage. Does not launch Player.")]
    public Task<MachineRuntimeBuildResult> BuildMachine(Guid documentId, CancellationToken cancellationToken) => ui.Invoke(() => session.BuildMachine(documentId, cancellationToken), cancellationToken);

    [McpServerTool(Name = "capture_machine", ReadOnly = true), Description("Return a PNG of the real editor's Machine Composition view rendered offscreen. No focus changes. Width 640..1920, height 480..1080; theme System/Light/Dark. View render, not a full-window screenshot.")]
    public async Task<CallToolResult> CaptureMachine(Guid documentId, CancellationToken cancellationToken, int width = 1200, int height = 900, string theme = "System")
    {
        if (!Enum.TryParse<ThemePreference>(theme, true, out var preference) || !Enum.IsDefined(preference)) throw new ArgumentException("Use System, Light or Dark.");
        var pending = await ui.Invoke(() => session.CaptureMachineAsync(documentId, width, height, preference), cancellationToken);
        return new CallToolResult { Content = [ImageContentBlock.FromBytes(await pending, "image/png")] };
    }

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

    [McpServerTool(Name = "add_face_source_shape"), Description("Add an undoable four-corner Face Source Shape to a Panel2D. Omit corners to use the whole panel; otherwise supply topLeft/topRight/bottomRight/bottomLeft with x/y. Requires a convex nondegenerate quad and output <=4096 pixels per axis. Save the panel before generating a Face.")]
    public Task<FaceSourceShapeSnapshot> AddFaceSourceShape(Guid panelDocumentId, string name, CancellationToken cancellationToken, FaceQuad? corners = null) => ui.Invoke(() => session.AddFaceSourceShape(panelDocumentId, name, corners), cancellationToken);

    [McpServerTool(Name = "inspect_face_source_shapes", ReadOnly = true), Description("Read a bounded page of Face Source Shape IDs and corners in the specified Panel2D. Limit 1..200.")]
    public Task<IReadOnlyList<FaceSourceShapeSnapshot>> InspectShapes(Guid panelDocumentId, CancellationToken cancellationToken, int offset = 0, int limit = 50) => ui.Invoke(() => session.InspectFaceSourceShapes(panelDocumentId, offset, limit), cancellationToken);

    [McpServerTool(Name = "create_face"), Description("Create and initially save a fresh Face package using the editor's native blank/image workflow. Optional sourceImage must be inside the workspace, max 4096 pixels per dimension. Refuses existing authored/generated packages. May write assets before a failed save; never silently overwrites an existing Face package.")]
    public Task<FaceSnapshot> CreateFace(string name, CancellationToken cancellationToken, string? sourceImage = null) => ui.Invoke(() => session.CreateFace(name, sourceImage), cancellationToken);

    [McpServerTool(Name = "generate_face_from_panel"), Description("Generate a NEW, unsaved Face from a saved, clean Panel2D and persisted source shape ID, through the editor's perspective/artwork/mask/semantic conversion pipeline. Returns conversion counts and Face diagnostics. Writes generated intermediates; call save_face to persist the authored package. Does not regenerate an existing Face.")]
    public Task<FaceGenerationSnapshot> GenerateFace(Guid panelDocumentId, string shapeId, string name, CancellationToken cancellationToken) => ui.Invoke(() => session.GenerateFaceFromPanel(panelDocumentId, shapeId, name, cancellationToken), cancellationToken);

    [McpServerTool(Name = "open_face"), Description("Validate and reopen Assets/Faces/<Name>/asset.face inside the open project. Reads local workspace asset references only.")]
    public Task<FaceSnapshot> OpenFace(string path, CancellationToken cancellationToken) => ui.Invoke(() => session.OpenFaceDocument(path), cancellationToken);

    [McpServerTool(Name = "inspect_face", ReadOnly = true), Description("Read Face identity, source links, artwork/mask paths, conversion geometry, build-product status and a bounded page of semantic elements. Limit 1..200. Save success does not imply every derived product is current; inspect these diagnostics.")]
    public Task<FaceSnapshot> InspectFace(Guid documentId, CancellationToken cancellationToken, int offset = 0, int limit = 50) => ui.Invoke(() => session.InspectFace(documentId, offset, limit), cancellationToken);

    [McpServerTool(Name = "save_face"), Description("Persist a session Face using the existing save/export service. Refuses external changes to any file in the authored Face package since opening/saving. Generated previews may be written; inspect build diagnostics afterward.")]
    public Task<FaceSnapshot> SaveFace(Guid documentId, CancellationToken cancellationToken) => ui.Invoke(() => session.SaveFace(documentId), cancellationToken);

    [McpServerTool(Name = "capture_face", ReadOnly = true), Description("Return a PNG of the real WPF/Skia Face layout view fitted offscreen. No desktop capture/focus change; no build/save. Width 320..1920, height 240..1080; theme System/Light/Dark. Excludes shell, menus and dialogs; restores viewport afterward.")]
    public Task<CallToolResult> CaptureFace(Guid documentId, CancellationToken cancellationToken, int width = 1000, int height = 700, string theme = "System") => ui.Invoke(() =>
    {
        if (!Enum.TryParse<ThemePreference>(theme, true, out var preference) || !Enum.IsDefined(preference)) throw new ArgumentException("Use System, Light or Dark.");
        return new CallToolResult { Content = [ImageContentBlock.FromBytes(session.CaptureFace(documentId, width, height, preference), "image/png")] };
    }, cancellationToken);

    [McpServerTool(Name = "undo"), Description("Undo the last command in the specified Panel2D session document, including source-shape creation. Does not undo Face creation, copied files or saves.")]
    public Task<PanelSnapshot> Undo(Guid documentId, CancellationToken cancellationToken) => ui.Invoke(() => session.Undo(documentId), cancellationToken);

    [McpServerTool(Name = "redo"), Description("Redo the next command in the specified Panel2D session document, including source-shape creation.")]
    public Task<PanelSnapshot> Redo(Guid documentId, CancellationToken cancellationToken) => ui.Invoke(() => session.Redo(documentId), cancellationToken);

    [McpServerTool(Name = "capture_panel2d", ReadOnly = true), Description("Return a PNG of the actual WPF Panel2D view offscreen, without focus changes. Width 320..1920, height 240..1080; theme System/Light/Dark. Framing=current uses the session viewport, fit fits the panel, element fits the supplied elementId. Captures restore zoom/pan. Excludes window chrome, menus and dialogs.")]
    public Task<CallToolResult> CapturePanel(Guid documentId, CancellationToken cancellationToken, int width = 1000, int height = 700, string theme = "System", string framing = "current", string? elementId = null) => ui.Invoke(() =>
    {
        if (!Enum.TryParse<ThemePreference>(theme, true, out var preference) || !Enum.IsDefined(preference)) throw new ArgumentException("Use System, Light or Dark.");
        var png = session.CapturePanel(documentId, width, height, preference, framing, elementId);
        return new CallToolResult { Content = [ImageContentBlock.FromBytes(png, "image/png")] };
    }, cancellationToken);
}
