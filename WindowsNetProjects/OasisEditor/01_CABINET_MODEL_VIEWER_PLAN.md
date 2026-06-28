# Cabinet Model Viewer Plan

## Purpose

Add 3D cabinet support to Oasis Editor.

The first milestones are now in place: Oasis can open/save `.cabinet3d` documents, reference `.glb` cabinet model assets without overwriting them, detect `OasisFace_` target quads inside the GLB, assign Face documents to detected targets, and render static Face previews on those targets.

The next design area is per-target mapping metadata: the Editor must let users correct target facing and texture orientation when Blender-authored blank quads are not oriented the way Oasis expects.

## Core workflow

1. Users author/export common cabinet models as `.glb` files.
2. Cabinet designers add flat quad target meshes in Blender with names such as `OasisFace_TopGlass` and `OasisFace_BottomGlass`.
3. Oasis Editor creates/opens a `.cabinet3d` document that references the `.glb`.
4. Oasis Editor detects `OasisFace_` targets from the GLB.
5. Users assign existing Face documents to detected cabinet face targets.
6. Users can correct per-target preview mapping in the `.cabinet3d` document.
7. The later Unity runtime/player loads the same `.glb` plus Oasis metadata and renders Face backgrounds/lamp textures onto those targets.

## Current status

Implemented so far:

- `.cabinet3d` files are the saveable Oasis cabinet documents.
- `.glb` files are source model assets and must not be overwritten by Oasis save operations.
- Cabinet3D documents reference `.glb` model assets.
- The Cabinet Model Viewer renders the referenced model in-editor.
- The viewer supports orbit, pan, zoom, reset camera, grid/orientation helpers, and safe load errors.
- GLB loading uses a Helix viewport with SharpGLTF-backed parsing/conversion where needed.
- Material work improved base visual fidelity beyond a single grey mesh.
- `OasisFace_` target meshes are detected and listed.
- Face documents can store an assigned cabinet face target ID.
- Static Face background/artwork preview can render onto assigned targets.

## Core principle

The `.glb` is the cabinet geometry/material asset. Oasis-specific assignments and overrides must be stored separately in `.cabinet3d` and related Oasis documents.

Do not mutate or re-export the `.glb` just to store Oasis metadata.

Preferred file pairing:

```text
Assets/Cabinets/GenesisCabinet.glb
Assets/Cabinets/GenesisCabinet.cabinet3d
```

The `.cabinet3d` file should reference the `.glb` and contain Oasis-specific metadata such as per-target mapping overrides and preview options.

## Preferred face target workflow

The preferred way to define cabinet face targets is inside the source model.

In Blender, the cabinet designer creates simple flat quad meshes over the glass/artwork areas and names them using this convention:

```text
OasisFace_TopGlass
OasisFace_BottomGlass
OasisFace_ButtonPanel
```

When exported to `.glb`, Oasis Editor scans the glTF scene/node hierarchy for these named objects and imports them as face target candidates.

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
OasisFace_TopGlass -> Top Glass
```

Detection should preserve:

- source object/node name
- cabinet-local/world-transformed quad corner positions
- derived normal
- bounds/center
- source mesh/material visibility state where useful

A target should ideally be a single quad. If the geometry is not usable as a face target, report it as invalid/non-displayable rather than crashing.

## Cabinet document persistence

A `.cabinet3d` document is the saveable cabinet metadata file.

Example shape:

```json
{
  "version": 1,
  "model": {
    "path": "GenesisCabinet.glb",
    "scale": 1.0,
    "upAxis": "Y"
  },
  "targetOverrides": [
    {
      "targetId": "topGlass",
      "frontSide": "normal",
      "textureRotation": 0
    }
  ],
  "preview": {
    "showTargetOverlays": true,
    "showFaceBackgrounds": true
  }
}
```

Use existing code conventions over this exact JSON shape if the implementation already chose a schema, but preserve these concepts:

- source GLB model reference
- detected target identity from the GLB
- per-target Oasis overrides
- editor preview settings

## Face target geometry model

The detected target should be represented in Oasis as a named quad in cabinet/model coordinates.

Example detected target shape:

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
  ]
}
```

The source quad geometry comes from the GLB. Oasis overrides should not modify the source geometry unless a later explicit editing tool is added.

## Per-target mapping overrides

Because Blender-authored target quads are often blank and hard to visually orient, Oasis should provide per-target mapping controls in the Cabinet3D document.

Recommended first override fields:

```json
{
  "targetId": "topGlass",
  "frontSide": "normal",
  "textureRotation": 0
}
```

Where:

```text
frontSide: normal | inverted
textureRotation: 0 | 90 | 180 | 270
```

`frontSide` controls which side of the target quad receives the Face preview/rendering. This fixes cases where the Face appears on the inside/back side of the quad.

`textureRotation` controls the 2D orientation of the Face preview on the target. This fixes common authoring cases where the plane is spatially correct but the artwork is rotated on the quad.

Do not add arbitrary UV editing in the first version. Avoid `uvFlipX`/`uvFlipY` unless a concrete need appears. Start with front/back plus 90-degree rotation steps.

## Face document assignment direction

Any Oasis Face document can choose a target face from the detected `OasisFace_` targets in an open/associated cabinet model.

The assignment is metadata, not baked into the `.glb`.

The Face document stores the assigned target ID. The Cabinet3D document stores per-target mapping overrides.

Keep these concepts separate:

- detected target geometry from the `.glb`
- Face document assignment to target ID
- Cabinet3D per-target mapping overrides
- future runtime export metadata

## Editor UI direction

The Cabinet3D viewer should expose detected targets as selectable editor items.

Preferred UI direction:

- Show detected `OasisFace_` targets in a Cabinet3D hierarchy/list.
- Selecting a target should populate the Inspector.
- The Inspector should allow editing per-target Cabinet3D overrides:
  - Front Side: Normal/Inverted
  - Texture Rotation: 0/90/180/270
- Changes should update the static Face preview immediately where practical.
- Changes should mark the `.cabinet3d` document dirty.
- Changes should save/load with the `.cabinet3d` document.
- Editing should follow existing document command/undo/redo patterns if Cabinet3D already has them.

Do not make these settings part of the Face document. The same Face can be assigned to a correctly configured cabinet target; the target-specific mapping belongs to the cabinet.

## Editor preview direction

The Editor should show a simple preview of assigned Face documents on top of the cabinet model.

Recommended preview staging:

1. Detect and list `OasisFace_` targets.
2. Render target overlays in the Cabinet Model Viewer with simple tint/wireframe/normal indicator.
3. Let a Face document select one target from detected faces.
4. Render the Face document's static/background image onto the target in the Cabinet Model Viewer.
5. Add per-target `frontSide` and `textureRotation` controls to correct orientation.
6. Defer full dynamic lamp simulation on the 3D cabinet until the Unity/runtime path is further along.

Do not make full lamp flashing on the 3D editor cabinet a near-term requirement. It would be useful later, but it is substantially more work because it needs runtime-style face compositing, lamp state simulation, texture updates, and performance handling.

## Future cabinet surface edit modes

Later work may add editor-created or editor-adjusted target surfaces, but this is secondary to Blender-authored `OasisFace_` targets.

Potential later tools:

- create surface in editor
- select surface in viewport
- move/rotate/scale surface
- edit individual corners
- show normal arrow
- front/side/top orthographic views
- selected-surface orthographic view
- snap quad/corners to model surface

These are future tasks. Do not implement them as part of the per-target mapping override milestone.

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

Keep a clear split between UI, document state, detected target data, mapping overrides, and model loading.

Feature area:

```text
OasisEditor/Features/CabinetEditor/
```

Possible additions:

```text
Features/CabinetEditor/
  Models/
    CabinetFaceTarget.cs
    CabinetFaceTargetOverride.cs
  Commands/
    SetCabinetFaceTargetFrontSideCommand.cs
    SetCabinetFaceTargetTextureRotationCommand.cs
  ViewModels/
    CabinetFaceTargetViewModel.cs
    CabinetFaceTargetInspectorViewModel.cs
```

Adapt names and paths to existing project conventions.

WPF view code-behind should remain thin glue only. Put target override editing, load state, commands, and user-facing state in ViewModels/services.

## Camera/view guidance

The current perspective orbit view is enough for the mapping override milestone.

Design the camera layer so these can be added later:

- front orthographic
- left/right orthographic
- top orthographic
- selected-surface orthographic

Avoid hard-coding assumptions that prevent orthographic view modes later.

## UX expectations

Expected next-stage UI:

- A Cabinet3D target hierarchy/list showing detected `OasisFace_` targets.
- Selecting a target populates the Inspector.
- Inspector shows source name, stable target ID, validity, front side, and texture rotation.
- Preview updates when front side or rotation changes.
- Clear warning if no `OasisFace_` targets are found.
- No hard-coded theme colors.

## Testing guidance

Codex cannot run the WPF build/tests in its environment. Add automated tests only where practical and UI-independent.

John will test locally:

- solution builds
- app launches
- project open/create still works
- `.cabinet3d` opens and references its `.glb`
- `.glb` source file is never overwritten by Save Document
- detected targets appear in the Cabinet3D target list/hierarchy
- selecting a target shows Inspector properties
- changing front side moves/changes the visible preview side as expected
- changing texture rotation rotates the static Face preview 0/90/180/270
- changes mark the `.cabinet3d` document dirty
- save/reopen preserves mapping overrides
- assigned Face static previews still render
- camera orbit/pan/zoom/reset still work
- existing Panel2D and Face workflows are unaffected

## Implementation caution

Do not refactor unrelated editor systems as part of this task.

Do not scan every Markdown file. Start with:

1. `AGENTS.md`
2. `00_CURRENT_PRIORITY.md`
3. this file
4. the task-specific prompt/document, if one is supplied

Then inspect only source files needed to integrate per-target mapping overrides into the existing Cabinet3D document, viewer, hierarchy/list, Inspector, save/load, and preview rendering workflows.
