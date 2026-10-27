using System.ComponentModel;
using ModelContextProtocol.Server;

namespace OasisEditor.Avalonia.Mcp;

public sealed partial class EditorTools
{
    [McpServerTool(Name = "import_fml"), Description("Import a workspace FML through the shared real decoder, mapper and artwork/mask processors. Refuses existing import packages; commits panel elements as one undoable command. Recovered input definitions are returned for Machine setup, not persisted in Panel2D.")]
    public Task<object> ImportFml(string path, CancellationToken cancellationToken)
        => ui.Invoke<object>(() => { var result = session.ImportFml(path); capture.View.ShowPanel(); return new { result, workspace = Snapshot() }; }, cancellationToken);
    [McpServerTool(Name = "select_elements"), Description("Replace Panel2D multi-selection by stable IDs. Validates every ID first; does not dirty the document.")]
    public Task<object> SelectElements(string[] ids, CancellationToken cancellationToken) => ui.Invoke(() => Act(() => session.SelectElements(ids)), cancellationToken);

    [McpServerTool(Name = "move_selection"), Description("Move the selected panel elements atomically through one undoable command. Rejects locked transforms.")]
    public Task<object> MoveSelection(double dx, double dy, CancellationToken cancellationToken) => ui.Invoke(() => Act(() => session.MoveSelected(dx, dy)), cancellationToken);

    [McpServerTool(Name = "delete_selection"), Description("Delete selected Panel2D elements with one undoable command. Referenced source images remain available for undo/redo.")]
    public Task<object> DeleteSelection(CancellationToken cancellationToken) => ui.Invoke(() => Act(() => session.DeleteSelected()), cancellationToken);

    [McpServerTool(Name = "duplicate_selection"), Description("Duplicate selected Panel2D elements, preserving metadata and creating fresh stable IDs. One undoable command.")]
    public Task<object> DuplicateSelection(CancellationToken cancellationToken, double offsetX = 16, double offsetY = 16)
        => ui.Invoke(() => Act(() => session.DuplicateSelected(offsetX, offsetY)), cancellationToken);

    [McpServerTool(Name = "reorder_selection"), Description("Move selected Panel2D elements one step forward or backward in visual stacking order. One undoable command.")]
    public Task<object> ReorderSelection(bool forward, CancellationToken cancellationToken) => ui.Invoke(() => Act(() => session.MoveSelectionInOrder(forward)), cancellationToken);

    [McpServerTool(Name = "add_element"), Description("Add a Rectangle, Anchor, Zone or Label to the active Panel2D. Positive geometry required; does not save.")]
    public Task<object> AddElement(string kind, string name, double x, double y, double width, double height, CancellationToken cancellationToken)
        => ui.Invoke(() => Act(() => session.AddElement(kind, name, x, y, width, height)), cancellationToken);

    [McpServerTool(Name = "import_image"), Description("Decode a workspace image and copy a PNG into the active authored panel package. Adds one undoable image element; does not save the manifest.")]
    public Task<object> ImportImage(string path, CancellationToken cancellationToken, string? name = null)
        => ui.Invoke(() => Act(() => session.ImportImage(path, name)), cancellationToken);

    [McpServerTool(Name = "add_source_shape"), Description("Add a rectangular Face source region to Panel2D using one command. Defaults to visible panel content bounds.")]
    public Task<object> AddSourceShape(string name, CancellationToken cancellationToken, double? x = null, double? y = null, double? width = null, double? height = null)
        => ui.Invoke(() => Act(() => session.AddSourceShape(name, x, y, width, height)), cancellationToken);

    [McpServerTool(Name = "update_source_shape"), Description("Edit a Face source region using four finite, convex corners in top-left, top-right, bottom-right, bottom-left order. One undoable command.")]
    public Task<object> UpdateSourceShape(string id, string name, PointSnapshot[] corners, CancellationToken cancellationToken)
        => ui.Invoke(() => Act(() => session.UpdateSourceShape(id, name, corners)), cancellationToken);

    [McpServerTool(Name = "delete_source_shape"), Description("Delete a Panel2D Face source region through document history.")]
    public Task<object> DeleteSourceShape(string id, CancellationToken cancellationToken) => ui.Invoke(() => Act(() => session.DeleteSourceShape(id)), cancellationToken);

    [McpServerTool(Name = "create_face"), Description("Create and save a native blank Face package with the current shared Oasis schema. Refuses existing packages.")]
    public Task<object> CreateFace(string name, CancellationToken cancellationToken) => ui.Invoke(() => ActFace(() => faces.CreateFace(name)), cancellationToken);

    [McpServerTool(Name = "open_face"), Description("Open an existing current-schema Face package for native authoring. Preserves full authored fields; refuses dirty replacement.")]
    public Task<object> OpenFace(string path, CancellationToken cancellationToken) => ui.Invoke(() => ActFace(() => faces.OpenFace(path)), cancellationToken);

    [McpServerTool(Name = "select_face_element"), Description("Select a Face element by stable ID, or clear with null. Does not dirty the document.")]
    public Task<object> SelectFaceElement(CancellationToken cancellationToken, string? id = null) => ui.Invoke(() => ActFace(() => faces.Select(id)), cancellationToken);

    [McpServerTool(Name = "add_face_lamp"), Description("Add an authored lamp window to a native Face through document history. Lamp IDs use the actual source-format range; does not emulate lamps or save.")]
    public Task<object> AddFaceLamp(string name, double x, double y, double width, double height, CancellationToken cancellationToken, int lampIndex = 0)
        => ui.Invoke(() => ActFace(() => faces.AddLamp(name, x, y, width, height, lampIndex)), cancellationToken);

    [McpServerTool(Name = "update_face_element"), Description("Atomically edit a Face element's name, geometry, visibility, transform locking or lamp ID through document history.")]
    public Task<object> UpdateFaceElement(string id, CancellationToken cancellationToken, string? name = null, double? x = null, double? y = null,
        double? width = null, double? height = null, bool? visible = null, bool? locked = null, int? lampIndex = null)
        => ui.Invoke(() => ActFace(() => faces.UpdateElement(id, name, x, y, width, height, visible, locked, lampIndex)), cancellationToken);

    [McpServerTool(Name = "delete_face_element"), Description("Delete the selected Face element through one undoable command.")]
    public Task<object> DeleteFaceElement(CancellationToken cancellationToken) => ui.Invoke(() => ActFace(() => faces.DeleteSelected()), cancellationToken);

    [McpServerTool(Name = "face_history"), Description("Undo one Face command, or redo with redo=true. Does not save.")]
    public Task<object> FaceHistory(CancellationToken cancellationToken, bool redo = false)
        => ui.Invoke(() => ActFace(() => { if (redo) faces.Redo(); else faces.Undo(); }), cancellationToken);

    [McpServerTool(Name = "save_face"), Description("Save full native Face metadata atomically, refusing external changes to the authored package. Does not run artwork generation or export.")]
    public Task<object> SaveFace(CancellationToken cancellationToken) => ui.Invoke(() => ActFace(() => faces.Save()), cancellationToken);
}
