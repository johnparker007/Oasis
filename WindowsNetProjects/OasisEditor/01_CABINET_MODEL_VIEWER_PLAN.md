# Cabinet Model Viewer Plan

## Purpose

Add 3D cabinet support to Oasis Editor.

The first milestone was a simple editor document that can load a `.glb` cabinet model and display it in a usable 3D viewport. That is now in place. The next milestone is to use specially named meshes inside the `.glb` as face target surfaces that Oasis Face documents can be assigned to.

This feature is part of the longer-term workflow:

1. Users author/export common cabinet models as `.glb` files.
2. Cabinet designers add flat quad target meshes in Blender with names such as `OasisFace_TopGlass` and `OasisFace_BottomGlass`.
3. Oasis Editor loads the `.glb`, renders the cabinet, and detects those `OasisFace_` targets.
4. Oasis Editor lets users assign existing Face documents to detected cabinet face targets.
5. Oasis saves assignment metadata separately from the `.glb`.
6. The later Unity runtime/player loads the same `.glb` plus Oasis metadata and renders the Face backgrounds/lamp textures onto those targets.

## Current status

Implemented so far:

- `.glb` cabinet files can be opened as Cabinet3D documents.
- The Cabinet Model Viewer renders the model in-editor.
- The viewer supports orbit, pan, zoom, reset camera, grid/orientation helpers, and safe load errors.
- GLB loading uses a Helix viewport with SharpGLTF-backed parsing/conversion where needed.
- Recent material work improved base visual fidelity beyond a single grey mesh.

## Core principle

The `.glb` is the cabinet geometry/material asset. Oasis-specific assignments and overrides must be stored separately.

Do not mutate or re-export the `.glb` just to store Oasis metadata.

Preferred file pairing:

```text
Assets/Cabinets/GenesisCabinet.glb
Assets/Cabinets/GenesisCabinet.cabinet3d
```

The `.cabinet3d` file should reference the `.glb` and contain Oasis-specific metadata such as which Face document is assigned to each detected face target.

## Preferred face target workflow

The preferred way to define cabinet face targets is inside the source model.

In Blender, the cabinet designer creates simple flat quad meshes over the glass/artwork areas and names them using this convention:

```text
OasisFace_TopGlass
OasisFace_BottomGlass
OasisFace_ButtonPanel
```

When exported to `.glb`, Oasis Editor should scan the glTF scene/node hierarchy for these named objects and import them as face target candidates.

Advantages:

- Blender remains the precise modelling/alignment tool.
- Oasis Editor does not need to become a full 3D modelling editor.
- The target planes travel with the cabinet model.
- Unity can later discover or address the same named targets.
- The Editor can focus on assignment, preview, validation, and metadata.

## Face target detection rules

A face target is any glTF node/mesh/object whose effective name starts with:

```text
OasisFace_
```

The display name should be derived from the suffix:

```text
OasisFace_TopGlass -> TopGlass or Top Glass, depending on existing UI naming conventions
```

Detection should preserve:

- source object/node name
- cabinet-local/world-transformed quad corner positions
- derived normal
- bounds/center
- source mesh/material visibility state where useful

A target should ideally be a single quad. For the first implementation, accept either:

- one quad made from two triangles, or
- any planar mesh where a simple rectangular/quad representation can be derived safely

If the geometry is not usable as a face target, report it as invalid/non-displayable rather than crashing.

## Face target geometry model

The target should ultimately be represented in Oasis as a named quad in cabinet/model coordinates.

Example future data shape:

```json
{
  "id": "topGlass",
  "sourceName": "OasisFace_TopGlass",
  "displayName": "Top Glass",
  "corners": [
    [-0.42, 1.85, -0.03],
    [ 0.42, 1.85, -0.03],
    [ 0.42, 1.20, -0.03],
    [-0.42, 1.20, -0.03]
  ],
  "frontSide": "normal"
}
```

Corner order defines texture mapping:

```text
top-left
top-right
bottom-right
bottom-left
```

The quad normal is derived from corner winding. `frontSide` controls whether the visible/rendered side is the derived normal side or the inverted side.

Do not add `uvRotation`, `uvFlipX`, or `uvFlipY` in the first design. If a real use case appears later, a minimal mapping flag can be added then. For now:

- corner order defines orientation
- `frontSide` defines physical facing direction

## Face document assignment direction

Any Oasis Face document should eventually be able to choose a target face from the detected `OasisFace_` targets in the active/associated cabinet model.

The assignment should be metadata, not baked into the `.glb`.

Possible future `.cabinet3d` shape:

```json
{
  "version": 1,
  "model": {
    "path": "GenesisCabinet.glb",
    "scale": 1.0,
    "upAxis": "Y"
  },
  "targets": [
    {
      "id": "topGlass",
      "sourceName": "OasisFace_TopGlass",
      "displayName": "Top Glass",
      "frontSide": "normal"
    }
  ],
  "faceAssignments": [
    {
      "faceDocumentPath": "Faces/TopGlass.face",
      "targetId": "topGlass"
    }
  ]
}
```

The exact persistence format can evolve, but keep these concepts separate:

- detected target geometry from the `.glb`
- user-facing target identity/name
- Face document assignment
- future per-target override data

## Editor preview direction

The Editor should eventually show a simple preview of assigned Face documents on top of the cabinet model.

Recommended preview staging:

1. Detect and list `OasisFace_` targets.
2. Render target overlays in the Cabinet Model Viewer with simple tint/wireframe/normal indicator.
3. Let a Face document select one target from detected faces.
4. Render the Face document's static/background image onto the target in the Cabinet Model Viewer.
5. Defer full dynamic lamp simulation on the 3D cabinet until the Unity/runtime path is further along.

Do not make full lamp flashing on the 3D editor cabinet a near-term requirement. It would be useful later, but it is substantially more work because it needs runtime-style face compositing, lamp state simulation, texture updates, and performance handling. A static background preview gives most of the layout confidence for much less risk.

## Future cabinet surface edit modes

Later work may add editor-created or editor-adjusted target surfaces, but this is secondary to Blender-authored `OasisFace_` targets.

Potential later tools:

- create surface in editor
- select surface
- move/rotate/scale surface
- edit individual corners
- flip front/back
- show normal arrow
- front/side/top orthographic views
- selected-surface orthographic view
- snap quad/corners to model surface
- preview assigned face document texture

These are future tasks. Do not implement them in the `OasisFace_` detection milestone.

## Rendering technology guidance

Keep the current split unless there is a strong reason to change it:

- SharpGLTF/glTF parsing and model data extraction
- Helix/WPF viewport rendering and camera controls

Important requirements:

- Must run inside a WPF editor document.
- Must load `.glb`/glTF 2.0 binary files.
- Must remain suitable as a foundation for face target overlays and picking.
- Should not require launching Unity.
- Should not put rendering/domain logic directly in WPF code-behind.

## Suggested architecture

Keep a clear split between UI, document state, detected target data, and model loading.

Feature area:

```text
OasisEditor/Features/CabinetEditor/
```

Possible additions:

```text
Features/CabinetEditor/
  Models/
    CabinetFaceTarget.cs
    CabinetFaceTargetDetectionResult.cs
  Services/
    ICabinetFaceTargetDetector.cs
    SharpGltfCabinetFaceTargetDetector.cs
  ViewModels/
    CabinetFaceTargetViewModel.cs
```

Adapt names and paths to existing project conventions.

WPF view code-behind should remain thin glue only. Put target detection, load state, commands, and user-facing state in ViewModels/services.

## Camera/view guidance

The current perspective orbit view is enough for the detection milestone.

Design the camera layer so these can be added later:

- front orthographic
- left/right orthographic
- top orthographic
- selected-surface orthographic

Avoid hard-coding assumptions that prevent orthographic view modes later.

## UX expectations

Expected next-stage UI:

- A list/panel/section showing detected `OasisFace_` targets for the loaded model.
- A count/status such as `Detected 2 Oasis face targets`.
- Clear warning if no `OasisFace_` targets are found.
- Optional simple overlay rendering of detected targets in the viewport.
- Optional selection sync between the list and viewport overlay.
- No hard-coded theme colors.

## Testing guidance

Codex cannot run the WPF build/tests in its environment. Add automated tests only where practical and UI-independent.

John will test locally:

- solution builds
- app launches
- project open/create still works
- cabinet model viewer still opens `.glb` files
- existing material/textured rendering still works
- `.glb` with `OasisFace_` objects reports detected targets
- `.glb` without `OasisFace_` objects shows a safe empty/warning state
- invalid `OasisFace_` geometry is reported safely
- camera orbit/pan/zoom/reset still work
- existing Panel2D editor workflow is unaffected

## Implementation caution

Do not refactor unrelated editor systems as part of this task.

Do not scan every Markdown file. Start with:

1. `AGENTS.md`
2. `00_CURRENT_PRIORITY.md`
3. this file
4. the task-specific prompt/document, if one is supplied

Then inspect only source files needed to integrate `OasisFace_` target detection into the existing Cabinet Model Viewer and Face document workflows.
