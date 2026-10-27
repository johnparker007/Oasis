using System.ComponentModel;
using ModelContextProtocol.Server;
using OasisEditor.Features.CabinetEditor.Models;
using OasisEditor.Avalonia.Rendering3D;

namespace OasisEditor.Avalonia.Mcp;

/// <summary>Headless MCP access to the same authored 3D documents and scene view as the Avalonia workspace.</summary>
public sealed partial class EditorTools
{
    private CabinetAuthoringSession Cabinets => capture.View.Cabinets;
    private PrefabAuthoringSession Prefabs => capture.View.Prefabs;

    private object Scene3DSnapshot()
    {
        var view = capture.View;
        var kind = view.SceneKind;
        var documentJson = kind switch
        {
            "Cabinet3D" when Cabinets.FilePath is not null => CabinetDocumentStorage.Serialize(Cabinets.Document),
            "Prefab" when Prefabs.FilePath is not null => Object3DDocumentStorage.Serialize(Prefabs.Document),
            "Machine" when Machines.Document is { } machine => MachineDocumentStorage.Serialize(machine),
            _ => null
        };
        var asset = kind switch
        {
            "Cabinet3D" => (object)new { path = Cabinets.FilePath, Cabinets.IsDirty, Cabinets.CanUndo, Cabinets.CanRedo, document = Cabinets.FilePath is null ? null : Cabinets.Document },
            "Prefab" => new { path = Prefabs.FilePath, Prefabs.IsDirty, Prefabs.CanUndo, Prefabs.CanRedo, document = Prefabs.FilePath is null ? null : Prefabs.Document },
            "Machine" => new { path = Machines.MachinePath, Machines.IsDirty, Machines.CanUndo, Machines.CanRedo, document = Machines.Document },
            _ => null
        };
        return new { scene = view.InspectScene3D(), sceneKind = kind, sceneLibraryRoot = view.SceneLibraryRoot,
            asset, nativeDocumentSerializedJson = documentJson };
    }

    private void RequireCabinet3D()
    { if (capture.View.SceneKind != "Cabinet3D" || Cabinets.FilePath is null) throw new InvalidOperationException("Open a Cabinet3D asset first."); }
    private void RequirePrefab3D()
    { if (capture.View.SceneKind != "Prefab" || Prefabs.FilePath is null) throw new InvalidOperationException("Open a Prefab asset first."); }

    [McpServerTool(Name = "inspect_scene3d", ReadOnly = true), Description("Inspect the actual loaded Cabinet3D, Prefab or Machine scene, hierarchy, bounds, selection, visibility, camera, diagnostics and native serialized document. Rendering is the real offscreen GPU scene.")]
    public Task<object> InspectScene3D(CancellationToken cancellationToken) => ui.InvokeAsync<object>(async () => { await capture.View.WaitForSceneAsync(); return Scene3DSnapshot(); }, cancellationToken);

    [McpServerTool(Name = "open_cabinet3d"), Description("Open an existing Cabinet3D manifest in the current project. Path is project-relative or an absolute path inside the project; unsaved edits are protected by the native session.")]
    public Task<object> OpenCabinet3D(string path, CancellationToken cancellationToken)
        => ui.InvokeAsync<object>(async () => { await capture.View.OpenCabinet3DAsync(path); return Scene3DSnapshot(); }, cancellationToken);

    [McpServerTool(Name = "create_cabinet3d"), Description("Create a native Cabinet3D package by importing an existing project-relative GLB, then open it in the scene view.")]
    public Task<object> CreateCabinet3D(string name, string sourceProjectRelativePath, CancellationToken cancellationToken)
        => ui.InvokeAsync<object>(async () => { var path = Cabinets.Create(name, sourceProjectRelativePath); await capture.View.OpenCabinet3DAsync(path); return Scene3DSnapshot(); }, cancellationToken);

    [McpServerTool(Name = "open_prefab"), Description("Open an existing Object3D Prefab manifest in the current project. Path is project-relative or an absolute path inside the project; unsaved edits are protected by the native session.")]
    public Task<object> OpenPrefab(string path, CancellationToken cancellationToken)
        => ui.InvokeAsync<object>(async () => { await capture.View.OpenPrefab3DAsync(path); return Scene3DSnapshot(); }, cancellationToken);

    [McpServerTool(Name = "create_prefab"), Description("Create and open a native Object3D Prefab. Optionally import an existing project-relative GLB onto the root node.")]
    public Task<object> CreatePrefab(string name, CancellationToken cancellationToken, string? sourceProjectRelativePath = null)
        => ui.InvokeAsync<object>(async () => { var path = Prefabs.Create(name, sourceProjectRelativePath); await capture.View.OpenPrefab3DAsync(path); return Scene3DSnapshot(); }, cancellationToken);

    [McpServerTool(Name = "select_scene_node"), Description("Select or clear a Cabinet3D, Prefab or Machine hierarchy node by its stable scene ID. Selection does not dirty authored content.")]
    public Task<object> SelectSceneNode(CancellationToken cancellationToken, string? id = null)
        => ui.Invoke<object>(() => { capture.View.SelectScene3DNode(id); return Scene3DSnapshot(); }, cancellationToken);

    [McpServerTool(Name = "set_scene_camera"), Description("Set the scene camera target, distance, yaw/pitch in radians and field of view in degrees. Invalid camera values are rejected.")]
    public Task<object> SetSceneCamera(float targetX, float targetY, float targetZ, float distance, float yaw, float pitch, CancellationToken cancellationToken, float fieldOfView = 45)
        => ui.Invoke<object>(() => { capture.View.SceneView.SetCamera(new(new(targetX, targetY, targetZ), distance, yaw, pitch, fieldOfView)); return Scene3DSnapshot(); }, cancellationToken);

    [McpServerTool(Name = "pick_scene3d"), Description("Pick the nearest visible scene triangle at normalized device coordinates from -1 to 1 and select its owning authored node. This uses loaded scene geometry, not a mock hit test.")]
    public Task<object> PickScene3D(float normalizedX, float normalizedY, CancellationToken cancellationToken)
        => ui.Invoke<object>(() =>
        {
            if (!float.IsFinite(normalizedX) || normalizedX is < -1 or > 1 || !float.IsFinite(normalizedY) || normalizedY is < -1 or > 1)
                throw new ArgumentOutOfRangeException(nameof(normalizedX), "Pick coordinates must be finite and between -1 and 1.");
            var pick = capture.View.PickScene3D(normalizedX, normalizedY);
            return new { pick, state = Scene3DSnapshot() };
        }, cancellationToken);

    [McpServerTool(Name = "set_scene_visibility"), Description("Toggle visual, collider and trigger scene geometry. Omitted values keep their current setting.")]
    public Task<object> SetSceneVisibility(CancellationToken cancellationToken, bool? visuals = null, bool? colliders = null, bool? triggers = null)
        => ui.Invoke<object>(() => { if (visuals.HasValue) capture.View.SceneView.ShowVisuals = visuals.Value; if (colliders.HasValue) capture.View.SceneView.ShowColliders = colliders.Value; if (triggers.HasValue) capture.View.SceneView.ShowTriggers = triggers.Value; return Scene3DSnapshot(); }, cancellationToken);

    [McpServerTool(Name = "reload_scene3d"), Description("Reload the active scene from native Cabinet3D, Prefab and Machine documents and their referenced GLB assets.")]
    public Task<object> ReloadScene3D(CancellationToken cancellationToken)
        => ui.InvokeAsync<object>(async () => { await capture.View.ReloadSceneAsync(); return Scene3DSnapshot(); }, cancellationToken);

    [McpServerTool(Name = "set_scene_library_root"), Description("Set an explicit existing directory as the Oasis Library root for resolving Machine scene references. The workspace validates the root and rejects reparse-point roots.")]
    public Task<object> SetSceneLibraryRoot(string directory, CancellationToken cancellationToken)
        => ui.Invoke<object>(() => { capture.View.SetSceneLibraryRoot(directory); return Scene3DSnapshot(); }, cancellationToken);

    [McpServerTool(Name = "set_cabinet_model"), Description("Set Cabinet model scale and/or up-axis as one native undoable edit. Scale must be positive and finite; up-axis is X, Y or Z.")]
    public Task<object> SetCabinetModel(CancellationToken cancellationToken, double? scale = null, string? upAxis = null)
        => ui.Invoke<object>(() => { RequireCabinet3D(); Cabinets.SetModel(scale, upAxis); return Scene3DSnapshot(); }, cancellationToken);

    [McpServerTool(Name = "import_cabinet_model"), Description("Replace the active Cabinet model with an existing project-relative GLB, copied safely into its package as a native undoable edit.")]
    public Task<object> ImportCabinetModel(string sourceProjectRelativePath, CancellationToken cancellationToken)
        => ui.Invoke<object>(() => { RequireCabinet3D(); Cabinets.ImportModel(sourceProjectRelativePath); return Scene3DSnapshot(); }, cancellationToken);

    [McpServerTool(Name = "set_cabinet_surface_target"), Description("Set one Cabinet face-target front side, face rotation and horizontal flip as a native undoable edit.")]
    public Task<object> SetCabinetSurfaceTarget(string targetId, string frontSide, CancellationToken cancellationToken, int faceRotation = 0, bool faceFlipHorizontal = false)
        => ui.Invoke<object>(() => { RequireCabinet3D(); Cabinets.SetSurfaceTarget(new(targetId, frontSide, faceRotation, faceFlipHorizontal)); return Scene3DSnapshot(); }, cancellationToken);

    [McpServerTool(Name = "set_cabinet_reflections"), Description("Replace all Cabinet reflection definitions using the typed native schema; validation and undo history are handled by the Cabinet session.")]
    public Task<object> SetCabinetReflections(CabinetReflectionDefinition[] reflections, CancellationToken cancellationToken)
        => ui.Invoke<object>(() => { RequireCabinet3D(); Cabinets.SetReflections(reflections); return Scene3DSnapshot(); }, cancellationToken);

    [McpServerTool(Name = "add_prefab_node"), Description("Add a named child node under the given Prefab node using a native undoable hierarchy edit; returns its stable ID.")]
    public Task<object> AddPrefabNode(string parentNodeId, string name, CancellationToken cancellationToken)
        => ui.Invoke<object>(() => { RequirePrefab3D(); var id = Prefabs.AddNode(parentNodeId, name); return new { id, state = Scene3DSnapshot() }; }, cancellationToken);

    [McpServerTool(Name = "rename_prefab_node"), Description("Rename a Prefab node as one native undoable edit.")]
    public Task<object> RenamePrefabNode(string nodeId, string name, CancellationToken cancellationToken)
        => ui.Invoke<object>(() => { RequirePrefab3D(); Prefabs.RenameNode(nodeId, name); return Scene3DSnapshot(); }, cancellationToken);

    [McpServerTool(Name = "set_prefab_node_transform"), Description("Replace one Prefab node's typed transform using the native schema and undo history.")]
    public Task<object> SetPrefabNodeTransform(string nodeId, MachineObjectTransform transform, CancellationToken cancellationToken)
        => ui.Invoke<object>(() => { RequirePrefab3D(); Prefabs.SetTransform(nodeId, transform); return Scene3DSnapshot(); }, cancellationToken);

    [McpServerTool(Name = "reparent_prefab_node"), Description("Reparent a Prefab subtree under another node at an optional child index. The session rejects cycles and invalid indexes.")]
    public Task<object> ReparentPrefabNode(string nodeId, string newParentId, CancellationToken cancellationToken, int? index = null)
        => ui.Invoke<object>(() => { RequirePrefab3D(); Prefabs.ReparentNode(nodeId, newParentId, index); return Scene3DSnapshot(); }, cancellationToken);

    [McpServerTool(Name = "reorder_prefab_children"), Description("Set the exact order of all current children of a Prefab node. Every child ID must appear once.")]
    public Task<object> ReorderPrefabChildren(string parentNodeId, string[] orderedChildIds, CancellationToken cancellationToken)
        => ui.Invoke<object>(() => { RequirePrefab3D(); Prefabs.ReorderChildren(parentNodeId, orderedChildIds); return Scene3DSnapshot(); }, cancellationToken);

    [McpServerTool(Name = "remove_prefab_node"), Description("Remove a Prefab node and its entire descendant subtree as one native undoable edit. The root cannot be removed.")]
    public Task<object> RemovePrefabNode(string nodeId, CancellationToken cancellationToken)
        => ui.Invoke<object>(() => { RequirePrefab3D(); Prefabs.RemoveNode(nodeId); return Scene3DSnapshot(); }, cancellationToken);

    [McpServerTool(Name = "import_prefab_model"), Description("Import a project-relative GLB onto an existing Prefab node, preserving native package validation and undo history.")]
    public Task<object> ImportPrefabModel(string nodeId, string sourceProjectRelativePath, CancellationToken cancellationToken, double scale = 1, string upAxis = "Y")
        => ui.Invoke<object>(() => { RequirePrefab3D(); Prefabs.ImportModel(nodeId, sourceProjectRelativePath, scale, upAxis); return Scene3DSnapshot(); }, cancellationToken);

    [McpServerTool(Name = "set_prefab_model"), Description("Set the typed Model component on a Prefab node. The model path must resolve inside the Prefab package.")]
    public Task<object> SetPrefabModel(string nodeId, Object3DModelDefinition model, CancellationToken cancellationToken)
        => ui.Invoke<object>(() => { RequirePrefab3D(); Prefabs.SetModel(nodeId, model); return Scene3DSnapshot(); }, cancellationToken);

    [McpServerTool(Name = "set_prefab_collider"), Description("Set the typed Collider component on a Prefab node, including shape, source frame and trigger flag.")]
    public Task<object> SetPrefabCollider(string nodeId, Object3DColliderDefinition collider, CancellationToken cancellationToken)
        => ui.Invoke<object>(() => { RequirePrefab3D(); Prefabs.SetCollider(nodeId, collider); return Scene3DSnapshot(); }, cancellationToken);

    [McpServerTool(Name = "set_prefab_rigidbody"), Description("Set the typed Rigidbody component on a Prefab node, including static/dynamic/kinematic mode and mass settings.")]
    public Task<object> SetPrefabRigidbody(string nodeId, Object3DRigidbodyDefinition rigidbody, CancellationToken cancellationToken)
        => ui.Invoke<object>(() => { RequirePrefab3D(); Prefabs.SetRigidbody(nodeId, rigidbody); return Scene3DSnapshot(); }, cancellationToken);

    [McpServerTool(Name = "remove_prefab_component"), Description("Remove a Model, Collider or Rigidbody component from a Prefab node. Transform is required and cannot be removed.")]
    public Task<object> RemovePrefabComponent(string nodeId, string kind, CancellationToken cancellationToken)
        => ui.Invoke<object>(() => { RequirePrefab3D(); Prefabs.RemoveComponent(nodeId, kind); return Scene3DSnapshot(); }, cancellationToken);

    [McpServerTool(Name = "scene_history"), Description("Undo or redo one command in the active Cabinet3D, Prefab or Machine document; history remains document-scoped.")]
    public Task<object> SceneHistory(CancellationToken cancellationToken, bool redo = false)
        => ui.Invoke<object>(() =>
        {
            switch (capture.View.SceneKind)
            {
                case "Cabinet3D": if (redo) Cabinets.Redo(); else Cabinets.Undo(); break;
                case "Prefab": if (redo) Prefabs.Redo(); else Prefabs.Undo(); break;
                case "Machine": if (redo) Machines.Redo(); else Machines.Undo(); break;
                default: throw new InvalidOperationException("Open a 3D document before using its history.");
            }
            return Scene3DSnapshot();
        }, cancellationToken);

    [McpServerTool(Name = "save_scene_document"), Description("Save the active Cabinet3D, Prefab or Machine document using its native validator and atomic storage path.")]
    public Task<object> SaveSceneDocument(CancellationToken cancellationToken)
        => ui.Invoke<object>(() =>
        {
            switch (capture.View.SceneKind)
            {
                case "Cabinet3D": Cabinets.Save(); break;
                case "Prefab": Prefabs.Save(); break;
                case "Machine": Machines.Save(); break;
                default: throw new InvalidOperationException("Open a 3D document before saving.");
            }
            return Scene3DSnapshot();
        }, cancellationToken);
}
